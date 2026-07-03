[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.21179574.svg)]([https://doi.org/10.5281/zenodo.21172973](https://doi.org/10.5281/zenodo.21179574))
![License](https://img.shields.io/badge/License-MIT-yellow.svg)
![Language](https://img.shields.io/badge/Language-CSharp-blue.svg)
[![arXiv](https://img.shields.io/badge/arXiv-2606.10211-b31b1b.svg)](https://arxiv.org/abs/2606.10211)

# TestMap

TestMap is a C# repository analysis and test-generation evaluation tool. It ingests repositories,
builds a persisted model of their source and test code, collects coverage and mutation evidence, and
uses that evidence to evaluate LLM-generated tests and agentic coding tools.

The core idea is simple: make test-generation experiments measurable. TestMap records what code was
targeted, what evidence was provided, what files changed, whether the generated tests compiled and
ran, and how coverage and mutation scores changed.

## What TestMap Does

- Clones or reuses C# repositories listed in a target file.
- Discovers solutions, projects, source files, test files, and test frameworks.
- Uses Roslyn and repository analysis to persist code entities and source/test relationships.
- Runs build, test, coverage, mutation, code-metric, and test-smell collection workflows.
- Selects candidate methods for test generation from existing tests, coverage gaps, mutation data, or
  metric-driven improvement signals.
- Runs the built-in LLM generation lane and optional Docker-based agent tool lanes.
- Persists generation attempts, tool attempts, validation outcomes, changed-file data, token usage
  where available, and result rows for later analysis.

## Directory Layout

```text
TestMap/                      .NET application, config, Docker images, migrations, outputs
TestMap/Config/               example and experiment configuration JSON files
TestMap/Docker/               validation and agent-tool Docker runners
TestMap/Migrations/           EF Core migrations for project/output databases
TestMap.UnitTests/            unit test project
TestMap.IntegrationTests/     integration tests, including migration schema checks
Docs/                         user-facing documentation
.ai/Docs/                     internal design notes and implementation plans
Analysis/                     analysis helpers for result datasets
```

## Main Commands

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- setup
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- generate-tests --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- experiment --config .\TestMap\Config\default-config.json
```

> **Note:** `generate-tests` is experimental. For measured evaluation runs, prefer
> `experiment` (see [Stable And Experimental Surfaces](#stable-and-experimental-surfaces)).

## Documentation

- [Setup](Docs/SETUP.md): dependencies, first-time setup, `.env`, Docker images, and a first example run.
- [Configuration](Docs/CONFIG.md): config model, override rules, tool model settings, secrets, and experimental switches.
- [How It Works](Docs/HOW_IT_WORKS.md): high-level architecture and execution flow.
- [How To Use](Docs/HOW_TO_USE.md): regular generation, experiments, tool evaluation, outputs, and practical workflows.

## Stable And Experimental Surfaces

The repository ingestion, analysis, persistence, and validation plumbing are the platform
layers. The LLM generation lane, repair loops, metric-driven target selection, and agentic tool
comparison are active evaluation surfaces. They are useful, but should be run with small candidate
limits first and interpreted as experiment results rather than product guarantees.

The standalone `generate-tests` command is **experimental** — it runs the built-in LLM pipeline
outside the controlled experiment harness. Use it for ad hoc generation only; use `experiment` for
anything you intend to measure or report.

For Basic Extension generation, keep `EnableSpeculativePlanning` disabled unless you are deliberately
running an ablation. The one-shot structured patch path is the recommended default.
