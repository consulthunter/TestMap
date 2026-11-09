# Tasks: Pinned Repository Targets

**Input**: Design documents from `specs/001-pinned-repository-targets/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Required by the TestMap Constitution because this feature changes repository identity,
workspace isolation, baseline validity, experiment provenance, persistence, canonical exports, and
analysis interpretation. Within each story, complete tests before the behavior they validate.

**Organization**: Tasks are grouped by user story. The scientific pilot gate remains closed until
all phases, including cross-cutting audits and end-to-end validation, are complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files and has no dependency on unfinished
  tasks in the same phase.
- **[Story]**: Maps the task to User Story 1 through User Story 5 in `spec.md`.
- Every task names the concrete file or directory it changes.

## Phase 1: Setup

**Purpose**: Add dependencies and deterministic fixtures needed by every story.

- [X] T001 Add CsvHelper 33.1.0 and YamlDotNet 18.1.0 package references in `TestMap/TestMap.csproj`
- [X] T002 [P] Add comma-, tab-, ambiguous-, malformed-, duplicate-, and BOM-delimited import fixtures under `TestMap.UnitTests/Fixtures/Targets/`
- [X] T003 [P] Add valid, invalid-version, duplicate-target, and malformed YAML fixtures under `TestMap.UnitTests/Fixtures/Targets/Manifests/`
- [X] T004 [P] Add reusable local bare-remote and multi-commit repository fixture builders in `TestMap.IntegrationTests/Fixtures/GitRepositoryFixture.cs`
- [X] T005 [P] Add pinned-target smoke input and local-repository fixture definitions under `TestMap.EndToEndTests/Fixtures/PinnedTargets/`

---

## Phase 2: Foundational

**Purpose**: Establish shared target identity, provenance, file publication, and service contracts.

**CRITICAL**: No user story implementation begins until this phase is complete.

- [X] T006 Define target manifest, source provenance, repository target, rejection, verification, materialization, and integrity domain records and enums under `TestMap/Models/Targets/`
- [X] T007 [P] Add target normalization, canonical GitHub URL derivation, full-SHA validation, and target-ID generation in `TestMap/Services/Targets/TargetIdentityService.cs`
- [X] T008 [P] Add SHA-256 byte fingerprinting and deterministic UTF-8/LF helpers in `TestMap/Services/Targets/TargetFingerprintService.cs`
- [X] T009 [P] Add atomic sibling-file publication abstraction in `TestMap/Services/Targets/AtomicFilePublisher.cs`
- [X] T010 Define `ITargetManifestSerializer`, `ITargetImportService`, `ITargetVerificationService`, and `ITargetSourceReader` contracts under `TestMap/Services/Targets/Contracts/`
- [X] T011 Extend `TestMap/Models/ProjectModel.cs` and `TestMap/App/ProjectContext.cs` with required repository target, materialized revision, and verified base-commit state
- [X] T012 [P] Add sanitized repository URL comparison and credential-redaction helpers in `TestMap/Services/RepoOperations/RepositoryUrlService.cs`
- [X] T013 Add target services, serializers, verifiers, materialization services, and integrity services to dependency injection in `TestMap/Services/ServiceCollectionExtensions.cs`
- [X] T014 [P] Add shared target-domain and integrity assertion helpers in `TestMap.UnitTests/Targets/TargetTestData.cs`

**Checkpoint**: Target identity and shared contracts are stable; user-story slices can begin.

---

## Phase 3: User Story 1 - Create a Reproducible Target Manifest (Priority: P1)

**Goal**: Convert the supplied repository dataset into a deterministic, minimal, versioned target
manifest with complete row accounting.

**Independent Test**: Run `targets create` against comma and tab fixtures containing valid,
duplicate, malformed, and ignored metadata fields. Verify deterministic targets, derived URLs,
source-row provenance, and a complete rejection report.

### Tests for User Story 1

- [X] T015 [P] [US1] Add delimiter detection, explicit override, ambiguous-input, BOM, quoting, and required-header tests in `TestMap.UnitTests/Targets/DelimitedTargetImportServiceTests.cs`
- [X] T016 [P] [US1] Add repository-name, full-SHA, canonical-URL, target-ID, and conflicting-duplicate tests in `TestMap.UnitTests/Targets/TargetIdentityServiceTests.cs`
- [X] T017 [P] [US1] Add deterministic serialization, strict schema, source-row aggregation, and round-trip tests in `TestMap.UnitTests/Targets/TargetManifestSerializerTests.cs`
- [X] T018 [P] [US1] Add rejection CSV header, escaping, logical-record numbering, content-addressed naming, manifest-last publication, and accounting tests in `TestMap.UnitTests/Targets/TargetRejectionReportWriterTests.cs`
- [X] T019 [P] [US1] Add `targets create` option, default-path, exit-code, and summary-output tests in `TestMap.UnitTests/CLI/TargetCommandsTests.cs`
- [X] T020 [US1] Add an integration test that imports `Replication/LicenseFilteredRepo/licenseFilteredRepoList.csv`, accounts for all 1,525 rows, and records import time/peak memory alongside a generated 10,000-target fixture in `TestMap.IntegrationTests/Targets/LicenseFilteredTargetImportTests.cs`

### Implementation for User Story 1

- [X] T021 [US1] Implement quote-aware comma/tab detection and streaming `name`/`lastCommitSHA` extraction in `TestMap/Services/Targets/DelimitedTargetImportService.cs`
- [X] T022 [US1] Implement validation, canonical URL derivation, normalization, target IDs, deduplication, and conflicting-duplicate handling in `TestMap/Services/Targets/TargetIdentityService.cs`
- [X] T023 [US1] Implement strict deterministic YAML reading and writing for manifest schema v1 in `TestMap/Services/Targets/TargetManifestSerializer.cs`
- [X] T024 [P] [US1] Implement canonical rejection CSV schema v1 in `TestMap/Services/Targets/TargetRejectionReportWriter.cs`
- [X] T025 [US1] Implement exact source-byte hashing, content-addressed rejection publication, report-hash verification, and atomic manifest-last publication in `TestMap/Services/Targets/TargetManifestCreationService.cs`
- [X] T026 [US1] Add the `targets create` command, delimiter values, output defaults, summaries, and exit semantics in `TestMap/Program.cs`
- [X] T027 [US1] Document manifest creation and the minimal `name`/`lastCommitSHA` import contract in `Docs/HOW_TO_USE.md` and `Docs/CONFIG.md`

**Checkpoint**: Researchers can independently create reproducible manifests without cloning or
evaluating repositories.

---

## Phase 4: User Story 2 - Verify the Sampling Frame Before a Run (Priority: P1)

**Goal**: Produce a complete availability census that retains every manifest target and explicitly
classifies invalid, inaccessible, authenticated, rate-limited, and unavailable-commit outcomes.

**Independent Test**: Verify a fixture manifest whose targets exercise every terminal status and
confirm exactly one ordered, atomic status row per target without modifying the manifest.

### Tests for User Story 2

- [X] T028 [P] [US2] Add verification status mapping, requested/resolved equality, wrong-identity, authentication, rate-limit, and unavailable-commit tests in `TestMap.UnitTests/Targets/TargetVerificationServiceTests.cs`
- [X] T029 [P] [US2] Add one-row-per-target, stable-order, nullable-resolved-commit, escaping, and atomic output tests in `TestMap.UnitTests/Targets/TargetVerificationReportWriterTests.cs`
- [X] T030 [P] [US2] Extend `targets verify` parsing, default-path, bounded-concurrency, complete-report, and exit-code tests in `TestMap.UnitTests/CLI/TargetCommandsTests.cs`
- [X] T031 [US2] Add local-remote integration tests for available and missing commits without network access in `TestMap.IntegrationTests/Targets/TargetVerificationIntegrationTests.cs`

### Implementation for User Story 2

- [X] T032 [US2] Implement repository and commit availability verification with sanitized failure classification in `TestMap/Services/Targets/TargetVerificationService.cs`
- [X] T033 [P] [US2] Implement ordered target verification report schema v1 in `TestMap/Services/Targets/TargetVerificationReportWriter.cs`
- [X] T034 [US2] Add bounded target concurrency while preserving manifest-order output in `TestMap/Services/Targets/TargetVerificationCoordinator.cs`
- [X] T035 [US2] Add the `targets verify` command, status output defaults, summaries, and complete-report exit semantics in `TestMap/Program.cs`
- [X] T036 [US2] Document verification statuses, sampling-frame accounting, credentials, and rate-limit behavior in `Docs/HOW_TO_USE.md`

**Checkpoint**: Researchers can audit target availability independently; unavailable targets remain
visible and do not silently alter the sampling frame.

---

## Phase 5: User Story 3 - Materialize the Exact Repository Revision (Priority: P1)

**Goal**: Make every collection and experiment pipeline operate on the exact declared commit in an
isolated revision workspace and stop visibly on any acquisition or identity failure.

**Independent Test**: Process local targets from no workspace, a matching clone, the wrong commit,
the wrong origin, a dirty workspace, and a missing commit. Only exact clean materializations proceed
to extraction, and each successful target has revision-specific paths and persisted provenance.

### Tests for User Story 3

- [X] T037 [P] [US3] Add revision path normalization, traversal, reserved-name, owner collision, and two-commit isolation tests in `TestMap.UnitTests/Targets/TargetPathResolverTests.cs`
- [X] T038 [P] [US3] Add manifest-mode versus legacy URL-list mode and measured-experiment rejection tests in `TestMap.UnitTests/Configuration/TargetSourceReaderTests.cs`
- [X] T039 [P] [US3] Add materialization status, fail-fast exception, requested/resolved provenance, and redacted-log tests in `TestMap.UnitTests/RepoOperations/RepositoryMaterializationServiceTests.cs`
- [X] T040 [P] [US3] Add revision lock acquisition, contention, stale-lock safety, and release tests in `TestMap.UnitTests/RepoOperations/RevisionWorkspaceLockTests.cs`
- [X] T041 [US3] Add fresh clone, existing clone, fetch missing object, detached checkout, wrong origin, dirty state, unavailable commit, and corrupt workspace integration tests in `TestMap.IntegrationTests/RepoOperations/RepositoryMaterializationIntegrationTests.cs`
- [X] T042 [US3] Add pipeline integration tests proving materialization failure prevents extraction/database insertion and still produces one terminal execution-status row per target in `TestMap.IntegrationTests/Execution/PinnedMaterializationPipelineTests.cs`

### Implementation for User Story 3

- [X] T043 [US3] Implement revision-specific workspace, database, artifact, and log path derivation in `TestMap/Services/Targets/TargetPathResolver.cs`
- [X] T044 [P] [US3] Implement exclusive per-revision workspace locking in `TestMap/Services/RepoOperations/RevisionWorkspaceLock.cs`
- [X] T045 [US3] Implement strict YAML target loading for measured experiments and explicit legacy URL loading for discovery workflows in `TestMap/Services/Configuration/TargetSourceReader.cs`
- [X] T046 [US3] Replace line-based project initialization with target-aware models and revision-specific paths in `TestMap/Services/Configuration/ConfigurationService.cs`
- [X] T047 [US3] Include target ID in pinned `ProjectModel.ContentHash` while preserving legacy discovery identity in `TestMap/Models/ProjectModel.cs`
- [X] T048 [US3] Implement origin validation, safe reuse, fetch, exact commit resolution, detached checkout, clean reset, and fail-fast statuses in `TestMap/Services/RepoOperations/RepositoryMaterializationService.cs`
- [X] T049 [US3] Replace clone-only interfaces with exact materialization contracts in `TestMap/Services/RepoOperations/IRepoOperations.cs`, `TestMap/Services/RepoOperations/RepoOperations.cs`, and `TestMap/Services/RepoOperations/ICloneRepoService.cs`
- [X] T050 [US3] Replace `CloneRepoStep` with target-aware materialization behavior in `TestMap/Execution/Steps/CloneRepoStep.cs` and all pipeline definitions under `TestMap/Runs/`
- [X] T051 [US3] Persist requested/resolved project provenance, add its EF migration/model snapshot, regenerate `TestMap/schema.sql`, and add upgrade/clean-create tests in `TestMap/Persistence/Ef/Entities/ProjectEntity.cs`, its EF mapping/configuration/repository, `TestMap/Migrations/`, and `TestMap.IntegrationTests/Persistence/PinnedProjectProvenanceSchemaTests.cs`
- [X] T052 [US3] Initialize and atomically update one target-execution census row per manifest target while allowing other targets to complete in `TestMap/App/ProjectRunCoordinator.cs` and `TestMap/Services/Targets/TargetExecutionReportWriter.cs`
- [X] T053 [US3] Document manifest-only experiment input, revision paths, dirty-workspace policy, and failure behavior in `Docs/SETUP.md`, `Docs/CONFIG.md`, and `Docs/HOW_IT_WORKS.md`

**Checkpoint**: Collection runs can be pinned and revision-isolated. Experiment validity still awaits
the continuous integrity and rollback work in User Story 4.

---

## Phase 6: User Story 4 - Enforce Workspace Integrity Throughout Evaluation (Priority: P1)

**Goal**: Ensure both lanes start, measure, restore, and publish against the same immutable base
commit, with persisted integrity evidence and fail-closed behavior.

**Independent Test**: Run valid working-tree edits, branch changes, detached commits, deleted Git
metadata, crashes, and restore failures through both lanes. Valid edits are measured; commit drift is
blocked; rollback restores the base; the next attempt starts clean; schema `3.0` results prove it.

### Tests for User Story 4

- [X] T054 [P] [US4] Add checkpoint policy tests for clean state, expected dirty state, revision mismatch, origin mismatch, invalid repository, and unavailable status in `TestMap.UnitTests/TestGeneration/Workspace/WorkspaceIntegrityServiceTests.cs`
- [X] T055 [P] [US4] Replace reset-to-HEAD expectations with reset-to-base, untracked cleanup, moved-HEAD recovery, and restore-failure tests in `TestMap.UnitTests/TestGeneration/RollbackWorkspaceServiceTests.cs`
- [X] T056 [P] [US4] Add LLM and agentic checkpoint-order, stop-before-analysis, stop-before-publication, and next-attempt recovery tests in `TestMap.UnitTests/TestGeneration/ExperimentWorkspaceIntegrityTests.cs`
- [X] T057 [P] [US4] Add run and attempt provenance mapping round-trip tests in `TestMap.UnitTests/Persistence/PinnedProvenanceMappingTests.cs`
- [X] T058 [P] [US4] Add result schema `3.0`, required provenance, legacy commit alias, row-kind inheritance, and integrity-success guard tests in `TestMap.UnitTests/TestGeneration/ExperimentResultsWriterTests.cs`
- [X] T059 [US4] Add experiment/attempt/integrity migration upgrade, clean creation, integrity indexes, and schema parity tests in `TestMap.IntegrationTests/Persistence/PinnedTargetSchemaTests.cs`
- [X] T060 [US4] Add moved-HEAD, agent-created-commit, deleted-Git-metadata, timeout, and failed-restore integration tests in `TestMap.IntegrationTests/TestGeneration/WorkspaceIntegrityIntegrationTests.cs`

### Implementation for User Story 4

- [X] T061 [US4] Implement checkpoint-aware integrity evaluation and structured observations in `TestMap/Services/TestGeneration/Workspace/WorkspaceIntegrityService.cs`
- [X] T062 [US4] Reset tracked content to `ProjectContext.MaterializedRevision.ResolvedCommit`, clean artifacts, and verify restoration in `TestMap/Services/TestGeneration/Workspace/RollbackWorkspaceService.cs`
- [X] T063 [US4] Add `WorkspaceIntegrityObservationEntity`, EF configuration, mapping, and repository under `TestMap/Persistence/Ef/`
- [X] T064 [US4] Add pinned provenance columns to experiment runs and built-in attempts across `TestMap/Models/Experiment/`, `TestMap/Persistence/Ef/Entities/Experiment/`, EF configurations, mappings, and repositories
- [X] T065 [US4] Populate and validate existing `ToolAttempt.BaseCommit` plus final integrity status in `TestMap/Models/AgentTools/ToolAttempt.cs` and its persistence mapping/configuration
- [X] T066 [US4] Add the EF migration and model snapshot changes for experiment run, attempt, and integrity provenance under `TestMap/Migrations/`
- [X] T067 [US4] Insert pre-baseline, experiment-start, pre-attempt, post-attempt/pre-analysis, post-rollback, and pre-publication checks in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T068 [US4] Add the pre-extraction checkpoint before source parsing in `TestMap/Execution/Steps/ExtractInfoStep.cs`
- [X] T069 [US4] Map blocking integrity outcomes consistently across built-in and tool lanes in `TestMap/Services/TestGeneration/Validation/`, `TestMap/Rules/Generation/`, and `TestMap/Services/Experiment/Evaluation/AgentTools/`
- [X] T070 [US4] Stop using fallback commit chains and require one resolved commit for cohorts, resume keys, work items, and attempt IDs in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs` and `TestMap/Services/Experiment/Execution/ExperimentResumeService.cs`
- [X] T071 [US4] Add target and integrity fields, schema `3.0`, and commit-alias invariants in `TestMap/Services/Experiment/Reporting/ExperimentResultFileRow.cs` and `TestMap/Services/Experiment/Reporting/ExperimentResultsWriter.cs`
- [X] T072 [US4] Add pinned provenance and integrity evidence to experiment manifests and result metadata in `TestMap/Services/Experiment/Reporting/`
- [X] T073 [US4] Regenerate `TestMap/schema.sql` and verify it matches the current EF migration model
- [X] T074 [US4] Update analysis schema/normalization for result schema `3.0`, separate repository-family and revision keys, and legacy labeling in `Analysis/src/analysis/schema.py` and `Analysis/src/analysis/normalize.py`
- [X] T075 [US4] Add blocking target, commit, manifest, cohort, integrity, and revision-path audits in `Analysis/src/analysis/audit_evaluation_data.py`
- [X] T076 [US4] Add Python fixtures and audit tests for every valid and blocking schema `3.0` condition under `Analysis/tests/fixtures/pinned_targets/` and `Analysis/tests/test_pinned_target_audits.py`

**Checkpoint**: Pinned experiments are scientifically valid at attempt and result grain. The pilot
gate remains closed until multi-revision behavior and the complete workflow are exercised.

---

## Phase 7: User Story 5 - Evaluate Multiple Revisions Without Collision (Priority: P2)

**Goal**: Evaluate two commits of one repository without workspace, database, artifact, cohort,
resume, result, or analysis-key collisions.

**Independent Test**: Process two commits in either order and concurrently where permitted. Verify
separate persisted evidence and artifacts, rejected cross-commit cohort reuse, and correct family-
versus-revision grouping in analysis.

### Tests for User Story 5

- [X] T077 [P] [US5] Add cohort create/reuse tests proving same-repository different-commit rejection in `TestMap.UnitTests/TestGeneration/CandidateCohortServiceTests.cs`
- [X] T078 [P] [US5] Add resume-key and attempt-ID tests proving resolved-commit participation in `TestMap.UnitTests/TestGeneration/ExperimentResumeServiceTests.cs` and `TestMap.UnitTests/TestGeneration/AttemptKeyFactoryTests.cs`
- [X] T079 [P] [US5] Add analysis key and cross-revision collision audit tests in `Analysis/tests/test_pinned_target_audits.py`
- [X] T080 [US5] Add two-commit sequential and concurrent isolation tests in `TestMap.IntegrationTests/Targets/MultiRevisionIsolationTests.cs`
- [X] T081 [US5] Add an end-to-end local two-revision, two-lane workflow with one simulated revision-moving agent in `TestMap.EndToEndTests/PinnedRepositoryTargetEndToEndTests.cs`

### Implementation for User Story 5

- [X] T082 [US5] Enforce resolved-commit equality in cohort compatibility and snapshots in `TestMap/Services/Experiment/Execution/CandidateCohortService.cs`
- [X] T083 [US5] Include resolved commit in canonical attempt identity and document the key contract in `TestMap/Services/Experiment/Reporting/AttemptKeyFactory.cs`
- [X] T084 [US5] Add revision-isolation and repository-family summaries to normalized evaluation exports in `Analysis/src/analysis/build_evaluation_dataset.py` and `Analysis/src/analysis/summaries.py`
- [X] T085 [US5] Add repository-family versus revision participation tables to `Analysis/notebooks/02_cross_repo_overview.ipynb`

**Checkpoint**: Multiple revisions are isolated and analyzable without losing repository-family
relationships. All user stories are complete.

---

## Phase 8: Polish and Cross-Cutting Pilot Gate

**Purpose**: Validate the complete research workflow and update public guidance before pilot use.

- [X] T086 [P] Update target manifest, verification, exact revision, integrity, schema `3.0`, and legacy compatibility documentation in `README.md`, `Docs/CONFIG.md`, `Docs/HOW_IT_WORKS.md`, and `Docs/HOW_TO_USE.md`
- [X] T087 [P] Add generated pinned-target example configuration and a two-target smoke manifest under `TestMap/Config/` and `TestMap/Data/`
- [X] T088 [P] Add target and integrity troubleshooting guidance, including unavailable commits and dirty workspaces, in `Docs/GETTING_STARTED_CHECKLIST.md`
- [X] T089 Run all .NET unit, integration, and end-to-end tests with `dotnet test TestMap.slnx`
- [X] T090 Run all Python analysis tests and rebuild normalized fixture datasets using commands documented in `Analysis/README.md`
- [X] T091 Execute affected analysis notebooks against the pinned fixture and verify target/revision headline counts under `Analysis/notebooks/`
- [X] T092 Follow every scenario in `specs/001-pinned-repository-targets/quickstart.md` and record observed outcomes in `specs/001-pinned-repository-targets/validation-report.md`
- [X] T093 Run provenance, duplicate-key, missingness, pairing, cohort, integrity, and path-collision audits on the smoke outputs and record the clean audit summary in `specs/001-pinned-repository-targets/validation-report.md`
- [X] T094 Review logs, manifests, reports, SQLite rows, canonical CSVs, and agent artifacts for secret leakage and record the result in `specs/001-pinned-repository-targets/validation-report.md`
- [X] T095 Compare all generated schemas and contracts with `specs/001-pinned-repository-targets/contracts/` and resolve any drift before marking the feature pilot-ready

---

## Dependencies and Execution Order

### Phase Dependencies

- **Phase 1 - Setup**: Starts immediately.
- **Phase 2 - Foundational**: Depends on Setup and blocks every user story.
- **US1 - Manifest Creation**: Depends on Foundational; first independently useful increment.
- **US2 - Sampling-Frame Verification**: Depends on US1 manifest parsing and identity contracts.
- **US3 - Exact Materialization**: Depends on US1 and Foundational; can proceed in parallel with US2
  after the manifest reader is stable.
- **US4 - Continuous Integrity**: Depends on US3 exact base revision and revision-specific paths.
- **US5 - Multi-Revision Isolation**: Depends on US3 and US4 because it validates both storage and
  attempt integrity across revisions.
- **Phase 8 - Pilot Gate**: Depends on every selected user story; all Phase 8 tasks must pass before a
  scientific pilot uses the feature.

### User Story Dependency Graph

```text
Setup -> Foundational -> US1 -> US2
                         |
                         +----> US3 -> US4 -> US5 -> Pilot Gate
```

US2 and US3 may overlap after US1's serializer and target identity contract are stable. US4 and US5
must remain sequential because integrity semantics depend on exact materialization and multi-revision
validation depends on integrity-aware persistence and exports.

### Within Each User Story

1. Add and run the story's tests; confirm new tests fail for the intended missing behavior.
2. Implement models/contracts only when not already foundational.
3. Implement services and persistence.
4. Wire CLI or pipeline integration.
5. Run the story's independent test and the existing full affected test project.
6. Do not advance when a provenance or integrity assertion is bypassed or downgraded.

## Parallel Opportunities

### User Story 1

```text
Parallel: T015 delimiter tests, T016 identity tests, T017 YAML tests, T018 report tests, T019 CLI tests
Then: T021 -> T022 -> T023/T024 -> T025 -> T026
```

### User Story 2

```text
Parallel: T028 verification policy tests, T029 report tests, T030 CLI tests
Then: T032/T033 -> T034 -> T035
```

### User Story 3

```text
Parallel: T037 path tests, T038 source-reader tests, T039 materialization tests, T040 lock tests
Then: T043/T044/T045 -> T046/T047 -> T048/T049/T050 -> T051/T052
```

### User Story 4

```text
Parallel: T054 integrity tests, T055 rollback tests, T056 orchestration tests, T057 mapping tests,
          T058 writer tests
Then: T061/T062/T063/T064/T065 -> T066 -> T067-T075 -> T076
```

### User Story 5

```text
Parallel: T077 cohort tests, T078 identity tests, T079 analysis tests
Then: T080 -> T081 and T082/T083/T084 -> T085
```

## Implementation Strategy

### MVP First

The narrow MVP is **US1 only**: deterministic manifest creation from the supplied repository file.
It is independently valuable for freezing the sampling frame but MUST NOT be used to claim pinned
experiment execution.

1. Complete Setup and Foundational phases.
2. Complete US1 and validate all 1,525 source rows are accounted for.
3. Stop and review the generated manifest and rejection report.

### Incremental Delivery

1. **US1** freezes target identity and source provenance.
2. **US2** makes target availability auditable before resource-intensive runs.
3. **US3** enables exact-revision collection with isolated storage.
4. **US4** enables scientifically valid LLM/agent experiments and result schema `3.0`.
5. **US5** enables longitudinal or multi-revision studies.
6. **Pilot Gate** validates the complete path and analysis interpretation.

### Scientific Readiness

- Manifest-ready after US1.
- Availability-audit-ready after US2.
- Pinned-collection-ready after US3.
- Pinned-experiment implementation-ready after US4, but not pilot-ready.
- Pilot-ready only after US5 and every Phase 8 validation task passes.

## Notes

- `[P]` means different files and no incomplete dependency, not merely that tasks are conceptually
  separable.
- Existing user changes must not be reverted while implementing these tasks.
- Migrations, `schema.sql`, result contracts, and analysis schemas must change together.
- Historical schema `2.0` artifacts remain explicitly legacy; do not fabricate pinned provenance.
- Commit after each coherent task group so high-risk persistence and workspace changes remain
  reviewable.
