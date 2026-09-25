# TestShield AI

ASP.NET Core 8 API that ingests OpenAPI specs, generates deterministic contract tests, executes them, validates responses, and detects regressions. The result is a deterministic **Safe / Review / Block** decision. AI scenario generation and Ask TestShield AI are optional enrichment/explanation and never decide pass/fail.

**Team handover (current implemented state):** [`docs/project-overview.md`](docs/project-overview.md)

The Angular dashboard in `src/TestShieldAI.Web` displays those API results. It does not recalculate decisions, risk, or coverage. See `src/TestShieldAI.Web/README.md` to run it.

Azure DevOps adapter: `ci/azure-devops/README.md`.

## Current environment

- Live Azure OpenAI / OpenAI access is **not configured**. `Ai:Enabled` is `false` and `Ai:Provider` is `None`.
- AI scenario counts can therefore be **zero**. Deterministic tests, coverage, regression, and Ask fallback still work.
- Do **not** claim live LLM responses are working in this environment.
- Power Automate notifications are **disabled** unless `Notifications:Enabled` is turned on and a trigger URL is set locally.

## Local AI configuration

AI is **disabled by default**. Leave `Ai:Enabled` as `false` and `Ai:Provider` as `None` unless you are exercising the Gate 2 adapters with a local key.

Do **not** put API keys in `appsettings.json`, source control, or logs.

### appsettings.json (no secrets)

```json
"Ai": {
  "Enabled": true,
  "Provider": "AzureOpenAI",
  "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
  "ModelOrDeployment": "YOUR_DEPLOYMENT_OR_MODEL",
  "MaxScenariosPerOperation": 6,
  "TimeoutSeconds": 30
}
```

`Provider` may be `None`, `AzureOpenAI`, or `OpenAI`. For the public OpenAI service, `Endpoint` may be left empty; `ModelOrDeployment` is the model name.

### API key

Set the key locally via environment variables:

```text
AI_PROVIDER_API_KEY=<set locally>
```

Provider-specific alternatives:

```text
AZURE_OPENAI_API_KEY=<set locally>
OPENAI_API_KEY=<set locally>
```

Or ASP.NET Core user secrets from `src/TestShieldAI.Api`:

```bash
dotnet user-secrets set "Ai:ApiKey" "<set locally>"
```

`AI_PROVIDER_API_KEY` wins over provider-specific variables, which win over `Ai:ApiKey` from user secrets.

When AI is disabled, the provider is `None`, or configuration is unknown, TestShield AI uses a no-op completion client and the deterministic flow continues (Gate 1 core plus Gate 2 coverage, risk, CI, notifications adapter, and Ask fallback).

### Power Automate trigger URL (optional)

Notifications POST `testshield.run.completed` JSON to a Power Automate HTTP trigger. Teams/Email are sent by that flow, not by TestShield. Set locally only:

```text
POWER_AUTOMATE_HTTP_TRIGGER_URL=<set locally>
```

Or user secret `Notifications:HttpTriggerUrl`. Never commit the URL.
