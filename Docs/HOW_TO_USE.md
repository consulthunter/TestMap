# How To Use

This guide describes the normal workflows: collecting project data, running built-in generation, and
running tool-evaluation experiments.

## Prepare A Config

Generate the default config:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- setup
```

Then edit:

- `RuntimeConfig.FilePaths.TargetFilePath`
- `RuntimeConfig.FilePaths.TempDirPath`
- `RuntimeConfig.FilePaths.OutputDirPath`
- provider settings under `AiProviderConfig`
- generation settings under `TestingConfig.GenerationConfig`
- experiment settings under `ExperimentConfig`

See [Configuration](CONFIG.md) for field-level guidance.

## Collect Project Data

For a new repository, run discovery and analysis before generation:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests --config .\TestMap\Config\default-config.json
```

`collect-tests` is the important step before experiments because candidate selection and validation
need stored source, test, coverage, and mutation evidence.

## Run Built-In LLM Generation

> **Experimental:** `generate-tests` runs the built-in LLM pipeline outside the controlled
> experiment harness. Use it for ad hoc generation only; use `experiment` for anything you intend to
> measure or report.

Use `generate-tests` when you want TestMap to select targets from the configured generation profile
and run the built-in LLM pipeline:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- generate-tests --config .\TestMap\Config\default-config.json
```

This uses `TestingConfig.GenerationConfig`.

Recommended default:

- `Executor`: `basic-extension`
- `BudgetMode`: `pass-at1-repair-at5`
- `ContextMode`: `chained-history`
- `EnableSpeculativePlanning`: `false`

## Run An Experiment

Use `experiment` when you want controlled comparison across providers, budget modes, context modes,
or tool lanes:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- experiment --config .\TestMap\Config\default-config.json
```

Start small:

```json
{
  "ExperimentConfig": {
    "CandidateLimit": 1,
    "IncludeProviders": ["CustomOpenAi"],
    "BudgetModes": ["pass-at1"],
    "Evaluation": {
      "TestMap": { "Enabled": true },
      "Tools": { "Enabled": false }
    }
  }
}
```

Then add dimensions one at a time.

## Run Tool Evaluation

Enable tool lanes under `ExperimentConfig.Evaluation.Tools` and list tool definitions under
`ExperimentConfig.Tools`.

Example:

```json
{
  "ExperimentConfig": {
    "Evaluation": {
      "TestMap": { "Enabled": false },
      "Tools": {
        "Enabled": true,
        "ToolIds": ["codex", "gemini"],
        "RequireAvailabilityInSetup": true
      }
    },
    "Tools": [
      {
        "Id": "codex",
        "ImageKey": "codex",
        "Provider": "OpenAi",
        "Model": "gpt-5.1",
        "RequiredEnvironmentVariables": ["OPENAI_API_KEY"]
      },
      {
        "Id": "gemini",
        "ImageKey": "gemini",
        "Provider": "GoogleGemini",
        "Model": "gemini-2.5-pro",
        "RequiredEnvironmentVariables": ["GEMINI_API_KEY"],
        "Environment": {
          "GEMINI_OUTPUT_FORMAT": "stream-json"
        }
      }
    ]
  }
}
```

Tool attempts write artifacts under the output directory. The database stores exact artifact paths,
stdout/stderr paths, JSONL paths, changed-file counts, token usage when available, and post-attempt
measurement.

## Compare LLM And Tool Generation

Enable both lanes:

```json
{
  "Evaluation": {
    "TestMap": { "Enabled": true },
    "Tools": {
      "Enabled": true,
      "ToolIds": ["codex", "gemini"]
    }
  }
}
```

The built-in lane produces one structured patch per attempt. Tool lanes run the external agent in a
workspace and then measure what changed. Both are summarized in the experiment CSV.

## Reading Results

Use the CSV for quick analysis:

- provider and model
- budget mode and attempt number
- generated test name
- compile/run/pass outcome
- coverage before/after/delta
- mutation before/after/delta
- token totals where available
- generation, validation, and total attempt duration

Use the SQLite database for detailed forensic analysis:

- full generation attempts
- patch JSON and repair patch JSON
- modified file snapshots and hashes
- tool stdout/stderr/JSONL log paths
- linked generated test members
- raw validation and diagnostic data

## Practical Advice

- Keep `CandidateLimit` low until the config is proven.
- Run one provider and one budget mode before expanding the matrix.
- Keep `StepAblation.Enabled` off unless you are studying prompt-step effects.
- Keep speculative planning off for Basic Extension unless you deliberately want the older decomposed
  generation flow.
- Inspect failed attempts in the database, not just the CSV.
