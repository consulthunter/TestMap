# Implementation Plan: Readable Log Directories

**Branch**: `004-readable-log-directories` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-readable-log-directories/spec.md`

## Summary

Use a readable UTC path for legacy and pinned per-project log directories:
`YYYY-MM-DD/HH-mm-ss_<owner>-<repository>`. Capture one run-start instant and propagate it from
configuration into each project model so date and time cannot straddle midnight. Preserve the
existing random `ProjectId` as a run/persistence identifier and existing log filename; it no longer
selects the directory. Reserve candidate directories with a fixed exclusive marker and deterministic
`-02`, `-03` ordinals so concurrent same-second runs cannot merge or overwrite evidence. Pinned
workspace, database, artifact, and provenance paths remain commit-scoped; pinned logs use `run.log`
inside the readable directory.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: .NET `System.IO`, Serilog, existing TestMap configuration and project models

**Storage**: Local filesystem directories, reservation marker, and log files; no database schema change

**Testing**: xUnit unit, integration, and end-to-end test projects

**Target Platform**: Windows and Linux filesystems supported by the TestMap CLI and Docker workflows

**Project Type**: .NET command-line research tool

**Performance Goals**: Allocate 100 same-second directories without collision or overwrite in under
2 seconds on a local filesystem; ordinary allocation remains negligible relative to repository work

**Constraints**: UTC only; portable path characters; one captured instant; no random directory
component; exclusive cross-process reservation; no rename/migration; pinned revision provenance unchanged

**Scale/Scope**: One directory per project run, including concurrent runs of the same
repository; hundreds of run directories per date are supported by ordinal allocation

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **Scientific purpose**: This feature affects retained audit evidence only. It makes execution logs
  easier to correlate with run time while protecting the claim that failures and diagnostics remain
  attributable to one project run.
- **Units and scope**: The affected unit is one project run and its log directory. Attempt,
  generated-test, candidate, repository, experiment, metric, and result-row grains are unchanged.
  No aggregation or attribution rule changes.
- **Eligibility and pairing**: Sampling frames, candidate eligibility, randomization, baselines, and
  before/after pairing are untouched.
- **Provenance**: Existing repository, commit, configuration, model/tool, experiment, attempt,
  `ProjectId`, and persisted run identifiers remain authoritative. The readable path adds a UTC
  start-second label and collision ordinal but does not replace any identifier.
- **Lane fairness and isolation**: Both LLM and agentic lanes retain their existing logs and paths
  beneath the selected project directory. Budgets, retries, ordering, and workspace reset are
  unchanged. Pinned non-log paths and provenance remain repository-and-commit scoped.
- **Missingness and failures**: A reservation or logger failure remains visible. Existing or partial
  directories are never treated as empty reusable capacity, and no unavailable log is converted to
  an observed artifact.
- **Verification**: Unit tests cover formatting, stable reuse, midnight capture, ordinals, 100-way
  contention, and identity preservation. Configuration integration covers propagation. End-to-end
  collection verifies a real log path. Existing pinned target tests provide non-regression coverage.
  No export or notebook checks are required because no data contract changes.
- **Compatibility**: Databases, CSVs, manifests, cohorts, metrics, result keys, output paths, and old
  log directories retain their meaning. No schema or policy version is required because this is a
  forward-only human-readable filesystem naming change.

### Post-Design Re-check

The design keeps the timestamp label separate from scientific identity, explicitly retains failed
or partial reservations, uses no metric or result data, and includes verification at the filesystem,
configuration, and end-to-end boundaries. All constitution gates remain satisfied.

## Project Structure

### Documentation (this feature)

```text
specs/004-readable-log-directories/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── log-directory-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Models/
│   └── ProjectModel.cs
├── Services/
│   ├── Configuration/
│   │   ├── ConfigurationService.cs
│   │   └── IConfigurationService.cs
│   └── Logging/
│       └── ProjectLogDirectoryAllocator.cs
└── Models/Targets/
    └── MaterializedRevision.cs             # verified unchanged

TestMap.UnitTests/
├── Models/
│   └── ProjectModelLogDirectoryTests.cs
├── Services/Logging/
│   └── ProjectLogDirectoryAllocatorTests.cs
└── Configuration/
    └── ConfigurationServiceTests.cs

TestMap.IntegrationTests/
└── Configuration/
    └── ReadableLogDirectoryIntegrationTests.cs

TestMap.EndToEndTests/
└── CollectTestsEndToEndTests.cs
```

**Structure Decision**: Keep the existing `ProjectModel` public logging entry point, but isolate
filesystem naming and exclusive reservation in a small logging service. `ConfigurationService`
owns the run-start instant and passes it to project models. This follows current project boundaries
without moving unrelated logging, persistence, or pinned-target code.

## Phase 0: Research Decisions

Research is consolidated in [research.md](research.md). The key decisions are:

1. Apply the readable directory to legacy and pinned project logs; keep pinned workspace, database,
   artifact, and provenance paths unchanged.
2. Capture one UTC run-start instant and derive the fixed `yyyy-MM-dd` and `HH-mm-ss` components
   from it.
3. Keep `ProjectId` and the existing project-specific log filename unchanged.
4. Claim a candidate directory using an exclusive fixed reservation marker; advance through
   deterministic ordinals when the directory or marker is already present.
5. Persist reservations and partial directories as audit evidence rather than cleaning or reusing
   them automatically.

## Phase 1: Design And Contracts

### Runtime Design

- `ConfigurationService` captures `RunStartedAtUtc` once in UTC. Existing `RunDate` continues to be
  produced for current consumers; the log date is derived independently as fixed `yyyy-MM-dd` from
  the same instant.
- Every initialized `ProjectModel` receives that instant. Directly constructed models capture UTC
  once by default and allow a fixed instant in tests.
- `ProjectModel.EnsureProjectLogDir()` routes both pinned and legacy logs through the allocator. It
  uses `run.log` for pinned runs and `<ProjectId>.log` for legacy runs, and updates the in-memory
  materialized log path to the selected allocation.
- `ProjectLogDirectoryAllocator` formats the base leaf and probes the base, then `-02`, `-03`, and so
  on. It rejects an already existing directory without modifying it.
- To close the create-directory race, a contender creates the candidate directory and then opens a
  fixed `.testmap-log-reservation` marker with exclusive create semantics. Exactly one contender can
  own a newly raced directory; losers continue to the next ordinal.
- The reservation marker remains in the selected directory. If allocation or logger creation fails,
  the directory remains visible and later runs skip it, preserving honest failure evidence.
- Existing `LogsFilePath` consumers receive the full selected path; child Docker/test logs remain
  beside the primary project log.

### Test Design

- Pure allocator tests use fixed UTC instants and temporary roots for exact path grammar,
  zero-padding, repository labels, pre-existing directories, deterministic ordinals, and a 100-task
  same-second contention case.
- `ProjectModel` tests prove repeated initialization is idempotent, `ProjectId` does not determine
  the directory, log filenames and persisted IDs remain unchanged, and pinned `MaterializedRevision`
  log paths update to the selected `run.log` while non-log revision paths remain unchanged.
- Configuration tests prove one captured instant reaches all project models and that default log
  parents remain `yyyy-MM-dd`, including a controlled near-midnight instant.
- Integration tests create multiple project runs against one root and inspect distinct directories
  and files.
- The existing collection end-to-end test gains an assertion for the readable path and actual log
  creation.

## Complexity Tracking

No constitution violations or exceptional architectural complexity are required.
