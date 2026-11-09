# Agent Guide

TestMap is a C#/.NET 10 tool that mines C# repositories, persists a Roslyn model of their source and
tests to a per-project SQLite database, and evaluates LLM-generated tests and agentic coding tools
against coverage and mutation evidence. `Analysis/` is the Python package (with notebooks and R
models) that turns experiment outputs into datasets. Read `README.md`, `Docs/HOW_IT_WORKS.md`, and
the research constitution in `.specify/memory/constitution.md` before changing measurement code.

## Layout

```text
TestMap/                      application: services, EF Core persistence, migrations, Docker images
TestMap/Config/               experiment configs (eval-config.json is the full evaluation)
TestMap/Data/                 pinned YAML target manifests (evaluation.yaml, pilot.yaml, example.yaml)
TestMap.UnitTests/            unit tests (in-memory SQLite, in-memory Roslyn workspaces)
TestMap.IntegrationTests/     migrations, git materialization, real MSBuild workspace
TestMap.EndToEndTests/        command-level tests
Analysis/                     Python analysis package (uv), notebooks, R models, docs/data_reference.md
Replication/                  data and instructions for the published studies and pilots
specs/                        feature specs and plans (spec-kit)
```

## Commands

```powershell
dotnet build TestMap.slnx
dotnet test TestMap.UnitTests/TestMap.UnitTests.csproj
dotnet test TestMap.IntegrationTests/TestMap.IntegrationTests.csproj
dotnet run --project .\TestMap\TestMap.csproj -- experiment --config .\TestMap\Config\<config>.json

cd Analysis
uv sync
uv run pytest -q
```

Experiments need Docker Desktop (`desktop-linux` context) and the images in `Docs/SETUP.md`. Secrets
come from `TestMap/.env`; never print or commit its values. Launch long runs from a PowerShell
terminal, not from an IDE.

## Invariants

- **Baseline evidence is never rewritten by an attempt.** Members are versioned by
  `(object, signature, origin_kind, origin_attempt_id)`: unchanged members reuse the `Baseline` row,
  changed or new members get `ToolAttempt` / `LlmAttempt` rows. Fix entity identity rather than adding
  per-attempt snapshot tables.
- **Source/test mappings are traced once per analysis pass**, through
  `IAnalyzeProjectService.RefreshSourceTestMappingsAsync`, after every project is analyzed and before
  rollback. Rows are upserted by `(project, source, test, evidence_kind)` so mapping ids stay stable.
  Readers that mean the repository's own tests filter to test members with `origin_kind = 'Baseline'`.
- **Both lanes compare against one targeted baseline per candidate** (`candidate_baselines`).
- **Workspaces are restored to the pinned commit** after every attempt; `core.autocrlf=false` and
  `core.eol=lf` are pinned so container rewrites do not leave phantom changes.
- **Schema changes need an EF Core migration**, and the database is migrated at startup. Ask before
  adding one; prefer a change that fits the existing schema.
- **The study design is frozen.** New agent or architecture ideas are future work, not changes to the
  running evaluation. Changes that could shift analysis results (Roslyn, MSBuild, Semantic Kernel,
  pandas) must be called out.

## Working Conventions

- Work on `dev`; `main` is the PR base. Commit only when asked.
- Commit messages: `fix(scope): <present participle> ...`, for example
  `fix(mapping): tracing once per analysis pass and upserting mappings`.
- Experiments usually run on a separate machine. Before reviewing pilot data, confirm which build
  produced it (`__EFMigrationsHistory`, stack-trace paths, log formats).
- Code and comments follow the surrounding style; tests use xUnit with `[Trait("Category", ...)]`.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/007-token-usage-accounting/plan.md
<!-- SPECKIT END -->
