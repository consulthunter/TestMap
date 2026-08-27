# Configuration

TestMap uses one JSON config file. The generated default lives at
`TestMap/Config/default-config.json`.

The top-level sections are:

- `RuntimeConfig`
- `TestingConfig`
- `AiProviderConfig`
- `ExperimentConfig`

Enum values can be written as kebab-case, PascalCase, or close aliases accepted by the friendly enum
converter. The generated files use kebab-case.

## RuntimeConfig

`RuntimeConfig` controls local paths, Docker image names, and run-level behavior.

Important fields:

- `FilePaths.TargetFilePath`: text file containing repository URLs.
- `FilePaths.LogsDirPath`: root for local logs. All project runs use the fixed UTC relative layout
  described below.
- `FilePaths.TempDirPath`: cloned repositories and temporary work.
- `FilePaths.OutputDirPath`: databases, experiment CSVs, and run artifacts.
- `Project.KeepProjectFiles`: when `false`, `collect-tests` removes each successfully prepared
  cloned workspace from `TempDirPath` after processing; when `true`, the workspace remains available
  for reuse and inspection. Pre-materialization failures are not allowed to delete an unverified or
  busy workspace.
- `Docker.DefaultContext`: Docker context used for Linux containers.
- `Docker.Images.ValidationSdkAll`: image used for build/test/coverage validation.
- `Docker.Images.AgentTools`: image map for agent tool lanes.
- `Frameworks`: test attribute names used to identify test methods.
- `MaxConcurrency`: project-level concurrency.

Measured runs should set `FilePaths.TargetFilePath` to a pinned YAML target manifest. A schema-3
manifest created with `targets create --url` contains one repository and links a content-addressed
resolution record. Configuration loading verifies that record's hash, requested URL, repository,
target identity, and exact commit without contacting GitHub again. Plain URL lists remain available
only to discovery workflows.

### Log Paths

Both pinned-manifest and legacy repository-list workflows allocate one directory per project run:

```text
<LogsDirPath>/YYYY-MM-DD/HH-mm-ss_<owner>-<repository>/
```

Both date and time come from one captured UTC run-start instant. `RunDateFormat` may change the
display or persisted run-date value, but it does not change this filesystem hierarchy. If the same
repository starts more than once in one second, later directories use deterministic `-02`, `-03`,
and subsequent suffixes. Every selected directory retains `.testmap-log-reservation`; existing or
partial directories are never reused or overwritten.

The primary project log is `<project-id>.log` for legacy runs and `run.log` for pinned runs. Child
Docker/test logs are written beside it. Pinned workspace, database, and artifact locations remain
scoped by immutable revision; the exact commit is retained in the manifest, materialized-revision
record, database, and target-execution report rather than encoded into the log directory.

```text
<TempDirPath>/<owner>/<repository>/<resolved-commit>/
<OutputDirPath>/<owner>/<repository>/<resolved-commit>/analysis.db
<OutputDirPath>/<owner>/<repository>/<resolved-commit>/artifacts/
<OutputDirPath>/project-validation.csv
```

`project-validation.csv` is the aggregate validation file for the configured target list. It is
not stored under any individual repository or commit directory. `collect-tests` replaces this file
at the start of an invocation so rows from different target-list runs are not mixed.

## TestingConfig.GenerationConfig

`TestingConfig.GenerationConfig` is the regular LLM generation profile. It is used directly by
`generate-tests`, and experiment mode also uses parts of it as the baseline generation profile.

Recommended Basic Extension defaults:

```json
{
  "Executor": "basic-extension",
  "BudgetMode": "pass-at1-repair-at5",
  "ContextMode": "chained-history",
  "Steps": {
    "EnableEvidencePackage": true,
    "EnableContextGraph": true,
    "EnableContextResolution": true,
    "EnableRoslynValidation": true,
    "EnableSpeculativePlanning": false,
    "EnableFinalTest": true
  }
}
```

The structured Basic Extension path asks the model for one patch-like JSON object, applies it
deterministically, validates it, and repairs it when needed.

### Experimental Step Flags

These decomposed planning steps are useful for ablations, but should usually stay disabled for the
recommended Basic Extension one-shot path:

- `EnableSpeculativePlanning`
- `EnableScenario`
- `EnableMethodName`
- `EnableArrangePlan`
- `EnableInputPlan`
- `EnableActionPlan`
- `EnableAssertionPlan`

They can be enabled when intentionally studying multi-step prompting, but they may turn early model
assumptions into downstream constraints.

## AiProviderConfig

Provider configs define model, endpoint, and credential defaults.

Common environment fallbacks:

- `OpenAi.ApiKey`: `OPENAI_API_KEY`
- `CustomOpenAi.ApiKey`: `CUSTOM_API_KEY`
- `Anthropic.ApiKey`: `ANTHROPIC_API_KEY`, `ANTHROPIC_KEY`
- `GoogleGemini.ApiKey`: `GEMINI_API_KEY`, `GOOGLE_GEMINI_API_KEY`, `GOOGLE_API_KEY`
- `GoogleCloud.ApiKey`: `GOOGLE_CLOUD_API_KEY`
- `GoogleCloud.AccessToken`: `GOOGLE_CLOUD_ACCESS_TOKEN`
- `GoogleCloud.TokenPath`: `GOOGLE_APPLICATION_CREDENTIALS`

Config values win over environment values. If the JSON field is non-empty, TestMap keeps it.

## ExperimentConfig

`ExperimentConfig` controls experiment-mode matrix construction and evaluation lanes.

Core fields:

- `Objective`: usually `test-suite-expansion`.
- `CandidateSelectionStrategy`: target selection strategy.
- `Approaches`: generation approaches to compare.
- `MetricsPaths`: evidence modes, such as `coverage`, `mutation`, or `coverage-and-mutation`.
- `BudgetModes`: `pass-at1`, `pass-at5`, or `pass-at1-repair-at5`.
- `ContextModes`: conversation history modes.
- `CandidateLimit`: number of candidate methods.
- `ExperimentSeriesId`: groups separately invoked runs into one logical experiment and becomes the
  default resume group when `Resume.ResumeRunId` is not set.
- `CandidateCohort`: freezes a randomly sampled candidate set for reuse by later runs.
- `OutputPath`: CSV path or output directory.
- `Evaluation.TestMap.Enabled`: built-in LLM lane.
- `Evaluation.Tools.Enabled`: Docker agent-tool lanes.
- `Tools`: per-tool configuration.

Experiment mode uses `ExperimentConfig` for matrix dimensions, provider inclusion, candidate limit,
tool selection, and output. It uses `TestingConfig.GenerationConfig` for the generation profile
details that are not matrix dimensions, including step toggles, executor behavior, target-selection
defaults, and acceptance policy.

### Candidate Cohorts And Chained Runs

Use a candidate cohort when an experiment must be executed in separate CLI invocations, such as
when only one custom model can be hosted at a time.

First run:

```json
{
  "ExperimentSeriesId": "custom-model-study-2026-07",
  "CandidateLimit": 10,
  "CandidateCohort": {
    "Id": "custom-model-study-cohort-a",
    "Mode": "create",
    "Randomize": true
  }
}
```

`create` selects eligible candidates, randomly orders them after context and eligibility checks,
takes `CandidateLimit`, and stores an immutable snapshot. When `RandomSeed` is omitted, TestMap
generates one and stores it in the experiment configuration and cohort record. Set `RandomSeed`
explicitly when you need a predetermined sample.

Later runs:

```json
{
  "ExperimentSeriesId": "custom-model-study-2026-07",
  "CandidateLimit": 10,
  "CandidateCohort": {
    "Id": "custom-model-study-cohort-a",
    "Mode": "reuse"
  }
}
```

`reuse` loads the exact stored candidate snapshots in their original order. Do not specify
`RandomSeed` in reuse mode. TestMap rejects reuse when the repository, commit, objective, selection
strategy, context mode, candidate limit, or coverage thresholds differ. Provider, model, budget,
evaluation-lane, and tool settings may differ because they are the dimensions being compared.

Modes:

- `disabled`: existing behavior; select candidates independently for this invocation.
- `create`: create a new immutable cohort; fail if that ID already exists for the project.
- `reuse`: require and load an existing cohort; never silently choose replacement candidates.

Candidate cohorts are stored in the project analysis database. Keep the same analysis database
between chained runs. A stable source identity (file, containing type, method, and content hash) is
stored so TestMap can resolve a changed database member ID after re-ingestion of the same commit.

## Provider And Model Overrides

Regular LLM generation:

- `TestingConfig.GenerationConfig.Provider` selects the default provider.
- `AiProviderConfig.<Provider>.Model` supplies the default model.
- Experiment mode can restrict providers with `ExperimentConfig.IncludeProviders`.
- `ExperimentConfig.PreferredProvider` affects ordering when more than one provider is usable.

Tool generation:

- `ExperimentConfig.Tools[].Provider` overrides the generation provider for that tool.
- `ExperimentConfig.Tools[].Model` overrides the provider model for that tool.
- `ExperimentConfig.Tools[].Environment` is applied last and can override native tool env vars.

Model mapping by tool:

| Tool | Config field | Container env |
|---|---|---|
| `codex` | `Tools[].Model` | `CODEX_MODEL` |
| `claude` | `Tools[].Model` | `CLAUDE_MODEL` |
| `gemini` | `Tools[].Model` | `GEMINI_MODEL` |
| `aider` | `Tools[].Model` | `AIDER_MODEL` |
| `mini-swe-agent` | `Tools[].Model` | `MINI_MODEL` |
| `openhands` | `Tools[].Model` | `LLM_MODEL` |
| `copilot` | `Tools[].Model` | `COPILOT_MODEL` (passed as `--model`) |

For `aider`, `mini-swe-agent`, and `openhands`, TestMap prefixes model names when needed:

```text
Provider=Anthropic + Model=claude-sonnet-4-6
=> anthropic/claude-sonnet-4-6
```

For `copilot`, the model id must come from the Copilot CLI's own catalog (`gpt-5.2`,
`gpt-5.3-codex`, `claude-sonnet-4.6`, `claude-haiku-4.5`, ...) and is never provider-prefixed. The
runner passes it as `--model`, which outranks `COPILOT_MODEL`, the settings file, and the CLI
default. When the attempt's telemetry reports the model that actually served the request, that
observed value is what TestMap records on the attempt, so a silent server-side substitution shows up
in results instead of being masked by the configured value.

### Copilot With Your Own Provider (BYOK)

Copilot CLI can talk to a non-GitHub endpoint, so it can be pointed at the same custom or local
model as `mini-swe-agent`. TestMap enables this whenever the tool resolves a base URL, from
`Tools[].Endpoint` or from a `CustomOpenAi`/`Ollama` provider block:

| Resolved value | Container env |
|---|---|
| `Tools[].Model` | `COPILOT_MODEL` (passed as `--model`) |
| `Tools[].Endpoint` or provider endpoint | `COPILOT_PROVIDER_BASE_URL` |
| Provider id | `COPILOT_PROVIDER_TYPE` (`anthropic`, otherwise `openai`) |
| Provider API key | `COPILOT_PROVIDER_API_KEY` |

```json
{
  "Id": "copilot",
  "ImageKey": "copilot",
  "Provider": "CustomOpenAi",
  "Model": "gpt-oss-120b",
  "Endpoint": "https://llm-api.example.edu/api/",
  "RequiredEnvironmentVariables": ["GITHUB_COPILOT_TOKEN", "CUSTOM_API_KEY"]
}
```

Notes:

- With no endpoint resolved, none of the `COPILOT_PROVIDER_*` variables are set and Copilot stays on
  GitHub-hosted models.
- The model is mandatory in BYOK mode; Copilot has no default for a provider it does not host, and
  the runner fails the attempt rather than letting the CLI error mid-run.
- The API key is optional so unauthenticated local servers (Ollama, vLLM) work.
- Azure OpenAI is not a TestMap provider. Reach it by setting `COPILOT_PROVIDER_TYPE` to `azure` in
  the tool's `Environment` block, with the deployment URL as the endpoint.
- BYOK models must support tool calling and streaming; Copilot errors out otherwise.
- `GITHUB_COPILOT_TOKEN` is still required: it authenticates the CLI itself, not the model calls.
- Endpoints are resolved inside the container, so a host-local server needs
  `http://host.docker.internal:<port>` rather than `localhost`.

Native environment overrides win:

```json
{
  "Id": "aider",
  "Model": "claude-sonnet-4-6",
  "Environment": {
    "AIDER_MODEL": "anthropic/custom-model"
  }
}
```

## Tool Secrets

Use `RequiredEnvironmentVariables` to fail early when a tool secret is missing.

Example:

```json
{
  "Id": "copilot",
  "ImageKey": "copilot",
  "Provider": "OpenAi",
  "Model": "gpt-5.2",
  "RequiredEnvironmentVariables": ["GITHUB_COPILOT_TOKEN"]
}
```

Secrets are passed to containers but are not persisted in attempt metadata. Tool attempts write
diagnostic files such as `runner-env.txt` that indicate whether a secret was set without printing the
secret value.

## Experimental Options To Keep Off By Default

Use these deliberately:

- `ExperimentConfig.StepAblation.Enabled`: multiplies matrix size quickly.
- `ExperimentConfig.CompareHistoryModes`: useful for studies, noisy for routine runs.
- Multi-step speculative planning flags: useful for prompt research, less reliable than one-shot
  Basic Extension.
- Agent tool lanes: useful for comparison, but require Docker images and tool-specific credentials.
- Large `CandidateLimit` values: expensive and harder to inspect.
- Candidate cohort `create`/`reuse`: use for controlled multi-run studies; leave disabled for normal
  one-off runs.

Start with `CandidateLimit: 1`, one provider, one budget mode, and either the TestMap lane or one
tool lane.
# Pinned Experiment Input

For `experiment`, set `RuntimeConfig.FilePaths.TargetFilePath` to a schema v1 YAML manifest. A plain
URL file is rejected in measured mode. `AnalyzeLatestCommit` does not override a manifest commit.

Revision-scoped locations are derived as:

```text
<TempDirPath>/<owner>/<repository>/<resolved-commit>/
<OutputDirPath>/<owner>/<repository>/<resolved-commit>/analysis.db
<OutputDirPath>/<owner>/<repository>/<resolved-commit>/artifacts/
<OutputDirPath>/project-validation.csv
<LogsDirPath>/YYYY-MM-DD/HH-mm-ss_<owner>-<repository>/run.log
```

The checked-in `TestMap/Config/pinned-target-experiment.example.json` points at a two-target smoke
manifest. Replace its provider/evaluation settings for the intended lane matrix; do not edit a
manifest already used by a measured run.
