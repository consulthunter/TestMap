# Tasks: Readable Log Directories

**Input**: Design documents from `specs/004-readable-log-directories/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. Log paths retain audit and failure evidence, and this change must prove that
distinct runs cannot merge logs and that persisted and pinned identities remain unchanged.

**Organization**: Tasks are grouped by user story so readable naming, collision-safe retention, and
compatibility guarantees can each be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it affects a different file and has no dependency on an
  incomplete task in the same phase.
- **[Story]**: Maps the task to a user story from spec.md.
- Every task includes a concrete repository path.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish deterministic time and filesystem fixtures before production behavior
changes.

- [x] T001 Create fixed UTC instants, repository labels, temporary-root helpers, and cleanup conventions in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryTestData.cs`
- [x] T002 [P] Document reservation-marker and synthetic pre-existing-directory fixture conventions in `TestMap.UnitTests/Fixtures/ReadableLogDirectories/README.md`
- [x] T003 [P] Add reusable project-model construction helpers for legacy and pinned logging cases in `TestMap.UnitTests/Models/ProjectModelLogTestData.cs`

---

## Phase 2: Foundational (Blocking Filesystem Contract)

**Purpose**: Define the safe allocator boundary, path invariants, and result model shared by every
story.

**CRITICAL**: No user-story implementation begins until the allocator contract rejects unsafe roots,
non-UTC timestamps, and invalid repository labels.

### Foundational Tests

> Write these tests first and confirm they fail for the missing allocator contract.

- [x] T004 [P] Add contract tests for UTC-only timestamps, configured-root containment, empty roots, and invalid owner/repository path segments in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorTests.cs`
- [x] T005 [P] Add result-model tests for selected directory, reservation marker, log file, captured instant, and ordinal invariants in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocationTests.cs`

### Foundational Implementation

- [x] T006 Define the immutable allocation result and allocator contract in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T007 Implement root normalization, safe repository-segment validation, UTC validation, and path-containment guards in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T008 Run and keep green all foundational logging contract tests in `TestMap.UnitTests/TestMap.UnitTests.csproj`

**Checkpoint**: The allocator boundary is deterministic, portable, and cannot escape the configured
log root.

---

## Phase 3: User Story 1 - Identify A Log Run By Time (Priority: P1) MVP

**Goal**: Create legacy log paths as
`YYYY-MM-DD/HH-mm-ss_<owner>-<repository>/<ProjectId>.log` from one captured UTC run instant.

**Independent Test**: Initialize a legacy project at `2026-07-16T14:05:09Z` and verify the exact
readable path, zero-padding, absence of a random directory prefix, and stable reuse on a second call.

### Tests For User Story 1

> Write these tests first and confirm they fail for the expected missing behavior.

- [x] T009 [P] [US1] Add exact path-format tests for year-month-day, hour-minute-second, owner/repository labels, zero-padding, and no ordinal on the first allocation in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorTests.cs`
- [x] T010 [P] [US1] Add project-model tests proving the random `ProjectId` is absent from the directory leaf but retained in the log filename in `TestMap.UnitTests/Models/ProjectModelLogDirectoryTests.cs`
- [x] T011 [P] [US1] Add project-model tests proving repeated `EnsureProjectLogDir` calls reuse one selected directory, path, and logger association in `TestMap.UnitTests/Models/ProjectModelLogDirectoryTests.cs`
- [x] T012 [P] [US1] Add configuration tests proving one UTC run-start instant is shared by every legacy project and derives both date and time without midnight disagreement in `TestMap.UnitTests/Configuration/ConfigurationServiceTests.cs`

### Implementation For User Story 1

- [x] T013 [US1] Implement base candidate formatting and first-directory allocation in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T014 [US1] Add immutable `RunStartedAtUtc` capture plus a deterministic test construction seam while preserving existing `ProjectId` generation in `TestMap/Models/ProjectModel.cs`
- [x] T015 [US1] Route the legacy branch of `EnsureProjectLogDir` through the allocator, preserve `<ProjectId>.log`, and make repeated initialization idempotent in `TestMap/Models/ProjectModel.cs`
- [x] T016 [US1] Capture one UTC run-start instant in configuration, preserve the existing `RunDate` contract, and pass the instant into every project model in `TestMap/Services/Configuration/ConfigurationService.cs`
- [x] T017 [US1] Expose the captured run-start instant without changing existing consumers in `TestMap/Services/Configuration/IConfigurationService.cs`
- [x] T018 [US1] Add a configuration integration test that creates two repository models and verifies the shared date/time parent with distinct repository leaves in `TestMap.IntegrationTests/Configuration/ReadableLogDirectoryIntegrationTests.cs`
- [x] T019 [US1] Run all US1 allocator, project-model, configuration, and integration tests from `TestMap.UnitTests/TestMap.UnitTests.csproj` and `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`

**Checkpoint**: A normal legacy run has a readable UTC directory, retains its existing log filename,
and does not allocate again when initialized twice.

---

## Phase 4: User Story 2 - Preserve Distinct Runs Without Random Names (Priority: P2)

**Goal**: Keep same-second runs isolated with exclusive reservation and deterministic `-02`, `-03`
ordinals, including across concurrent processes.

**Independent Test**: Start at least 100 allocator instances for one repository, timestamp, and root
concurrently; verify 100 distinct directories from the unsuffixed base through `-100`, one retained
marker per directory, and no overwritten log.

### Tests For User Story 2

> Write these tests first and confirm they fail for the expected missing collision behavior.

- [x] T020 [P] [US2] Add tests proving pre-existing base and ordinal directories are skipped without modification and allocation chooses the first available deterministic ordinal in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorTests.cs`
- [x] T021 [P] [US2] Add tests for `-02`, `-09`, `-10`, `-99`, and `-100` ordinal formatting and monotonic allocation in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorTests.cs`
- [x] T022 [P] [US2] Add a 100-way concurrent allocation test proving distinct directories and one exclusive reservation marker per winner in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorConcurrencyTests.cs`
- [x] T023 [P] [US2] Add failure-injection tests proving partial reservations remain visible and later runs advance rather than delete or reuse evidence in `TestMap.UnitTests/Services/Logging/ProjectLogDirectoryAllocatorFailureTests.cs`
- [x] T024 [P] [US2] Add integration tests proving distinct project models starting in one second create separate logs without merged content in `TestMap.IntegrationTests/Configuration/ReadableLogDirectoryIntegrationTests.cs`

### Implementation For User Story 2

- [x] T025 [US2] Implement deterministic ordinal probing that treats every existing candidate as occupied in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T026 [US2] Implement exclusive `.testmap-log-reservation` ownership and lost-race retry behavior in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T027 [US2] Retain successful and partial reservations on allocation or logger failure and return clear failure context in `TestMap/Services/Logging/ProjectLogDirectoryAllocator.cs`
- [x] T028 [US2] Integrate reservation ownership with legacy logger creation without opening another run's file in `TestMap/Models/ProjectModel.cs`
- [x] T029 [US2] Run all US2 ordinal, concurrency, failure-retention, and integration tests and record the 100-allocation elapsed time in `specs/004-readable-log-directories/validation-report.md`

**Checkpoint**: Same-second and concurrent runs cannot share, overwrite, or silently reuse one
another's audit evidence, and final directory names contain no random value.

---

## Phase 5: User Story 3 - Keep Pinned And Persisted Identity Stable (Priority: P3)

**Goal**: Prove that readable directories change no pinned workspace/database/artifact path,
persisted identity, output contract, data contract, or historical directory.

**Independent Test**: Run pinned-path, project mapping, collection, and historical-directory
fixtures and verify both legacy and pinned logs use the readable convention while exact revision
provenance and non-log paths remain stable.

### Tests For User Story 3

> Write these tests first and confirm they guard the existing compatibility contracts.

- [x] T030 [P] [US3] Add tests proving pinned `MaterializedRevision.Paths.LogPath` uses readable allocation with `run.log` while non-log materialized paths remain revision-scoped in `TestMap.UnitTests/Models/ProjectModelLogDirectoryTests.cs`
- [x] T031 [P] [US3] Extend target-path tests to lock readable pinned log paths and existing repository/commit workspace, database, and artifact contracts in `TestMap.UnitTests/Targets/TargetPathResolverTests.cs`
- [x] T032 [P] [US3] Add mapping tests proving `ProjectId`, persisted run ID, database identity, and output path do not depend on the readable directory label in `TestMap.UnitTests/Persistence/ProjectMappingExtensionsTests.cs`
- [x] T033 [P] [US3] Add tests proving existing random-prefixed historical directories are neither renamed, modified, nor selected for a new run in `TestMap.UnitTests/Models/ProjectModelLogDirectoryTests.cs`
- [x] T034 [P] [US3] Extend the collection end-to-end test to assert an actual readable legacy log path and unchanged persisted test-run identity in `TestMap.EndToEndTests/CollectTestsEndToEndTests.cs`

### Implementation For User Story 3

- [x] T035 [US3] Route the pinned branch of `EnsureProjectLogDir` through readable allocation, update in-memory log provenance, and preserve non-log revision paths in `TestMap/Models/ProjectModel.cs`
- [x] T036 [US3] Preserve existing project mapping and test-run mapping behavior while adapting any log-path assertions in `TestMap/Persistence/Ef/Mapping/ProjectMappingExtensions.cs` and `TestMap/Persistence/Ef/Mapping/Testing/TestRunMappingExtensions.cs`
- [x] T037 [US3] Run all US3 pinned-path, persistence, historical-directory, and collection tests from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`

**Checkpoint**: Pinned and persisted scientific identities retain their exact meaning; only new
legacy log directory names become readable.

---

## Phase 6: Polish And Cross-Cutting Validation

**Purpose**: Finish registration, documentation, performance evidence, and the full regression gate.

- [x] T038 Register the log directory allocator with the existing project-service composition in `TestMap/Services/ServiceCollectionExtensions.cs`
- [x] T039 [P] Document `Logs/YYYY-MM-DD/HH-mm-ss_owner-repository/` and UTC/ordinal semantics in `Docs/CONFIG.md`
- [x] T040 [P] Add operator examples for readable legacy logs, retained reservation markers, and pinned-path differences in `Docs/HOW_TO_USE.md`
- [x] T041 [P] Update setup guidance to distinguish configurable log roots from the fixed UTC relative naming contract in `Docs/SETUP.md`
- [x] T042 Run the complete unit, integration, and end-to-end suites from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`
- [x] T043 Execute every scenario in `specs/004-readable-log-directories/quickstart.md` and record exact paths, collision counts, elapsed time, and test totals in `specs/004-readable-log-directories/validation-report.md`
- [x] T044 Audit generated directories for random/GUID components, duplicate ownership, missing markers, merged logs, date/time disagreement, and path escape; record results in `specs/004-readable-log-directories/validation-report.md`
- [x] T045 Run `git diff --check`, scan for stale random-directory assumptions, confirm no database/schema/result contract changed, and record the explicit no-migration decision in `specs/004-readable-log-directories/validation-report.md`

---

## Dependencies And Execution Order

### Phase Dependencies

- **Phase 1, Setup**: Starts immediately. T002 and T003 can run in parallel after fixture naming is
  agreed.
- **Phase 2, Foundation**: Depends on Setup and blocks all user stories.
- **Phase 3, US1**: Depends on the safe allocator boundary and delivers the MVP readable path.
- **Phase 4, US2**: Extends the US1 allocator with collision and cross-process ownership.
- **Phase 5, US3**: Can begin its compatibility tests after Foundation, but final validation depends
  on US1 and US2 behavior being complete.
- **Phase 6, Polish**: Depends on all desired stories and is the completion gate.

### User Story Dependencies

```text
Safe allocator foundation
   -> US1 readable UTC paths
       -> US2 deterministic collision ownership
           -> US3 integrated compatibility proof
               -> Full validation
```

- **US1 (P1)**: Independently testable after Foundation and is the suggested MVP.
- **US2 (P2)**: Depends on US1 base candidate formatting but is independently verifiable through
  allocator contention tests.
- **US3 (P3)**: Compatibility tests can be authored in parallel, but its end-to-end claim requires
  completed US1/US2 integration.

### Within Each User Story

- Write the listed tests first and verify they fail for the intended missing behavior.
- Define runtime state before integrating it into `ProjectModel` or configuration.
- Implement pure path behavior before filesystem reservation behavior.
- Complete focused unit tests before integration and end-to-end tests.
- Preserve the pinned branch and persisted identities throughout all legacy-path edits.

## Parallel Opportunities

### User Story 1

```text
Parallel tests: T009 path grammar, T010/T011 project model, T012 configuration
Sequential implementation: T013 -> T014 -> T015 -> T016/T017 -> T018
```

### User Story 2

```text
Parallel tests: T020 pre-existing paths, T021 ordinals, T022 concurrency, T023 failure retention
Sequential allocator work: T025 -> T026 -> T027 -> T028
Integration test T024 can be completed after T028
```

### User Story 3

```text
Parallel tests: T030 project pinned branch, T031 target paths, T032 persistence, T033 history,
T034 collection end-to-end
Compatibility implementation: T035 -> T036 -> T037
```

## Implementation Strategy

### MVP First

1. Complete Setup and Foundation.
2. Complete US1 readable UTC paths and idempotent project logging.
3. Validate exact `YYYY-MM-DD/HH-mm-ss_owner-repository` output with fixed-time tests.

The MVP is usable for sequential runs, but the full feature is not complete until US2 prevents
same-second and cross-process evidence collisions.

### Incremental Delivery

1. **US1**: Human-readable date/time and repository path.
2. **US2**: Deterministic ordinal and exclusive ownership under contention.
3. **US3**: Pinned, persistence, historical, and end-to-end compatibility proof.
4. **Polish**: Documentation, performance evidence, audits, and complete regressions.

### Completion Gate

Do not treat the feature as ready until T042-T045 confirm:

- new legacy paths match the documented UTC grammar;
- 100 same-second allocations remain distinct and complete within the target time;
- every selected directory retains exactly one reservation marker;
- repeated initialization remains idempotent;
- old directories and partial reservations are untouched;
- pinned non-log paths, revision provenance, and persisted IDs remain unchanged;
- no database, CSV, manifest, experiment-result, or notebook contract changed.

## Task Format Validation

All 45 tasks use the required checkbox, sequential task ID, optional `[P]` marker, required
user-story label inside story phases, concrete action, and exact repository path.
