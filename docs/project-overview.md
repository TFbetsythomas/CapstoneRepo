# TestShield AI — Team handover

Use this file as the current project context for a new team member. It describes the **implemented** product, not a proposal. Do not treat it as application code.

Related runbooks:

- Root `README.md` — local AI key configuration (no secrets in source)
- `src/TestShieldAI.Web/README.md` — Angular dashboard
- `ci/azure-devops/README.md` — Azure DevOps quality-gate adapter

## 1. Product overview

**TestShield AI** is an API regression and contract protection tool.

It ingests OpenAPI/Swagger specifications, generates and executes HTTP tests against a configured base URL, validates responses against the contract, compares the run to a stored baseline, and reports a deterministic **Safe / Review / Block** decision plus risk and coverage.

AI is an optional **enrichment and explanation** layer. It may propose extra scenarios and explain stored evidence. It must never decide pass/fail, contract validity, regression, or Safe / Review / Block.

### Problem

API changes often break consumers without a clear, repeatable check. Teams may already have an OpenAPI/Swagger spec, but they lack a simple way to generate tests from that contract, run them against a live base URL, and see whether responses still match the spec and the last known-good run.

### Goal

Protect an existing API before release: import the contract, run a deterministic suite, detect regressions against a baseline, and surface the result in CI, the dashboard, optional notifications, and Ask TestShield AI.

### Target users

- Backend and API developers protecting a service before release
- QA engineers running contract and regression checks
- Reviewers who need a demoable .NET 8 + Angular flow

## 2. Approved architecture and technology stack

| Area | Choice |
|---|---|
| Backend | .NET 8 (ASP.NET Core Web API) |
| Engine | `TestShieldAI.Engine` class library |
| Frontend | Angular 19 SPA (`src/TestShieldAI.Web`) |
| API standard | Swagger / OpenAPI |
| Database | SQLite (EF Core) |
| AI | Optional Azure OpenAI / OpenAI adapter (`None` = no-op) |
| CI/CD | Azure DevOps adapter under `ci/azure-devops/` |
| Notifications | Power Automate HTTP trigger (Teams/Email in the flow, not in TestShield) |
| Development | Cursor |

Solution file: `TestShieldAI.sln` at the repo root.

### Repository layout

```text
TestShieldAI.sln
├── src/TestShieldAI.Api          ASP.NET Core host, persistence, endpoints, AI adapters, insights, notifications
├── src/TestShieldAI.Engine       Deterministic ingest, generation, execution, validation, regression, coverage, risk
├── src/TestShieldAI.DemoApi      Local Pets demo target (not part of the TestShield product)
├── src/TestShieldAI.Ci           CI helper library (exit-code mapping, run-report parse/format, secret redaction)
├── src/TestShieldAI.Web          Angular dashboard
├── tests/TestShieldAI.Engine.Tests
├── tests/TestShieldAI.DemoApi.Tests
└── ci/azure-devops/              YAML template + PowerShell runner
```

### Runtime ports (local)

| Process | Default |
|---|---|
| TestShield API | `http://localhost:5126` |
| Angular dashboard | `http://localhost:4200` (proxies `/api` to 5126) |
| Demo Pets API | `http://localhost:5180` |

### Source of truth

| Concern | Owner |
|---|---|
| Pass/fail of a test | `ApiContractValidator` |
| Regression and Safe / Review / Block | `RegressionDetector` |
| Coverage % and gaps | `CoverageCalculator` (deterministic tests only) |
| Risk level / title / summary | `RiskSummaryBuilder` (from the detector decision) |
| Dashboard | Displays API JSON only; does not recalculate decision, risk, or coverage |
| Azure DevOps | Calls `POST /api/projects/{projectId}/tests/run` and maps **Block** → exit `1` |
| Ask TestShield AI | Explains stored evidence; does not change the run |

## 3. Gate 1 completed scope

Gate 1 is implemented and remains the deterministic core:

- ASP.NET Core 8 Web API + Angular SPA + SQLite
- Import OpenAPI 3 / Swagger and list operations
- Deterministic test generation (happy path per operation; a negative missing-required-body case where a request body applies)
- HTTP execution against the project base URL, with timeouts
- Contract validation: documented status codes and response body vs JSON Schema
- Persist the last run; compare a later run to the last passing baseline
- JSON report as source of truth; Angular on-screen report
- TestShield’s own API documented with Swagger
- Engine unit tests for ingest, generation, validation, and regression
- Optional AI adapter behind configuration; no-op when AI is disabled

Gate 1 explicitly did **not** include Azure DevOps as a quality gate. That landed in Gate 2.

## 4. Gate 2 implemented capabilities

### AI scenario enrichment

When `Ai:Enabled` is true and `Ai:Provider` is `AzureOpenAI` or `OpenAI`, generate/run may call `IAiScenarioGenerator` (`LlmAiScenarioGenerator`). Proposed scenarios are **normalized** by `AiScenarioNormalizer` so they must match an imported operation. Accepted kinds are `AiPositive`, `AiNegative`, and `AiEdge`.

Normalization limits (implemented): at most 6 scenarios per operation and 2 per type. Rejected proposals become warnings, not test failures.

If AI is disabled, the provider is `None`, the key/endpoint is missing, or completion fails, TestShield uses `NoOpAiCompletionClient` / no-op generation. The deterministic suite still runs.

AI scenario counts on the coverage payload (`aiCoveredOperations`, `scenarios.aiPositive` / `aiNegative` / `aiEdge`) are informational. They do **not** change coverage percent or gaps.

When AI generation is unavailable, regression comparison uses **deterministic** snapshots only so missing AI tests cannot produce a false `TestRemoved` regression.

### Multiple OpenAPI specifications

A project can import more than one specification. Each import is stored. Generate and run select the **latest specification per `SpecKey`** (`OpenApiSpecKey` from the OpenAPI title). Operations, tests, coverage gaps, and regression snapshots are keyed by spec + method + path so two APIs can share a path without colliding.

### Deterministic coverage

`CoverageCalculator` treats an operation as covered only when it has a deterministic test (`HappyPath` or `NegativeMissingRequiredBody`). Gaps use reason `NO_DETERMINISTIC_TEST`. Coverage percent is `covered / total` operations, rounded to two decimals. AI tests are counted separately and never close a gap.

### Risk assessment

`RiskSummaryBuilder` maps the detector decision:

| Decision | Risk level | Title |
|---|---|---|
| Safe | Low | Low risk |
| Review | Medium | Review recommended |
| Block | High | Release blocked |

Reasons come from findings (and coverage below 100% when present). Recommendations are fixed copy for Review and Block. Risk does not invent a second decision.

### Azure DevOps CI integration

`ci/azure-devops/` is an **adapter only** (YAML template + `testshield-run.ps1`). `TestShieldAI.Ci` holds the matching exit-code and report-parse helpers used in tests. The adapter calls the existing run endpoint, prints decision / risk / coverage / findings / counts, and maps:

- **Safe** → exit `0` (pipeline continues)
- **Review** → exit `0` (pipeline continues)
- **Block** → exit `1` (pipeline fails)

HTTP or JSON failures also exit `1`. Review does not fail the pipeline. See `ci/azure-devops/README.md`.

### Angular dashboard

Routes: `/projects` (create/select) and `/projects/:projectId` (dashboard).

The dashboard shows the project, OpenAPI import, Generate Tests, Run Tests, Decision, Risk, Coverage (including AI scenario counts as display-only), Findings, generated tests or execution results, and an AI run summary when the last run included one.

It does not recalculate Safe / Review / Block, risk, or coverage.

### Demo API

`src/TestShieldAI.DemoApi` is a local Pets API used as a TestShield **target**. It is not part of the TestShield product. Swagger is at `http://localhost:5180/swagger`. Implemented operations: `GET /pets`, `GET /pets/{id}`, `POST /pets`. `GET /pets` currently returns HTTP 200 with the in-memory list.

### Power Automate notifications (Teams and Email)

TestShield does **not** call Teams or Email APIs directly.

After a successful run, if `Notifications:Enabled` is true and `NotificationPolicy` allows the decision, TestShield POSTs a `testshield.run.completed` JSON event to a Power Automate HTTP trigger URL. The consumer’s Power Automate flow can then send Teams and Email.

Defaults in `appsettings.json`: notifications **disabled**; notify on Review and Block; do not notify on Safe; max 5 findings; 10s timeout. The trigger URL is **not** stored in source. Use `POWER_AUTOMATE_HTTP_TRIGGER_URL` or user secret `Notifications:HttpTriggerUrl`.

Notification failure is best-effort: it must not fail the TestShield run.

### Ask TestShield AI / RAG architecture

Ask is explanation-only over **stored TestShield evidence**. There is no vector database.

Flow:

1. `POST /api/projects/{projectId}/insights/ask` with `{ "question": "..." }` (max 500 characters)
2. `InsightsIntentRouter` maps the question to an intent (`RunOverview`, `ExplainDecision`, `ExplainCoverage`, `BaselineDiff`, `ContractDrift`, `FailedTests`, `NegativeScenarios`)
3. `InsightsEvidenceRetriever` loads last run, baseline, specs, and generated tests as needed for that intent
4. `LlmInsightsAssistant` may explain that evidence if a live completion client is configured
5. If the LLM is unavailable or returns empty, `DeterministicInsightsAnswerer` produces the answer
6. Response includes `answer`, `answerSource` (`llm` or `deterministic`), optional `warning`, and evidence ids — not a new decision

Ask does not execute tests, does not call the target API, and does not change Decision, Risk, Coverage, Findings, baseline, or RAG stores.

### Deterministic fallback when AI is unavailable

- **Scenarios:** no-op generator; deterministic tests still generate and run
- **Ask:** deterministic answer plus warning `AI insights assistant is not available.`
- **Dashboard:** shows the warning and “Using deterministic evidence because AI is unavailable.” AI unavailability is **not** a test failure

### Floating chat UI

Bottom-right floating **AI** button opens the Ask TestShield AI panel.

- Header and input stay fixed; only the conversation area scrolls
- Auto-scrolls to the latest message only when the user is already near the bottom
- **Close** hides the panel and does **not** clear history
- Reopening shows the in-memory conversation
- **New Chat** clears only the in-memory history (max 10 turns). It does not clear server data
- Empty conversations show suggestion chips that submit through the existing `askInsights()` call
- Enter/send submit a non-empty question; empty input does not call the API
- History is not persisted to `localStorage` or the server

## 5. End-to-end workflow

```text
OpenAPI/Swagger import
  -> Deterministic test generation
  -> Optional AI scenario enrichment (normalized)
  -> HTTP execution against project base URL
  -> Contract validation          ← pass/fail
  -> Regression detection         ← Safe / Review / Block
  -> Risk + deterministic coverage
  -> Persist last run (+ promote baseline when all deterministic tests pass)
  -> Optional Power Automate notification
  -> Dashboard + CI + Ask TestShield AI
```

1. **Create a project** with a name and the target API base URL (`POST /api/projects`).
2. **Import** one or more OpenAPI documents (`POST /api/projects/{id}/specs/import`).
3. **Generate** (optional preview) or **Run**. Run always regenerates, executes, and validates.
4. **Contract validation** decides each test Passed / Failed / Error.
5. **RegressionDetector** compares comparable current snapshots to the stored baseline.
6. **Coverage** and **risk** are computed from generated tests and the detector result.
7. The last run is saved. If every deterministic test passed, that run’s snapshots become the new baseline (deterministic tests plus passing AI tests).
8. CI, notifications, the dashboard, and Ask consume that result. They do not replace the detector.

## 6. AI does not decide

AI must not be described as the quality gate. Implemented split:

| Question | Who answers |
|---|---|
| Did this HTTP response pass the contract? | `ApiContractValidator` |
| Did the API regress vs baseline? | `RegressionDetector` |
| Safe, Review, or Block? | `RegressionDetector.Decide` |
| Coverage % / gaps? | `CoverageCalculator` (deterministic only) |
| Extra scenarios? | Optional `IAiScenarioGenerator` + normalizer |
| Why was this run blocked? | Ask TestShield AI (LLM or deterministic explanation of **stored** evidence) |

The insights assistant prompt is explicit: do not calculate or change Safe / Review / Block; treat `AUTHORITATIVE_DECISION` as a fact; do not invent evidence.

## 7. Baseline and controlled regression E2E

Baseline is the last **all-deterministic-pass** snapshot set stored in SQLite. The first healthy run against the Demo API establishes it and should report **Safe** / Low risk when every current test passed and there are no findings.

Verified demo loop (Demo Pets API + TestShield project whose base URL is `http://localhost:5180`):

| Step | Target API | Expected TestShield result |
|---|---|---|
| 1. Normal run | `GET /pets` returns 200 + JSON list (current Demo API) | **Safe**, Low risk, empty/no blocking findings |
| 2. Intentional breaking change | Temporarily make `GET /pets` fail the contract (for example return 500) and restart the Demo API | **Block**, High risk (`Release blocked`), regression/validation findings on `GET /pets` |
| 3. Restore | Restore `GET /pets` to 200 + list, restart Demo API, run again | **Safe**, Low risk |

Notes:

- The Demo API in source currently implements the healthy `GET /pets` 200 path. The breaking change is a **temporary** demo edit, then revert. Do not leave a 500 in source for the demo.
- Block is produced by `RegressionDetector` because a previously passing validation now fails — not by Ask TestShield AI and not by the Angular UI.
- Azure DevOps would fail the pipeline only on that Block run (exit `1`). Safe and Review continue.

## 8. Enterprise integration

TestShield is designed to sit beside an existing system, not replace it.

| Piece | Role |
|---|---|
| Existing application / API | The system under test. TestShield calls its HTTP endpoints using the imported OpenAPI contract and the project base URL. |
| Existing Azure DevOps pipeline | Consumes `ci/azure-devops/testshield-regression.yml`. Supplies `TESTSHIELD_BASE_URL` and `TESTSHIELD_PROJECT_ID` (optional secret `TESTSHIELD_TOKEN`). |
| TestShield as a CI quality gate | The pipeline does not generate its own pass/fail. It runs TestShield and fails only on **Block**. |
| Teams / Email | Delivered by a Power Automate flow that receives `testshield.run.completed`. TestShield only POSTs that JSON to the HTTP trigger. |
| Jira | **Future.** Not implemented. No Jira client, issue creation, or webhook exists in this repository. |

Jenkins or GitHub Action adapters are not implemented. Future CI tools should call the same `/tests/run` contract.

## 9. Current limitations

State these clearly in demos and handover:

- **Azure OpenAI / OpenAI live access is not configured** in the current environment. `appsettings.json` has `Ai:Enabled: false` and `Ai:Provider: None`.
- Therefore **AI scenario counts can currently be zero** (`aiPositive` / `aiNegative` / `aiEdge` / `aiCoveredOperations`).
- **Deterministic fallback remains available** for generation, runs, and Ask TestShield AI.
- **Do not claim live LLM responses are working** unless a reviewer has configured a provider, endpoint/model, and API key locally (never commit keys).
- Notifications are **disabled by default**. Do not claim Teams/Email fired unless `Notifications:Enabled` is true and a trigger URL is configured.
- Ask history is in-memory only (max 10 turns) and is lost on browser refresh.
- No multi-user auth, Postgres, message bus, Pact, GraphQL, or async API support.
- The Angular dashboard is a per-project view, not a multi-service analytics suite.

## 10. Future enhancements

Approved directions that are **not** implemented:

- Live AI provider configuration in the current environment (Azure OpenAI / OpenAI with a real key)
- Richer multi-service dashboard views
- Jira integration
- Historical analytics, root-cause analysis beyond the last run + baseline, and other CI adapters (Jenkins, GitHub Action)

Do not invent additional product features in this list.

## 11. What is implemented vs future

| Capability | Status |
|---|---|
| OpenAPI 3 / Swagger import | Implemented |
| Multiple specs per project (`SpecKey`) | Implemented |
| Deterministic happy-path + negative body tests | Implemented |
| HTTP execution + contract validation | Implemented |
| Baseline + `RegressionDetector` Safe / Review / Block | Implemented |
| Deterministic coverage % and gaps | Implemented |
| Risk summary from the detector decision | Implemented |
| Optional AI scenario enrichment + normalizer | Implemented (adapter); **live provider not configured here** |
| SQLite last run + baseline persistence | Implemented |
| Angular project list + dashboard | Implemented |
| Floating Ask TestShield AI chat (New Chat, scroll, suggestions) | Implemented |
| Ask evidence retrieval + deterministic fallback | Implemented |
| Live LLM Ask / live LLM scenario generation in this environment | **Not working** (provider disabled / not configured) |
| Demo Pets API | Implemented |
| Azure DevOps `/tests/run` quality-gate adapter | Implemented |
| Power Automate HTTP notification payload | Implemented (disabled by default) |
| Teams/Email message templates inside TestShield | **Not implemented** (belong in the Power Automate flow) |
| Jira | **Future** |
| Multi-user auth / Postgres / message bus | **Not implemented** |
| CLI / GitHub Action / Jenkins adapter | **Not implemented** |
| Historical analytics / RCA product | **Future** |
| Pact / GraphQL / async APIs | Out of scope |

## 12. Demo checklist (5–10 minutes)

Use this as the presenter script. Show only what is implemented. Do **not** claim live LLM answers or AI scenario generation in this environment.

### Setup (before the audience)

These are not demo clicks; without them the run and notification steps fail for the wrong reason.

| Check | How |
|---|---|
| Demo Pets API is running | `dotnet run --project src/TestShieldAI.DemoApi --launch-profile http` → `http://localhost:5180` |
| `GET /pets` is healthy | Returns HTTP 200 and a JSON list (`PetsEndpoints.List` → `Results.Ok`) |
| Demo Pets project exists | Project on the Projects page with base URL `http://localhost:5180` and the Demo OpenAPI already imported (`GET /pets`, `GET /pets/{id}`, `POST /pets`) |
| Block notifications (step 10) | Local only: `Notifications:Enabled` = true, `NotifyOnBlock` = true, and `POWER_AUTOMATE_HTTP_TRIGGER_URL` (or user secret `Notifications:HttpTriggerUrl`) pointing at a flow that sends Teams and Email. Default `appsettings.json` has notifications **off** — if that is unchanged, skip claiming a live Teams/email message and say the HTTP event is what TestShield implements. |

Do not leave a `GET /pets` 500 in source after the demo. Do not commit keys or trigger URLs.

### 1. Start TestShield API and Angular

**Click / run**

```bash
dotnet run --project src/TestShieldAI.Api --launch-profile http
```

```bash
cd src/TestShieldAI.Web
npm start
```

**Audience should see** API on `http://localhost:5126` (Swagger) and the dashboard on `http://localhost:4200`.

**Why it matters** TestShield is a separate quality-gate service plus a display-only UI; the Demo Pets API stays the system under test.

### 2. Open the TestShield AI dashboard

**Click / run** Browser → `http://localhost:4200/` (redirects to `/projects`).

**Audience should see** the **Projects** page: existing projects and a create form. No Safe/Review/Block yet.

**Why it matters** Each TestShield project is one target base URL; the dashboard does not invent results until the API runs.

### 3. Select the Demo Pets API project

**Click / run** Click the Demo Pets project in the list (base URL `http://localhost:5180`).

**Audience should see** `/projects/{id}`: project name, base URL, **Generate Tests**, **Run Tests**, OpenAPI import, empty Decision until a run (“Angular does not calculate this decision”).

**Why it matters** All later generate/run/Ask calls are scoped to this project’s stored specs and last run.

### 4. Show the imported OpenAPI operations

**Click / run** If the import success line is not on screen, paste `http://localhost:5180/swagger/v1/swagger.json` into **OpenAPI import** and click **Import specification**. Optionally keep Demo Swagger open to show the same three operations.

**Audience should see** `Imported 3 operation(s).` (or the same count). Name them: `GET /pets`, `GET /pets/{id}`, `POST /pets`. The durable on-screen list of method/path is the **Generated tests** table in the next step.

**Why it matters** Tests come from the contract, not from hand-written cases in Angular.

### 5. Generate deterministic API tests

**Click / run** **Generate Tests**. Wait until Generating… finishes.

**Audience should see**

- Note: tests were generated and have **not** been executed; use Run Tests for Safe / Review / Block.
- **Generated tests** table: Kind, Method, Path, expected status, SpecKey.
- **Coverage** card (percent, totals, scenario counts, gaps).
- Decision stays unset (no Safe yet).

**Why it matters** Generation is preview only; pass/fail and the quality gate happen only on Run.

### 6. Explain HappyPath, negative body, coverage, and AI counts

**Click / run** Stay on the generate result. Point at **Generated tests** and **Coverage → Scenario counts**.

**Audience should see**

| Kind | What it is on Demo Pets |
|---|---|
| `HappyPath` | One valid request per operation (`GET /pets`, `GET /pets/{id}`, `POST /pets` with name + species) |
| `NegativeMissingRequiredBody` | `POST /pets` with a required body field removed; GETs do not get this kind |
| Coverage % / gaps | Deterministic only. Happy path covers each operation, so gaps should be none / 100% if all three operations imported |
| AI Positive / Negative / Edge and AI covered operations | **Zero** in this environment |

Also say: `Ai:Enabled` is false / `Provider` is `None`; live Azure OpenAI/OpenAI is not configured. AI counts are display-only and **do not** change coverage percent.

**Why it matters** The suite stays useful when AI is down; AI is enrichment, not the gate.

### 7. Run tests — passed, coverage, Safe, Low risk

**Click / run** **Run Tests**. Confirm Demo API is still the healthy 200 `GET /pets`.

**Audience should see**

- **Test results**: expected vs actual, Matched, Validation **Passed** (open **Details** on one row if asked).
- **Coverage**: same deterministic story; AI counts still zero.
- **Decision**: **Safe** (green).
- **Risk**: **Low** — “Low risk”; summary that all current tests passed and no regression/drift was detected.
- **Findings**: “No regression or contract drift findings.”
- **AI summary**: “AI scenario generation is currently disabled.”

This run establishes or refreshes the baseline because all deterministic tests passed.

**Why it matters** `RegressionDetector` is the source of Safe; Angular only displays it. This is the known-good baseline for the next break.

### 8. Ask TestShield AI

**Click / run** Bottom-right floating **AI** button. On the empty chat, click each suggestion (or type and send):

1. **What tests failed?**
2. **What negative scenarios exist?**
3. **What changed from baseline?**

**Audience should see** the panel with fixed header/input and a scrollable transcript. Each turn shows the answer, **Source: deterministic**, warning that the insights assistant is not available, and “Using deterministic evidence because AI is unavailable.” Failed-tests answer should match the green run (nothing failed). Negative-scenarios answer should mention the generated missing-required-body case. Baseline-diff should not invent a regression. Decision/Risk/Coverage cards **do not change**. Close keeps history; **New Chat** clears only in-memory chat.

**Why it matters** Ask explains stored evidence; when live AI is unavailable, deterministic fallback still answers. It never decides pass/fail or Safe/Review/Block.

### 9. Controlled regression — `GET /pets` returns 500

**Click / run** Temporarily change Demo `PetsEndpoints.List` so `GET /pets` returns HTTP **500**. Restart the Demo API. Back on the dashboard, click **Run Tests**. Do not commit that 500.

**Audience should see**

- **Test results**: the previously passing `GET /pets` HappyPath now **Failed** (expected 200, actual 500).
- **Findings**: regression on `GET /pets` (previously passing validation now fails).
- **Decision**: **Block**.
- **Risk**: **High** — “Release blocked.”

**Why it matters** A real consumer-breaking change is caught against the baseline by the engine, not by the chat UI.

### 10. Power Automate Teams / email for Block

**Click / run** No extra TestShield button. After the Block run, open the Teams channel and mailbox the Power Automate flow is wired to. If notifications were not enabled for this session, say that and do not invent a TestShield notification screen.

**Audience should see** a Teams and/or Email message driven by the `testshield.run.completed` JSON (decision Block, high risk, findings, coverage, result counts). TestShield POSTed that payload to the HTTP trigger; it does not call Teams or Email APIs itself. A notification failure would not have changed the Block result on the dashboard.

**Why it matters** The same detector decision can alert humans without replacing CI or the dashboard as source of truth.

### 11. Restore `GET /pets`

**Click / run** Revert `List` to `Results.Ok(store.List())`. Restart the Demo API. Confirm `GET /pets` is 200 again.

**Audience should see** Demo Swagger/try-it (or a quick browser/curl) succeeding. Dashboard still shows Block until the next run.

**Why it matters** The quality gate tracks the live API, not a one-off chat answer.

### 12. Run again — Safe / Low risk

**Click / run** **Run Tests**.

**Audience should see** `GET /pets` Passed again, **Decision Safe**, **Risk Low**, findings empty. Same coverage story as the healthy run.

**Why it matters** Fixing the API clears Block on the next TestShield run; the baseline/quality gate is repeatable.

### 13. Enterprise integration (30–45 seconds)

**Click / run** Speak only; optional: point at `ci/azure-devops/README.md`. Do not open Jira.

**Audience should see** this picture:

```text
Existing application/API
  → existing Azure DevOps pipeline
    → TestShield POST /api/projects/{id}/tests/run
      → Safe / Review  → pipeline continues (exit 0)
      → Block          → pipeline fails (exit 1)
    → optional Power Automate → Teams / Email
```

TestShield is the quality gate inside a pipeline the team already owns. Review does **not** fail the pipeline; Block does.

**Why it matters** Adoption is an adapter on an existing API and pipeline, not a new platform rewrite.

### 14. Implemented now vs future (20–30 seconds)

**Click / run** One slide’s worth of talk; details are in the table in section 11.

**Audience should see / hear**

| Now | Not now |
|---|---|
| Deterministic generate/run, coverage, Safe/Review/Block, risk, Demo Pets, Angular dashboard, Ask fallback, Azure DevOps adapter, Power Automate HTTP event | Live LLM in this environment, AI scenario counts above zero here, Jira, richer multi-service analytics, historical RCA, GitHub Action/Jenkins adapters |

**Why it matters** Reviewers should not leave thinking Jira or live Azure OpenAI is already wired.

**If asked why AI counts are zero:** provider is not configured; deterministic tests and Ask fallback still work. Do not claim live LLM responses.

## Local commands (handover)

```bash
dotnet test TestShieldAI.sln
dotnet run --project src/TestShieldAI.Api --launch-profile http
dotnet run --project src/TestShieldAI.DemoApi --launch-profile http
```

```bash
cd src/TestShieldAI.Web
npm start
npx ng test --watch=false --browsers=ChromeHeadless
npx ng build
```

Do not put API keys, Power Automate trigger URLs, or `TESTSHIELD_TOKEN` in source control.
