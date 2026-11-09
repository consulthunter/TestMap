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
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --file .\TestMap\Data\pinned-targets-smoke.yaml
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests --config .\TestMap\Config\default-config.json
```

> `check-projects` reads a pinned target YAML manifest and inspects each exact commit. It writes a
> stable bundle pointer, a complete YAML observation report, and reusable YAML manifests for tests
> detected and no tests detected. A missing or invalid `GITHUB_TOKEN`, unavailable commit, rate
> limit, or truncated tree remains indeterminate and causes exit code `2`; it is not evidence that
> tests are absent. See [Setup — Secrets](SETUP.md#secrets) for token scope details.

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

## Chain Runs Across Separately Hosted Models

Use a candidate cohort when only one custom model endpoint can be active at a time. The first run
creates and freezes a random candidate sample. Later runs reuse that sample while changing the model
or evaluation lane.

Ready-to-run paired examples are available at
`TestMap/Config/candidate-cohort-testmap-custom-model.json` and
`TestMap/Config/candidate-cohort-mini-swe-custom-model.json`.

In the first model's config:

```json
{
  "ExperimentConfig": {
    "ExperimentSeriesId": "hosted-model-comparison-2026-07",
    "CandidateLimit": 5,
    "CandidateCohort": {
      "Id": "hosted-model-comparison-cohort-a",
      "Mode": "create",
      "Randomize": true
    },
    "IncludeProviders": ["CustomOpenAi"]
  }
}
```

Run that config normally. For the second and later models, keep the series ID, cohort ID, candidate
limit, selection settings, coverage thresholds, repository, and commit unchanged. Switch only the
cohort mode and the model/lane settings:

```json
{
  "ExperimentConfig": {
    "ExperimentSeriesId": "hosted-model-comparison-2026-07",
    "CandidateLimit": 5,
    "CandidateCohort": {
      "Id": "hosted-model-comparison-cohort-a",
      "Mode": "reuse"
    },
    "IncludeProviders": ["CustomOpenAi"]
  }
}
```

Each invocation remains a separate execution run with its own attempts and results. The runs share
the immutable candidate cohort and experiment series. The series ID also acts as the resume group
unless `Resume.ResumeRunId` explicitly overrides it, so rerunning the same model configuration can
skip completed work while a different model still receives its own work items.

The project must keep using the same `analysis.db`. Reuse fails rather than silently selecting new
candidates when the cohort is missing or incompatible. Each chained execution still performs its
normal baseline and post-attempt measurement workflow.

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

- experiment series and candidate cohort identifiers for joining chained runs
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

## Finding Project Logs

All project runs use readable UTC directories beneath the configured log root:

```text
Logs/YYYY-MM-DD/HH-mm-ss_owner-repository/<project-id>.log
```

For example, a run of `powershell/platyps` started at 14:05:09 UTC on 16 July 2026 appears under
`Logs/2026-07-16/14-05-09_powershell-platyps/`. A second run in the same second uses `-02`, then
`-03`, without overwriting the first. `.testmap-log-reservation` records ownership of the directory;
leave it in place, including in a partial directory created by a failed run.

Pinned-target runs use `run.log` in that directory; legacy runs retain `<project-id>.log`. Docker and
test-run logs are written beside the primary log. Pinned workspaces, databases, and artifacts remain
commit-scoped, while the exact revision also remains recorded in the target manifest and execution
report.

## Practical Advice

- Keep `CandidateLimit` low until the config is proven.
- Run one provider and one budget mode before expanding the matrix.
- Keep `StepAblation.Enabled` off unless you are studying prompt-step effects.
- Keep speculative planning off for Basic Extension unless you deliberately want the older decomposed
  generation flow.
- Inspect failed attempts in the database, not just the CSV.
# Reproducible Target Manifests

The import file may be comma- or tab-delimited and needs only `name` and `lastCommitSHA`. `name` is
the GitHub `owner/repository`; `lastCommitSHA` must be the full 40-character commit. Other columns are
ignored. CSV quoting, UTF-8 BOMs, duplicate records, and rejected logical records are handled
explicitly.

For a single GitHub repository, resolve and pin its current default-branch head without creating an
intermediate CSV:

```powershell
dotnet run --project TestMap -- targets create `
  --url https://github.com/powershell/platyps `
  --output TestMap/Data/platyps-targets.yaml
```

URL mode accepts one repository URL only. It queries repository metadata, the named default branch,
the exact Git commit object, and confirmation metadata. If metadata changes during the observation,
the complete sequence is retried once. The command publishes a content-addressed resolution record
before a one-target schema-3 manifest. `GITHUB_TOKEN` is optional for public repositories; only the
access mode is recorded. Failures retain a sanitized categorized resolution record and leave any
existing manifest unchanged.

```powershell
dotnet run --project TestMap -- targets create `
  --input Replication/LicenseFilteredRepo/licenseFilteredRepoList.csv `
  --output TestMap/Data/evaluation-targets.yaml `
  --delimiter auto

dotnet run --project TestMap -- targets verify `
  --file TestMap/Data/evaluation-targets.yaml `
  --max-concurrency 4
```

Creation publishes a content-addressed rejection CSV first and the manifest last. Verification
always writes one status row per target in manifest order. Unavailable repositories, authentication
failures, rate limits, and missing commits remain part of the sampling-frame accounting.

Schema-3 consumers hash and validate the linked resolution record locally. They use its exact commit
and never re-resolve the default branch, so an existing target does not move when the branch moves.

During a run, each target gets one terminal row in `target-execution-<manifest-hash>.csv`, including
failures that happen before a database exists.
