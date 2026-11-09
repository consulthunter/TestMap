# Implementation Plan: Check Project Manifests

**Branch**: `not created - no before-plan branch hook configured` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-check-project-manifests/spec.md`

## Summary

Replace the per-project, append-to-text-file `check-projects` pipeline with a manifest-level command
that validates one pinned-target YAML sampling frame, checks the Git tree for each exact commit using
a versioned test-presence policy, and publishes two directly reusable categorized target manifests
plus a complete observation report. Artifacts are deterministic and content-addressed; a bundle
pointer is written last so partial publication cannot appear complete. Remote failures and incomplete
trees remain explicit indeterminate observations rather than being converted to no tests detected.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`, nullable reference types enabled)

**Primary Dependencies**: System.CommandLine 2.0.9, Octokit 14.0.0, YamlDotNet 18.1.0, existing
target identity/fingerprint/atomic publication services

**Storage**: Versioned YAML files only; no database or migration changes

**Testing**: xUnit unit, integration, and end-to-end projects already present in the solution;
deterministic fake repository-tree clients and checked YAML fixtures

**Target Platform**: Cross-platform .NET CLI, with Windows and Linux filesystem behavior covered by
the existing target-manifest patterns

**Project Type**: CLI research tool

**Performance Goals**: After remote observations are available, validate, partition, serialize,
hash, and publish a 10,000-target bundle within 30 seconds in the reference test environment;
remote checks use configurable bounded concurrency

**Constraints**: Exact 40-character commit identity; no default-branch fallback; complete target-level
accounting; no append outputs; no credentials in reports; no negative classification from a
truncated tree; output manifests must be accepted by downstream target consumers

**Scale/Scope**: Up to 10,000 pinned targets per invocation, potentially including multiple commits
of one repository; three immutable bundle members plus one atomic bundle pointer

## Constitution Check

*GATE: PASS before Phase 0 research. Re-checked after Phase 1 design: PASS.*

- **Scientific purpose**: This feature changes repository screening and therefore candidate
  eligibility. It protects the claim that every evaluated lane used a shared sampling frame screened
  at the exact revision declared before the experiment.
- **Units and scope**: The canonical observation grain is one repository target, identified by
  normalized repository plus exact commit. Categorized manifests are target subsets; report counts
  aggregate observations without collapsing revisions. No coverage, mutation, test, or attempt metric
  is produced by this feature.
- **Eligibility and pairing**: The complete input manifest is the sampling frame. `TestsDetected` is
  eligible for later collection, `NoTestsDetected` is a screening exclusion under a named heuristic,
  and every other status is unresolved missingness. Screening occurs before lane assignment, so both
  lanes inherit the same target set.
- **Provenance**: Every observation and derived artifact carries the input manifest SHA-256, target
  ID, repository, requested and observed commit, policy name/version, and check time. The bundle
  records artifact filenames and hashes. Random seeds, models, tools, and attempts do not apply.
- **Lane fairness and isolation**: The command has no lane-specific path and executes before cohorts
  or attempts. It cannot use output from one lane to determine another lane's eligibility.
- **Missingness and failures**: Invalid targets, identity mismatch, repository or commit
  unavailability, authentication/authorization failure, rate limiting, truncation, provider failure,
  and unexpected failure are explicit indeterminate statuses. None is converted to false, zero, or
  no tests detected.
- **Verification**: Unit tests cover policy boundaries, status mapping, partition invariants,
  serializers, hashes, and publication. Integration tests cover exact-commit API behavior and
  bundle consumption. End-to-end tests cover a moved default branch and incomplete runs. Contract
  tests validate every YAML schema and CLI exit code. A 10,000-target publication benchmark records
  the success criterion.
- **Compatibility**: Legacy URL/text input and `repos_with_tests.txt` / `repos_without_tests.txt` are
  intentionally unsupported. Existing source target manifests remain schema version 1. Derived
  target manifests use schema version 2, and downstream readers are extended to accept both versions.
  No database, result CSV, cohort, or notebook meaning changes.

### Post-Design Re-check

The data model preserves one observation per target, the contracts make incomplete status explicit,
the exact requested/observed commit pair is mandatory, and bundle publication is auditable. No
constitution exception or complexity waiver is required.

## Project Structure

### Documentation (this feature)

```text
specs/002-check-project-manifests/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli-contract.md
│   ├── target-subset-manifest-v2.yaml
│   ├── project-check-report-v1.yaml
│   └── project-check-bundle-v1.yaml
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Program.cs
├── Models/Targets/
│   ├── TargetManifest.cs
│   └── ProjectCheckModels.cs
├── Services/Configuration/
│   └── TargetSourceReader.cs
├── Services/ProjectDiscovery/
│   ├── ProjectCheckCoordinator.cs
│   ├── GitHubProjectTreeProbe.cs
│   ├── ProjectTestPresencePolicy.cs
│   ├── ProjectCheckBundlePublisher.cs
│   └── Contracts/
│       └── ProjectCheckContracts.cs
├── Services/Targets/
│   └── TargetManifestSerializer.cs
└── [remove obsolete check-projects pipeline run, step, options, and append service]

TestMap.UnitTests/
├── CLI/CheckProjectsCommandTests.cs
├── Configuration/TargetSourceReaderTests.cs
├── ProjectDiscovery/ProjectCheckCoordinatorTests.cs
├── ProjectDiscovery/ProjectTestPresencePolicyTests.cs
├── ProjectDiscovery/ProjectCheckSerializationTests.cs
└── Targets/TargetManifestSerializerTests.cs

TestMap.IntegrationTests/
└── ProjectDiscovery/ProjectCheckBundleIntegrationTests.cs

TestMap.EndToEndTests/
└── ProjectDiscovery/PinnedProjectCheckEndToEndTests.cs
```

**Structure Decision**: Keep the public target-manifest format and shared serialization primitives in
the existing `Targets` boundary. Place the screening policy, remote tree probe, coordinator, and
bundle publication in `ProjectDiscovery`, where the old command already lives. Invoke the new
coordinator directly from a dedicated CLI command because this is one sampling-frame transaction,
not N independent repository analysis pipelines. Remove the obsolete run/step/service path so only
one classification implementation remains.

## Phase 0: Research Decisions

Research is consolidated in [research.md](research.md). The decisions are:

1. Resolve the exact Git commit first, then request its tree SHA; never inspect repository contents
   or the default branch.
2. Treat truncated recursive trees without positive evidence as indeterminate.
3. Use a conservative, path-only policy named `project-test-presence/v1` with boundary-aware
   directory, project-file, and test-source indicators.
4. Use a manifest-level coordinator with bounded concurrency and deterministic result ordering.
5. Extend the target format with a derived-manifest schema version 2 while preserving source schema
   version 1 support.
6. Publish content-addressed report/manifests first and an atomic bundle pointer last.
7. Keep `--config` as a source of defaults while adding direct `--file` and output options; reject
   legacy data regardless of invocation style.
8. Return distinct complete, incomplete, and fatal CLI exit codes.

## Phase 1: Design And Contracts

### Data Model

[data-model.md](data-model.md) defines the target-manifest schema variants, project-check
observation vocabulary, derived subset metadata, report summary, and bundle publication invariants.

### Public Contracts

- [CLI contract](contracts/cli-contract.md): options, defaults, completion semantics, and exit codes.
- [Derived target manifest v2](contracts/target-subset-manifest-v2.yaml): downstream-compatible YAML
  target subset with parent/report/policy provenance.
- [Project check report v1](contracts/project-check-report-v1.yaml): complete target-level census and
  status counts.
- [Project check bundle v1](contracts/project-check-bundle-v1.yaml): last-written completion pointer
  with hashes for all immutable members.

### Implementation Sequence

1. Extend target-manifest models and strict serialization for source schema 1 and derived schema 2,
   including valid empty derived subsets.
2. Introduce project-check status, evidence, observation, report, and bundle models with strict
   validation and deterministic YAML serializers.
3. Implement and unit-test the versioned boundary-aware `project-test-presence/v1` policy.
4. Replace default-branch probing with an exact-commit GitHub probe that verifies repository
   identity, resolves the requested commit, reads its tree, and exposes truncation and provider
   failures without classification.
5. Implement bounded manifest-level coordination, one terminal observation per target, deterministic
   ordering, partition invariants, and aggregate counts.
6. Implement content-addressed member publication and bundle-pointer-last commit semantics, including
   injected-failure tests.
7. Replace the pipeline-backed CLI with the dedicated command, retain configuration-based defaults,
   and remove obsolete append-based components and registrations.
8. Extend downstream target reading to verify schema 2 parent/report provenance and consume empty or
   non-empty derived manifests safely.
9. Add integration/end-to-end fixtures for different commits, moved default branches, tree
   truncation, output reuse, and interrupted publication.
10. Update user documentation and run the full .NET test matrix plus the 10,000-target publication
    benchmark.

## Complexity Tracking

No constitution violations require justification.
