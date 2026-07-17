# Setup

This guide gets TestMap from a fresh checkout to a small local experiment.

## Dependencies

Install:

- .NET SDK 10
- Git
- Docker Desktop
- A Docker context that can run Linux containers, usually `desktop-linux`
- API credentials for any LLM providers or tools you plan to run

Optional but useful:

- Stryker.NET or project-specific mutation tooling when collecting mutation data
- A local OpenAI-compatible endpoint if you use `custom-openai`

## First-Time Setup

From the repository root:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- setup
```

This creates or refreshes:

- `TestMap/Config/default-config.json`
- `TestMap/Data/example_project.txt`
- `TestMap/.env`
- Docker image names in the generated config

The setup command does not fill in secrets. Add secrets to `TestMap/.env` or to your process
environment.

## Secrets

`Program` loads `.env` from the working directory and from the config file's parent directories. With
the default layout, `TestMap/.env` is loaded when you pass a config under `TestMap/Config`.

Supported `.env` style:

```text
OPENAI_API_KEY=...
CUSTOM_API_KEY=...
ANTHROPIC_API_KEY=...
GEMINI_API_KEY=...
GOOGLE_API_KEY=...
GITHUB_COPILOT_TOKEN=...
GITHUB_TOKEN=...
```

`GITHUB_TOKEN` is a GitHub personal access token (PAT) used by `targets create --url` to resolve the
current default-branch head and by `check-projects` to inspect the exact commit declared by each
pinned target. Public repositories may be available anonymously within API limits; private targets
require a classic PAT with `repo` or a fine-grained token with Contents: read. Artifacts record only
whether access was authenticated or anonymous. Missing, expired, insufficiently scoped, or
rate-limited credentials produce explicit failure or indeterminate observations, never a no-tests
classification.

Avoid shell syntax in `.env`:

```text
export OPENAI_API_KEY=...
```

The parser expects `NAME=value` lines. Double quotes are trimmed; single quotes are not.

## Target Repositories

For measured workflows, `RuntimeConfig.FilePaths.TargetFilePath` points to a pinned YAML target
manifest. Create a one-repository schema-3 manifest directly from GitHub:

```powershell
dotnet run --project TestMap -- targets create `
  --url https://github.com/consulthunter/TestMap-Example `
  --output TestMap/Data/testmap-example-targets.yaml
```

CSV import remains available for a repository set. Plain URL lists are accepted only by discovery
workflows because they do not pin an immutable revision.

TestMap clones or refreshes repositories under `RuntimeConfig.FilePaths.TempDirPath` and writes each
project database under the configured output directory.

## Log Storage

`RuntimeConfig.FilePaths.LogsDirPath` selects the log root. For a legacy URL-list run, TestMap writes
project logs beneath a readable UTC hierarchy:

```text
Logs/2026-07-16/14-05-09_owner-repository/<project-id>.log
```

The date parent is always `YYYY-MM-DD` and the project directory starts with `HH-mm-ss`. Same-second
collisions add deterministic `-02`, `-03`, and later suffixes. The project ID remains in the log
filename and persisted run identity for legacy runs; it is no longer the directory prefix. Pinned
manifest runs use `run.log` in the same readable directory. Their workspace, database, and artifacts
remain commit-scoped, and their exact revision remains in provenance records.

## Docker Images

The default config expects local Docker images such as:

```text
testmap-validation-sdk-all:latest
testmap-agent-eval-codex:latest
testmap-agent-eval-claude:latest
testmap-agent-eval-gemini:latest
```

The exact images needed depend on the commands and tool lanes you run. Validation uses the validation
image. Agent-tool experiments use the corresponding tool images listed under
`RuntimeConfig.Docker.Images.AgentTools`.

## First Example Run

Start with a small candidate limit and one target repository.

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --file .\TestMap\Data\pinned-targets-smoke.yaml
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- experiment --config .\TestMap\Config\default-config.json
```

For quick experiments, set:

```json
{
  "ExperimentConfig": {
    "CandidateLimit": 1,
    "BudgetModes": ["pass-at1"],
    "Evaluation": {
      "TestMap": { "Enabled": true },
      "Tools": { "Enabled": false }
    }
  }
}
```

## Output

Look under `RuntimeConfig.FilePaths.OutputDirPath` for:

- per-project SQLite databases
- generated experiment CSVs
- tool-attempt artifacts
- prompt, task-card, stdout, stderr, and JSONL logs for tool runs
# Pinned Target Setup

1. Create a manifest from a fixed source file or one GitHub repository URL.
2. Archive the source/rejection bundle for schema 1 or the resolution-record/manifest bundle for
   schema 3.
3. Run `targets verify` with the credentials and network policy used for the pilot.
4. Review every non-`Available` row before starting the experiment.
5. Keep revision workspaces under a TestMap-controlled temporary root.

An existing workspace is reused only when it is a valid repository with the expected origin, no
uncommitted changes, and the requested commit available. TestMap does not discard researcher changes
during initial materialization. Concurrent use of the same revision is blocked by an exclusive
workspace lock.
