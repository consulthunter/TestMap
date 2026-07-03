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
```

Avoid shell syntax in `.env`:

```text
export OPENAI_API_KEY=...
```

The parser expects `NAME=value` lines. Double quotes are trimmed; single quotes are not.

## Target Repositories

`RuntimeConfig.FilePaths.TargetFilePath` points to a text file with one repository URL per line.

Example:

```text
https://github.com/consulthunter/TestMap-Example
```

TestMap clones or refreshes repositories under `RuntimeConfig.FilePaths.TempDirPath` and writes each
project database under the configured output directory.

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
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --config .\TestMap\Config\default-config.json
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
