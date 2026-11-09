# Implementation Plan: Pinned Repository Targets

**Branch**: `not created - no before-plan branch hook configured` | **Date**: 2026-07-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-pinned-repository-targets/spec.md`

## Summary

Replace URL-only experiment targets with a versioned YAML manifest that identifies each target by
normalized `owner/repository` plus a full commit SHA. Add first-class `targets create` and
`targets verify` CLI commands, using only `name` and `lastCommitSHA` from comma- or tab-delimited
source files. Materialize the exact commit into revision-specific workspace, database, and artifact
paths; preserve the verified base commit in run provenance; enforce it at every protected pipeline
and attempt boundary; restore it after every attempt; and publish explicit target and workspace
integrity outcomes. Canonical result files move to schema `3.0` because pinned provenance is required
and historical rows cannot be retroactively certified.

## Technical Context

**Language/Version**: C# 14 on .NET 10

**Primary Dependencies**: System.CommandLine 2.0.9; LibGit2Sharp 0.31.0; EF Core/SQLite 10.0.9;
CsvHelper 33.1.0 for structured delimited input; YamlDotNet 18.1.0 for manifest serialization;
Octokit 14.0.0 for GitHub availability classification where remote API evidence is required

**Storage**: Versioned YAML target manifests; versioned CSV verification/rejection reports; one
SQLite database and artifact root per repository revision; new EF migration for run provenance and
workspace-integrity observations

**Testing**: xUnit unit tests, SQLite in-memory repository tests, filesystem/Git integration tests,
CLI parsing tests, migration/schema integration tests, and end-to-end pipeline tests against local
Git fixtures with no network dependency

**Target Platform**: Cross-platform .NET CLI on Windows and Linux; GitHub-hosted public or
credential-accessible repositories; Docker remains optional for agent lanes

**Project Type**: Existing single .NET CLI application with unit, integration, and end-to-end test
projects plus Python analysis tooling

**Performance Goals**: Stream input rather than retaining ignored repository metadata in domain
models; verify targets with bounded concurrency and one output row per target; record import time,
peak memory, and integrity-check latency for the 1,525-row example and a generated 10,000-target
fixture so regressions are reviewable without a machine-dependent release threshold

**Constraints**: Full 40-character commit SHA required for measured experiments; no silent fallback
to a branch or current `HEAD`; existing dirty workspaces are never overwritten; unavailable targets
remain in the sampling frame; credentials never enter manifests, reports, logs, or snapshots; all
successful experiment rows carry non-empty matching requested/resolved commits; old result formats
remain legacy and are not upgraded implicitly

**Scale/Scope**: Initial manifests up to 10,000 targets; multiple revisions of one repository;
bounded project concurrency from existing runtime configuration; one target revision per isolated
database; both built-in LLM and agentic evaluation lanes

## Constitution Check

*GATE: Passed before Phase 0 research. Re-checked after Phase 1 design.*

- **Scientific purpose - PASS**: The feature protects repository-mining and AI-test evaluation
  claims from revision drift, stale clone reuse, and cross-revision persistence contamination.
- **Units and scope - PASS**: The target grain is one repository revision. Integrity observations
  are checkpoint-level audit rows; experiment, attempt, candidate, and generated-test grains remain
  unchanged. Result provenance is repeated on child rows without changing metric attribution.
- **Eligibility and pairing - PASS**: The manifest is the sampling frame. Target eligibility is
  decided before candidate selection. Baseline and post-attempt evidence are valid only when both
  belong to the same verified target ID and resolved commit.
- **Provenance - PASS**: Target ID, repository identity, requested/resolved commits, manifest SHA-256,
  source-file SHA-256, policy/schema versions, materialization time, integrity status, run UID,
  model/tool, seed, and configuration remain available at their appropriate grains.
- **Lane fairness and isolation - PASS**: Both lanes share one pinned base revision and revision-
  isolated baseline. Integrity checks and rollback are lane-neutral. Agent-created commits or branch
  changes are invalid; ordinary working-tree test edits remain measurable.
- **Missingness and failures - PASS**: Invalid target, repository unavailable, authentication failed,
  rate limited, commit unavailable, materialization failed, workspace busy, integrity failed, and
  restore failed are explicit outcomes. None become zero metric movement or silent exclusions.
- **Verification - PASS**: The design requires parser, serializer, identity, path, materialization,
  rollback, checkpoint, persistence, CLI, migration, export, analysis-audit, and end-to-end tests.
- **Compatibility - PASS**: YAML manifests are required only for measured experiment mode. URL-only
  discovery remains explicitly legacy. Result schema `3.0` and provenance policy
  `pinned-target-v1` distinguish new evidence; old databases and CSVs are not relabeled.

## Architecture and Data Flow

1. `targets create` reads a comma- or tab-delimited source file, detects or accepts the delimiter,
   validates `name` and `lastCommitSHA`, derives canonical GitHub URLs, deduplicates by target ID,
   writes a content-addressed rejection report, and atomically publishes the manifest last as the
   bundle commit.
2. `targets verify` loads and validates the manifest, checks repository/commit availability with
   bounded concurrency, and atomically writes one status row per target in manifest order.
3. Experiment configuration points `TargetFilePath` to the manifest. Configuration loading creates
   one `ProjectModel` per target and derives revision-specific workspace, database, artifact, and log
   paths before dependency injection or database initialization.
4. `MaterializeRepositoryStep` validates an existing workspace or acquires the repository, fetches
   the requested object when necessary, checks out the exact commit in detached-HEAD state, cleans
   the workspace, verifies origin and commit, and records immutable materialization provenance.
5. `WorkspaceIntegrityService` verifies expected versus actual commit at every required boundary and
   persists an observation. A target pipeline or attempt stops at the first blocking mismatch.
6. `PinnedWorkspaceService` restores tracked content to the base commit, removes attempt-created
   untracked and build artifacts, and re-verifies integrity. It never resets to incidental `HEAD`.
7. Cohort keys, resume keys, attempt IDs, persisted run provenance, result CSVs, manifests, and
   analysis repository keys use the same normalized repository identity and resolved commit.
8. Analysis ingestion accepts result schema `3.0` for pinned-target claims and audits target identity,
   commit equality, manifest consistency, integrity status, and cross-revision collisions.

## Implementation Design

### Target Import and Manifest

- Add target-domain records under `TestMap/Models/Targets/` for manifest metadata, repository target,
  source-row provenance, rejection, verification, and status enums.
- Add `TargetManifestSerializer` with strict schema-version validation, unknown-field rejection for
  top-level contract errors, deterministic target ordering, UTF-8 without BOM, LF line endings, and
  atomic manifest replacement after referenced reports are durable.
- Add `DelimitedTargetImportService` using CsvHelper. Delimiter candidates are comma and tab only;
  an explicit override wins. Auto-detection must resolve a unique header containing both required
  columns and stable field counts across sampled records, otherwise fail before output.
- Compare headers case-insensitively after trimming a possible BOM, but keep the contract names
  `name` and `lastCommitSHA` in diagnostics. Ignore all non-required columns without deserializing
  their large JSON/text payloads into target objects.
- Compute `source.sha256` from exact input bytes. Preserve the input filename and one-based logical
  data-record numbers, excluding the header, for every emitted or rejected record. A deduplicated
  target stores all contributing record numbers. Embedded newlines do not create additional records.
- Derive `https://github.com/{owner}/{repository}.git`; reject names outside exactly two non-empty,
  slash-separated path segments and reject non-hex or non-40-character commit values.
- Compute `target_id = sha256(lower(repository_identity) + "|" + lower(commit))`.

### CLI Contracts

- Add a top-level `targets` command with `create` and `verify` subcommands in `Program.cs`, delegating
  behavior to services rather than implementing file or network work in command handlers.
- `targets create` requires `--input` and `--output`; accepts `--delimiter auto|comma|tab` and optional
  `--rejections`, defaulting to a content-addressed rejection filename beside the manifest.
- `targets verify` requires `--file`; accepts `--status-output` and bounded `--max-concurrency`.
- Both commands return nonzero for contract-level failure. `create` may succeed with row rejections
  when at least one valid target exists and publishes the manifest only after its referenced report
  is durable; `verify` completes and reports target-level unavailability, reserving nonzero exit for
  unreadable/invalid manifests or inability to publish a complete report.
- Keep current JSON application configuration. YAML is only the target-manifest contract.

### Configuration and Revision Isolation

- Replace direct line-by-line target reading in `ConfigurationService` with an `ITargetSourceReader`.
  In experiment mode it requires a valid YAML manifest; discovery-only runs may continue to consume
  URL lists through an explicitly named legacy reader.
- Extend `ProjectModel`/`ProjectContext` with immutable `RepositoryTarget` and `MaterializedRevision`
  state. Remove commit fallback chains from experiment code; successful evaluation reads one required
  `ResolvedCommit` value.
- Derive paths as:
  - workspace: `{TempDir}/{owner}/{repo}/{commit}/workspace`
  - database: `{OutputDir}/{owner}-{repo}/{commit}/analysis.db`
  - artifacts: `{OutputDir}/{owner}-{repo}/{commit}/artifacts`
  - target reports: beside the manifest unless explicitly overridden
- Normalize path segments, reject traversal/reserved segments, and include the full commit in storage
  paths. Use an exclusive revision lock file to prevent concurrent processes from mutating one
  workspace; report `WorkspaceBusy` rather than waiting indefinitely.
- Include target ID in `ProjectModel.ContentHash` for pinned targets. Legacy discovery identities
  retain existing behavior and cannot enter measured experiment mode.

### Exact Revision Materialization

- Replace clone-only semantics with `IRepositoryMaterializationService` and
  `MaterializeRepositoryStep`; update all run pipelines to use the step.
- For pinned targets: reject a non-repository non-empty workspace; validate `origin`; reject dirty
  user state before any destructive operation; clone or fetch; resolve the exact commit object;
  perform a forced detached checkout only after safety checks; hard reset to that commit; remove
  untracked and known ignored build artifacts; and verify actual SHA exactly equals requested SHA.
- Use standard remote fetch refspecs first, then a targeted commit refspec when the commit is not
  present. Failure to obtain the requested object is `CommitUnavailable`, never fallback.
- Centralize credentials in an in-memory credentials provider using existing configured environment
  secrets. Redact URLs before logging and never persist credentials.
- Stop swallowing clone/materialization exceptions. Convert known failures to stable target statuses,
  log structured context without secrets, persist status where possible, and stop that target's
  pipeline before extraction.

### Integrity Enforcement and Rollback

- Add `IWorkspaceIntegrityService.CheckAsync(checkpoint, context)` returning a structured observation
  with expected/actual commit, repository validity, origin match, dirty status, and blocking status.
- Required checkpoints: pre-extraction, pre-baseline, experiment-start, pre-attempt, post-attempt/
  pre-analysis, post-rollback, and pre-results-publication.
- Dirty working trees are allowed only at post-attempt/pre-analysis and pre-results-publication while
  an attempt is under measurement; the commit and origin must still match. Other checkpoints require
  clean state after permitted artifact cleanup.
- Persist every protected checkpoint to `workspace_integrity_observations`; link by experiment run,
  lane, stable work-item key, attempt number, and target ID rather than using incompatible lane-specific
  attempt foreign keys.
- Replace `RollbackWorkspaceService` reset-to-`HEAD` behavior with reset-to-verified-base-commit.
  Restore failures are blocking and prevent the next attempt.
- Add a shared `WorkspaceIntegrityFailed` failure category/status mapping for both lanes. Do not
  calculate or accept post-attempt impact after commit drift.

### Persistence and Result Provenance

- Add run-level provenance fields to experiment run domain/entity/configuration/mapping: target ID,
  repository identity, requested commit, resolved commit, manifest SHA-256, source SHA-256,
  materialized time, final integrity status, and policy version.
- Add `workspace_integrity_observations` with indexed experiment run, target, work-item, checkpoint,
  lane, and status columns. Store expected and nullable actual commits plus sanitized details.
- Add requested/resolved target provenance to the persisted project record so pre-experiment analysis
  can be audited inside its revision-specific database.
- Add a run-level target execution CSV beside the manifest/output root. Initialize one `Pending` row
  per manifest target before project execution and atomically publish a terminal status for every
  target, including failures before SQLite initialization.
- Populate existing `ToolAttempt.BaseCommit` from required resolved provenance and add equivalent
  base-commit/integrity fields for built-in generation attempts.
- Extend `ExperimentResultFileRow` and writer with target/manifest/integrity fields and bump
  `ResultsSchemaVersion` from `2.0` to `3.0`. All row kinds inherit attempt-level provenance.
- Regenerate `schema.sql`, update the EF model snapshot, and update result manifest/audit contracts.

### Analysis Compatibility

- Update Python schemas and normalization to require pinned provenance for result schema `3.0`.
- Build `repository_key` from normalized repository identity plus resolved commit; retain a separate
  repository-only key for cross-revision grouping.
- Add blocking audits for requested/resolved mismatch, missing successful commit, duplicate target
  IDs, inconsistent manifest hashes within a run, failed final integrity, cohort commit mismatch,
  and revision path collisions.
- Keep schema `2.0` artifacts explicitly legacy. They may be analyzed by legacy workflows but cannot
  be pooled into analyses claiming `pinned-target-v1` compliance without a documented sensitivity
  analysis.

## Verification Strategy

- **Unit**: delimiter detection/override, quoted fields, BOM/header normalization, row provenance,
  malformed names/SHAs, deterministic YAML, manifest fingerprint, target ID, path safety, URL
  normalization, status mapping, integrity policy, result schema, and secret redaction.
- **Integration**: local bare remotes for fresh clone, fetch, wrong origin, unavailable commit,
  detached checkout, dirty workspace rejection, exact rollback, moved `HEAD`, deleted Git metadata,
  lock contention, and two revisions of one repository.
- **Persistence**: migration upgrade from current schema, clean database creation, schema snapshot,
  provenance round-trip, integrity observation indexes, and `schema.sql` parity.
- **CLI contract**: create/verify help, required options, exit codes, default output paths, example
  tab-delimited file, comma fixture, ambiguous fixture, rejection accounting, and atomic outputs.
- **End to end**: create manifest from a small fixture, verify it, run collection and a tiny two-lane
  experiment on a local pinned repository, force one agent-style revision movement, and confirm clean
  recovery plus complete schema `3.0` provenance.
- **Analysis**: dataset-builder and audit fixtures containing valid rows and every blocking defect;
  notebook smoke tests confirm target/revision headline counts and refuse noncompliant pooled data.

## Delivery Phases

1. Domain contracts, CSV/YAML dependencies, CLI create/verify, fixtures, deterministic reports.
2. Target-aware configuration and revision-specific path derivation.
3. Exact materialization, project-provenance migration/schema regeneration, target execution census,
   credentials/redaction, lock handling, and fail-fast pipeline behavior.
4. Integrity service, checkpoint orchestration, pinned rollback, and shared failure semantics.
5. Experiment/attempt migration, schema regeneration, result provenance, and schema `3.0` changes.
6. Analysis schema/audits, end-to-end fixture, documentation, and pilot readiness validation.

Each phase must leave tests green. Phases 3 through 5 are not pilot-safe independently; the pilot
gate opens only after the end-to-end and analysis audits in Phase 6 pass.

## Project Structure

### Documentation (this feature)

```text
specs/001-pinned-repository-targets/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli-contract.md
│   ├── result-provenance-v3.md
│   ├── target-manifest-v1.yaml
│   └── target-reports-v1.md
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Program.cs
├── Models/
│   ├── ProjectModel.cs
│   └── Targets/
├── Services/
│   ├── Configuration/
│   ├── RepoOperations/
│   ├── Targets/
│   ├── TestGeneration/Workspace/
│   └── Experiment/{Execution,Reporting}/
├── Execution/Steps/
├── Persistence/Ef/{Entities,Configuration,Mapping,Repositories}/
├── Migrations/
└── schema.sql

TestMap.UnitTests/
├── Configuration/
├── RepoOperations/
├── Targets/
└── TestGeneration/

TestMap.IntegrationTests/
├── Configuration/
├── Persistence/
├── RepoOperations/
└── Targets/

TestMap.EndToEndTests/
└── PinnedRepositoryTargetEndToEndTests.cs

Analysis/
├── src/analysis/{schema.py,normalize.py,audit_evaluation_data.py}
├── tests/
└── notebooks/
```

**Structure Decision**: Extend the existing single CLI application and its current test projects.
Target parsing and manifest concerns receive a dedicated bounded folder; Git materialization remains
under repository operations; attempt isolation remains under workspace services; provenance remains
under experiment persistence/reporting. No new deployable project or service is introduced.

## Complexity Tracking

No constitution violations require justification. The new target domain, integrity audit table, and
two parsing dependencies each remove concrete scientific or parsing ambiguity and follow existing
service/repository boundaries.
