[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.21172973.svg)](https://doi.org/10.5281/zenodo.21172973)
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
dotnet run --project .\TestMap\TestMap.csproj -- check-projects --file .\TestMap\Data\pinned-targets-smoke.yaml
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- generate-tests --config .\TestMap\Config\default-config.json
dotnet run --project .\TestMap\TestMap.csproj -- experiment --config .\TestMap\Config\default-config.json
```

`check-projects` screens the exact commit in each pinned YAML target. It publishes a complete YAML
report plus directly reusable `tests-detected` and `no-tests-detected` target manifests. Repository,
commit, and source provenance are preserved; access failures and incomplete provider trees remain
indeterminate instead of being treated as evidence that tests are absent. Exit code `2` means an
auditable bundle was published but the screening set is incomplete.

> **Note:** `generate-tests` is experimental. For measured evaluation runs, prefer
> `experiment` (see [Stable And Experimental Surfaces](#stable-and-experimental-surfaces)).

## Documentation

- [Research Constitution](.specify/memory/constitution.md): non-negotiable principles for
  reproducibility, measurement validity, lane fairness, provenance, and research review.
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

## Coverage Integrity

Coverage collection records a terminal outcome even when no artifact is produced. The runner tries
the requested collector first, falls back to the other built-in collector when necessary, and writes
a `coverage-collection-v1` sidecar containing provider attempts, artifact hashes, merge inputs, and
the final reason. Test success and coverage success are separate outcomes.

Corrected reports use measurement policy `coverage-integrity-v1`. Raw class and member observations
are persisted before source attribution, so unmatched, ambiguous, out-of-project, unsupported, and
interrupted observations remain auditable with nullable source IDs. Exact line and branch counters
carry explicit availability; unavailable values must not be interpreted as measured zero.

`HasCoverage` is true only for the latest corrected report with usable mapped coverage. Historical
reports remain queryable but are not silently upgraded to the corrected policy. Coverage-dependent
candidate, risk, evidence, comparison, and MSR consumers use mapped rows from usable corrected
reports only. Constructor observations (`.ctor` and `.cctor`) are retained and attributed when their
kind, lines, and signature identify a unique persisted constructor.

## Assertion-Lineage Evidence

Experiment result schema 4.0 can report whether each recognized generated-test assertion has a
backward data-flow connection to production code. The classifier uses three categories:

- `Traced`: at least one observed assertion operand reaches a uniquely resolved production member;
- `Trivial`: every observed operand is fully explained by literals or test-local computation;
- `Unresolved`: no production lineage is confirmed and at least one required path is ambiguous,
  unsupported, cyclic, missing, or beyond the configured depth.

`Unavailable`, `NotApplicable`, and historical `NotMeasured` are measurement statuses, not assertion
categories. Counts remain blank when evidence is unavailable, so missing analysis is never presented
as zero assertions. Raw per-assertion evidence is written beside a results CSV as
`*.assertions.csv`; downstream tooling derives traced-only views without deleting trivial or
unresolved observations.

The analysis is intentionally data-dependence-only. A production-derived branch condition does not,
by itself, make an assertion traced. Likewise, `Traced` establishes connection to production code,
not logical oracle strength, mutation sensitivity, or proof that an assertion caused a mutant kill.
Assertion lineage is complementary to coverage and mutation evidence and does not alter generation
acceptance, retries, repair stopping, or dynamic metric calculation.

## Why?

Originally, this started a an MSR (mining software repositories) tool. I wanted to get source <-> test pairs for fine-tuning an LLM for software testing in C#.
Then, I wondered about how would I know the quality of such tests, possibly for RL (reinforcement learning driven by acutal test performance). A friend suggested running the projects in a Docker environment, so I would have actual data.
Then, I figured now that I was collecting all of this data I could use it to generate tests for projects.
Finally, I was asked what about a comparison between a generic LLM approach (ChatUniTest) versus an actual Agent (mini-swe-agent) which is how this tool ended up the way it is today.

# Pinned Repository Experiments

Measured `experiment` runs consume an immutable YAML target manifest rather than a URL list. Each
target binds `owner/repository` to a full 40-character commit SHA, and TestMap records the requested
and resolved revisions in SQLite and schema 3.0 result CSVs. Create and verify a manifest before a
pilot:

```powershell
dotnet run --project TestMap -- targets create --input targets.csv --output targets.yaml
dotnet run --project TestMap -- targets verify --file targets.yaml
dotnet run --project TestMap -- experiment --config TestMap/Config/pinned-target-experiment.example.json
```

For one GitHub repository, the CSV step is optional:

```powershell
dotnet run --project TestMap -- targets create `
  --url https://github.com/powershell/platyps `
  --output TestMap/Output/platyps-targets.yaml
```

URL mode resolves the named default branch and its exact Git commit, confirms repository metadata,
then publishes a content-addressed resolution record before the schema-3 manifest. It uses
`GITHUB_TOKEN` when present and records only `authenticated` or `anonymous`, never the credential.
Downstream commands verify the linked record locally and keep using the recorded commit even if the
remote branch later moves. A valid URL that cannot be resolved leaves a categorized resolution
record but does not create or replace a target manifest. CSV creation remains the schema-1 workflow.

Discovery commands may still consume documented URL lists. Experiment mode rejects them because an
unpinned branch tip cannot support reproducible lane comparisons.
