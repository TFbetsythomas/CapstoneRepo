# Azure DevOps integration

Gate 2 quality-gate adapter. Runs the existing TestShield AI `POST /api/projects/{projectId}/tests/run` workflow from an Azure DevOps pipeline. TestShield remains the source of truth for generation, execution, validation, regression, coverage, and Safe / Review / Block. This folder is an adapter only.

Product handover: `docs/project-overview.md`. Jira is not implemented.

## What it does

1. Calls the existing TestShield run endpoint.
2. Prints decision, risk, coverage, findings, and result counts.
3. Maps the TestShield decision to a process exit code:
   - **Safe** → `0` (pipeline continues)
   - **Review** → `0` (pipeline continues)
   - **Block** → `1` (pipeline fails)
4. Non-success HTTP responses and invalid JSON also exit `1`.

Review does not fail the pipeline. Block does.

## Prerequisites

- A running TestShield AI API
- A TestShield project ID that already has an imported OpenAPI specification
- An Azure DevOps pipeline that can check out this repository
- PowerShell 7 (`pwsh`) on the agent

## Configure TestShield URL and project ID

Do not hard-code these values in scripts.

Pipeline variables or template parameters:

| Name | Purpose |
|---|---|
| `TESTSHIELD_BASE_URL` | TestShield API origin, for example `https://testshield.example.com` |
| `TESTSHIELD_PROJECT_ID` | TestShield project GUID |

The PowerShell script also accepts `-BaseUrl` and `-ProjectId`.

## Optional token

If the TestShield API requires a bearer token, set a **secret** pipeline variable named `TESTSHIELD_TOKEN`. The YAML passes it only as an environment variable. The script sends:

```text
Authorization: Bearer <token>
```

If the token is omitted, the request is unauthenticated.

Never:

- commit a token
- put a token in YAML
- echo `TESTSHIELD_TOKEN`
- include a token in logs or exception text

## Include the YAML in an Azure DevOps pipeline

Example consuming pipeline (pool and variable groups are defined by the consumer):

```yaml
trigger:
  - main

# Choose an agent pool in the consuming pipeline. This template does not assume one.

variables:
  TESTSHIELD_BASE_URL: https://testshield.example.com
  TESTSHIELD_PROJECT_ID: 00000000-0000-0000-0000-000000000000
  # TESTSHIELD_TOKEN is a secret variable configured in Azure DevOps, not in source.

stages:
  - stage: Regression
    jobs:
      - job: TestShield
        steps:
          - template: ci/azure-devops/testshield-regression.yml
            parameters:
              testShieldBaseUrl: $(TESTSHIELD_BASE_URL)
              testShieldProjectId: $(TESTSHIELD_PROJECT_ID)
```

Run the script locally:

```powershell
./ci/azure-devops/testshield-run.ps1 `
  -BaseUrl $env:TESTSHIELD_BASE_URL `
  -ProjectId $env:TESTSHIELD_PROJECT_ID
```

Optional token from the environment:

```powershell
$env:TESTSHIELD_TOKEN = "<set locally>"
./ci/azure-devops/testshield-run.ps1 -BaseUrl $env:TESTSHIELD_BASE_URL -ProjectId $env:TESTSHIELD_PROJECT_ID
```

## Architecture

```text
TestShield API
      |
CI integration contract (existing /tests/run JSON)
      |
Azure DevOps adapter (this folder)
      |
Azure DevOps pipeline
```

Future Jenkins or other CI adapters should consume the same TestShield API contract. They are not implemented in this slice.
