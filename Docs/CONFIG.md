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
- `FilePaths.LogsDirPath`: local logs.
- `FilePaths.TempDirPath`: cloned repositories and temporary work.
- `FilePaths.OutputDirPath`: databases, experiment CSVs, and run artifacts.
- `Docker.DefaultContext`: Docker context used for Linux containers.
- `Docker.Images.ValidationSdkAll`: image used for build/test/coverage validation.
- `Docker.Images.AgentTools`: image map for agent tool lanes.
- `Frameworks`: test attribute names used to identify test methods.
- `MaxConcurrency`: project-level concurrency.

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
- `OutputPath`: CSV path or output directory.
- `Evaluation.TestMap.Enabled`: built-in LLM lane.
- `Evaluation.Tools.Enabled`: Docker agent-tool lanes.
- `Tools`: per-tool configuration.

Experiment mode uses `ExperimentConfig` for matrix dimensions, provider inclusion, candidate limit,
tool selection, and output. It uses `TestingConfig.GenerationConfig` for the generation profile
details that are not matrix dimensions, including step toggles, executor behavior, target-selection
defaults, and acceptance policy.

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
| `copilot` | metadata only unless a native model flag is added |

For `aider`, `mini-swe-agent`, and `openhands`, TestMap prefixes model names when needed:

```text
Provider=Anthropic + Model=claude-sonnet-4-6
=> anthropic/claude-sonnet-4-6
```

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
  "Model": "github-copilot",
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

Start with `CandidateLimit: 1`, one provider, one budget mode, and either the TestMap lane or one
tool lane.
