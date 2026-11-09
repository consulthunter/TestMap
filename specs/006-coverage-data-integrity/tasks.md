# Tasks: Coverage Data Integrity

**Input**: Design documents from `specs/006-coverage-data-integrity/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md),
[coverage-integrity-contract.md](contracts/coverage-integrity-contract.md),
[quickstart.md](quickstart.md)

**Tests**: Tests are required because this feature changes measurement, outcome classification,
persistence, candidate eligibility, export semantics, and historical interpretation. Write each
phase's tests first and confirm they fail for the intended defect before implementing that phase.

**Minimal-change constraint**: Extend the existing runner, coverage models, three coverage tables,
collector, mapper, repositories, consumers, and project-validation CSV. Do not add a coverage
subsystem, injected service, database table, package dependency, public command, or separate export
pipeline. A single pure counter helper and one runner status sidecar are the only new implementation
artifacts permitted by the plan.

## Safe Validation Constraint

- Use a unique operating-system temporary directory and `--artifacts-path` for every .NET build/test
  session.
- Use isolated temporary SQLite databases and fixture workspaces; never reuse the active TestMap
  database, logs, output, or repository workspaces.
- Generate migrations from a disposable current-source copy if the SDK would touch locked repository
  outputs, then bring back only the intended migration, snapshot, and schema changes.
- Do not overwrite `Replication/FilteredTestRepoCollectTests` or `Replication/MSRValidation`; canary
  and cohort recollection must use new output roots.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and does not depend on an
  incomplete task in the same phase.
- **[Story]**: Maps the task to one of the six user stories in `spec.md`.
- Every task names the exact file or directory it changes or validates.

---

## Phase 1: Setup and Deterministic Fixtures

**Purpose**: Establish safe validation records and small reusable inputs without adding runtime
architecture.

- [X] T001 Create the implementation validation log with disposable-build, isolated-database, canary, cohort, and audit checkpoints in `specs/006-coverage-data-integrity/validation-report.md`
- [X] T002 [P] Add a compact Cobertura fixture containing matched and unmatched classes, normal methods, overloaded `.ctor` entries, `.cctor`, covered/uncovered lines, branch fractions, and malformed optional counters in `TestMap.UnitTests/Fixtures/Coverage/coverage-integrity.cobertura.xml`
- [X] T003 [P] Add empty, all-unmatched, and runner-status JSON fixtures for collection outcome tests in `TestMap.UnitTests/Fixtures/Coverage/empty.cobertura.xml`, `TestMap.UnitTests/Fixtures/Coverage/all-unmatched.cobertura.xml`, and `TestMap.UnitTests/Fixtures/Coverage/collection_status.json`

---

## Phase 2: Foundational In-Place Schema and Model Changes

**Purpose**: Evolve the existing coverage grains so all stories can represent run identity, explicit
status, nullable attribution, raw identity, availability, and reconciliation.

**⚠️ CRITICAL**: No user story implementation begins until this phase passes.

### Tests for the Foundation

> Write these tests first and confirm the current schema/model cannot satisfy them.

- [X] T004 [P] Add migration tests for nullable source IDs, raw identity fields, object-to-member ownership, status/policy fields, availability flags, source ordinals, reconciliation counts, historical `LegacyNotMeasured`, and preserved legacy rows in `TestMap.UnitTests/Persistence/CoverageIntegrityMigrationTests.cs`
- [X] T005 [P] Extend schema snapshot tests for the three evolved coverage tables, new uniqueness constraints, nullable foreign keys, and unchanged coverage-gap ownership in `TestMap.IntegrationTests/Persistence/MigrationSchemaTests.cs`
- [X] T006 [P] Add repository round-trip and update tests for run identity, status metadata JSON, nullable object/member IDs, raw names/signatures, ordinals, availability flags, and report counts in `TestMap.UnitTests/Persistence/CoverageReportRepositoryTests.cs`, `TestMap.UnitTests/Persistence/ObjectCoverageRepositoryTests.cs`, and `TestMap.UnitTests/Persistence/MemberCoverageRepositoryTests.cs`

### Foundation Implementation

- [X] T007 [P] Add `RunId`, collection status/reason, successful collector, metadata JSON, usable flag, availability flags, policy version, and raw/mapped counts to `TestMap/Models/Coverage/CoverageReportModel.cs` and add only the non-XML raw/availability properties required by persistence to `TestMap/Models/Coverage/ObjectCoverageModel.cs` and `TestMap/Models/Coverage/MemberCoverageModel.cs`
- [X] T008 [P] Evolve `CoverageReportEntity`, `ObjectCoverageEntity`, and `MemberCoverageEntity` in `TestMap/Persistence/Ef/Entities/Coverage/CoverageReportEntity.cs`, `TestMap/Persistence/Ef/Entities/Coverage/ObjectCoverageEntity.cs`, and `TestMap/Persistence/Ef/Entities/Coverage/MemberCoverageEntity.cs` with nullable source IDs, raw identity, source ordinal, attribution outcome, availability, report summary, and raw object-to-member relationship fields
- [X] T009 Update columns, nullable foreign keys, unique indexes, and raw object-to-member ownership without adding tables in `TestMap/Persistence/Ef/Configuration/Entities/Coverage/CoverageReportEntityConfiguration.cs`, `TestMap/Persistence/Ef/Configuration/Entities/Coverage/ObjectCoverageEntityConfiguration.cs`, and `TestMap/Persistence/Ef/Configuration/Entities/Coverage/MemberCoverageEntityConfiguration.cs`
- [X] T010 Update existing domain/entity mappings and repository insert/update/change detection for all foundational fields in `TestMap/Persistence/Ef/Mapping/Coverage/CoverageReportMappingExtensions.cs`, `TestMap/Persistence/Ef/Mapping/Coverage/ObjectCoverageMappingExtensions.cs`, `TestMap/Persistence/Ef/Mapping/Coverage/MemberCoverageMappingExtensions.cs`, and `TestMap/Persistence/Ef/Repositories/Coverage/`
- [X] T011 Generate the additive coverage-integrity migration in a disposable source copy, mark existing reports and counter availability as legacy/not measured, and bring back only changes to `TestMap/Migrations/`, `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`, and `TestMap/schema.sql`
- [X] T012 Run the foundation migration and repository tests with isolated SQLite databases and record exact commands/results in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: Existing coverage tables can represent all corrected states without a new table or
service.

---

## Phase 3: User Story 1 - Preserve Every Coverage Observation (Priority: P1) 🎯 MVP

**Goal**: Persist every parsed class/member row before attribution, retain terminal non-match reasons,
and map constructors using existing member matching safeguards.

**Independent Test**: Map the deterministic fixture and verify raw rows exist exactly once before
attribution; matched, unmatched, ambiguous, out-of-project, parent-unmatched, `.ctor`, and `.cctor`
rows all reach explicit outcomes and reconcile to report totals.

### Tests for User Story 1

> Write these tests first and confirm current filtering and constructor exclusion make them fail.

- [X] T013 [P] [US1] Add raw-first persistence, source ordinal, object/member ownership, retry idempotence, and interrupted-`PendingAttribution` tests in `TestMap.UnitTests/TestExecution/MapCoverageServiceTests.cs`
- [X] T014 [US1] Add terminal `Mapped`, `Unmatched`, `Ambiguous`, `OutOfProject`, `Unsupported`, and `ParentUnmatched` reconciliation tests in `TestMap.UnitTests/TestExecution/MapCoverageServiceTests.cs`
- [X] T015 [US1] Add instance constructor, static constructor, overload-by-line, overload-by-parameter-count, and unresolved-tie tests in `TestMap.UnitTests/TestExecution/MapCoverageServiceTests.cs`
- [X] T016 [P] [US1] Add raw identity and nullable-attribution mapping tests in `TestMap.UnitTests/Persistence/ObjectCoverageRepositoryTests.cs` and `TestMap.UnitTests/Persistence/MemberCoverageRepositoryTests.cs`

### Implementation for User Story 1

- [X] T017 [US1] Change `MapCoverageService.MapAsync` to persist the report plus all raw object/member observations with null source IDs and `Pending` outcomes, commit that first save, then attribute the same tracked rows in `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T018 [US1] Replace silent object/member `continue` paths with explicit terminal statuses/reasons, retain children of unmapped objects, and calculate exact raw/mapped reconciliation counts in `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T019 [US1] Remove unconditional `.ctor`/`.cctor` exclusion, require compatible `constructor`/`static_constructor` kinds, and reuse line-overlap then parameter-count disambiguation in `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T020 [US1] Update object/member mapping and repository helpers to create and update raw rows by report/object source ordinal rather than mapped source ID alone in `TestMap/Persistence/Ef/Mapping/Coverage/ObjectCoverageMappingExtensions.cs`, `TestMap/Persistence/Ef/Mapping/Coverage/MemberCoverageMappingExtensions.cs`, `TestMap/Persistence/Ef/Repositories/Coverage/ObjectCoverageRepository.cs`, and `TestMap/Persistence/Ef/Repositories/Coverage/MemberCoverageRepository.cs`
- [X] T021 [US1] Keep coverage-gap replacement strictly mapped-member-only and verify unmatched rows cannot create or delete another member's gaps in `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T022 [US1] Run the User Story 1 fixture and persistence tests with a disposable artifacts root and record reconciliation examples in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: User Story 1 is independently complete; every parsed observation is durable and
constructors are no longer structurally excluded.

---

## Phase 4: User Story 2 - Report Truthful Collection Outcomes (Priority: P1)

**Goal**: Persist explicit collection outcomes and make `HasCoverage` mean usable corrected
in-project coverage rather than report-row existence.

**Independent Test**: Exercise unavailable provider metadata, missing artifacts, parse failure,
parsed-empty, all-unmatched, partial mapping, and mapped coverage; verify status/reason, report
persistence, `HasUsableCoverage`, test-run linking, and CSV values.

### Tests for User Story 2

> Write these tests first and confirm current empty placeholder and `AnyAsync` behavior fails them.

- [X] T023 [P] [US2] Add missing-sidecar, missing-artifact, parse-failure, parsed-empty, valid-report, run-ID, and sidecar-metadata collection tests in `TestMap.UnitTests/TestExecution/Collection/CollectCoverageResultsServiceTests.cs`
- [X] T024 [P] [US2] Add report lookup/update/link tests proving `(ProjectId, RunId)` identity and status changes do not collide on timestamp zero in `TestMap.UnitTests/Persistence/CoverageReportRepositoryTests.cs`
- [X] T025 [P] [US2] Add collection-to-mapping tests proving failure reports are persisted, usable reports are linked to their test run, and test outcome remains separate in `TestMap.UnitTests/TestExecution/BuildTestResultCollectorTests.cs`
- [X] T026 [P] [US2] Add project-validation tests for false report-only claims, parsed-empty/all-unmatched rows, partial/mapped success, legacy blanks, and appended coverage contract columns in `TestMap.UnitTests/TestExecution/CollectTestsResultWriterTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] Read `collection_<run-id>.json`, preserve its raw JSON, synthesize explicit missing-sidecar/no-artifact/parse-failure reports, set corrected policy/run identity, and stop returning an indistinguishable empty success in `TestMap/Services/TestExecution/Collection/CollectCoverageResultsService.cs`
- [X] T028 [US2] Persist every explicit coverage outcome through the existing mapper and keep collection/mapping failures visible without changing mutation collection flow in `TestMap/Services/TestExecution/BuildTestResultCollector.cs`
- [X] T029 [US2] Finalize report status after attribution as `ParsedNoData`, `ParsedNoUsableCoverage`, `PartiallyMapped`, or `Mapped`; set `HasUsableCoverage` and child reconciliation counts from persisted rows in `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T030 [US2] Change report insert/update and test-run linking from project/timestamp lookup to project/run-ID lookup while retaining legacy reads in `TestMap/Persistence/Ef/Repositories/Coverage/CoverageReportRepository.cs` and `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T031 [US2] Append status, reason, policy, and raw/mapped object/member counts to `ProjectValidationResult` and the existing CSV while deriving `HasCoverage` from the latest corrected report's usable flag in `TestMap/Models/Results/ProjectValidationResult.cs` and `TestMap/Services/TestExecution/Collection/CollectTestsResultWriter.cs`
- [X] T032 [US2] Run the User Story 2 collector, mapper, repository, linker, and CSV tests and record one example of every terminal status in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: Report existence can no longer produce a false `HasCoverage` claim.

---

## Phase 5: User Story 3 - Recover Through Provider Fallback (Priority: P1)

**Goal**: Attempt the remaining built-in collector after an unavailable, failed, or artifact-less
preferred collector while retaining each attempt and preserving test outcome separately.

**Independent Test**: Simulate preferred-provider unavailable/nonzero/no-artifact and fallback
success, both providers failing, and preferred success; verify order, stopping, artifact ownership,
return semantics, and sidecar attempt evidence.

### Tests for User Story 3

> Write these tests first and confirm current explicit-collector and nonzero-return behavior fails.

- [X] T033 [P] [US3] Add runner tests for explicit XPlat-to-Microsoft fallback, explicit Microsoft-to-XPlat fallback, nonzero preferred return with no artifact, and no fallback after preferred artifact success in `TestMap/Docker/validation/runner/test/test_cli.py`
- [X] T034 [US3] Add runner tests for ordered provider attempt records, separate test return code, no-artifact/all-failed terminal status, and sidecar creation on every normal exit path in `TestMap/Docker/validation/runner/test/test_cli.py`

### Implementation for User Story 3

- [X] T035 [US3] Extend the existing runner command result with ordered collector-attempt and current-run artifact records using only local dataclasses in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T036 [US3] Build collector order from the requested collector followed by remaining built-ins, continue after nonzero return when no current-run artifact exists, and stop after artifact success in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T037 [US3] Preserve provider return codes, TRX paths, artifact paths, collector identity, framework, and target while keeping final test command outcome distinct from coverage availability in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T038 [US3] Write `coverage-collection-v1` atomically for success, fallback success, provider unavailable, collection failure, and no-artifact outcomes from `run_dotnet_tests` and `run_dotnet_test_project` in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T039 [US3] Run the User Story 3 Python runner suite and record the exact provider order and sidecar examples in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: Baseline and targeted collection both recover through the existing default providers
without a new provider subsystem or public option.

---

## Phase 6: User Story 4 - Retain Complete Measurements (Priority: P2)

**Goal**: Populate exact covered/valid line and branch counters, preserve availability, and prevent
legacy/unmatched rows from affecting mapped consumers.

**Independent Test**: Parse deterministic line/condition fixtures, persist and reload exact counts,
then run candidate, risk, evidence, gap, and before/after queries with corrected, unmatched, and legacy
rows to verify only valid mapped evidence is consumed.

### Tests for User Story 4

> Write these tests first and confirm existing zeroed entity counters and unrestricted queries fail.

- [X] T040 [P] [US4] Add pure counter tests for distinct lines, positive hits, zero hits, condition fractions, child-condition fallback, no branches, duplicate details, malformed values, and covered-greater-than-valid rejection in `TestMap.UnitTests/TestExecution/CoverageCounterCalculatorTests.cs`
- [X] T041 [P] [US4] Add object/member mapping and repository tests for exact counters, line/branch availability, observed zero, unavailable legacy values, and update detection in `TestMap.UnitTests/Persistence/ObjectCoverageRepositoryTests.cs` and `TestMap.UnitTests/Persistence/MemberCoverageRepositoryTests.cs`
- [X] T042 [P] [US4] Add corrected-policy/null-ID/availability regression cases to candidate selection and metric scoring in `TestMap.UnitTests/TestGeneration/CandidateMethodSelectorTests.cs`, `TestMap.UnitTests/TestGeneration/MetricDrivenCandidateSelectionStrategyTests.cs`, and `TestMap.UnitTests/TestGeneration/MethodSelectionServiceContextMappingTests.cs`
- [X] T043 [P] [US4] Add corrected-policy/null-ID/availability regression cases to risk, generation evidence, and baseline/post comparison in `TestMap.UnitTests/RiskScoring/RiskFactorProviderTests.cs`, `TestMap.UnitTests/TestGeneration/GenerationEvidenceServiceTests.cs`, and `TestMap.UnitTests/Experiment/Execution/AttemptMetricComparisonServiceTests.cs`

### Implementation for User Story 4

- [X] T044 [US4] Implement one non-injected pure line/branch counter calculator with availability and validation results in `TestMap/Persistence/Ef/Mapping/Coverage/CoverageCounterCalculator.cs`
- [X] T045 [US4] Populate existing object/member counter columns and availability flags in domain/entity conversion, repository `Apply`, and `HasChanged` paths in `TestMap/Persistence/Ef/Mapping/Coverage/ObjectCoverageMappingExtensions.cs`, `TestMap/Persistence/Ef/Mapping/Coverage/MemberCoverageMappingExtensions.cs`, `TestMap/Persistence/Ef/Repositories/Coverage/ObjectCoverageRepository.cs`, and `TestMap/Persistence/Ef/Repositories/Coverage/MemberCoverageRepository.cs`
- [X] T046 [US4] Populate root counter availability from parsed report attributes and reject impossible root/entity counter combinations without inferring denominators from rates in `TestMap/Services/TestExecution/Collection/CollectCoverageResultsService.cs` and `TestMap/Services/TestExecution/Mapping/MapCoverageService.cs`
- [X] T047 [P] [US4] Require corrected usable reports and non-null mapped member IDs in candidate/method selection and metric scoring queries in `TestMap/Services/TestGeneration/TargetSelection/CandidateMethodSelector.cs`, `TestMap/Services/TestGeneration/TargetSelection/Strategies/MetricDrivenCandidateSelectionStrategy.cs`, and `TestMap/Services/TestGeneration/TargetSelection/MethodSelectionService.cs`
- [X] T048 [P] [US4] Require corrected usable reports, non-null mapped member IDs, and available counters where arithmetic needs them in `TestMap/Services/RiskScoring/CoverageGapRiskFactorProvider.cs`, `TestMap/Services/RiskScoring/TestGapRiskFactorProvider.cs`, and `TestMap/Services/TestGeneration/Evidence/GenerationEvidenceService.cs`
- [X] T049 [P] [US4] Require corrected usable reports and mapped member IDs in before/after and orchestration coverage queries in `TestMap/Services/Experiment/Execution/AttemptMetricComparisonService.cs` and `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T050 [US4] Run the User Story 4 counter, persistence, candidate, risk, evidence, and comparison tests and record exact counter reconciliation examples in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: Corrected rows retain complete measurements, observed zero is distinguishable from
unavailable, and unmatched/legacy rows cannot enter mapped metrics.

---

## Phase 7: User Story 5 - Merge Only Intended Coverage Evidence (Priority: P2)

**Goal**: Merge every distinct current-run artifact even when filenames repeat, exclude stale output,
and audit content-and-scope duplicate decisions.

**Independent Test**: Provide two distinct same-name current artifacts, one same-scope byte duplicate,
one identical artifact from a different scope, one stale artifact, and prior merged outputs; verify
the exact merge input and duplicate lists.

### Tests for User Story 5

> Write these tests first and confirm filename deduplication and directory rescanning fail them.

- [X] T051 [P] [US5] Replace the filename-dedupe expectation with tests for content-and-scope identity, distinct same-name retention, cross-scope identity retention, and same-scope duplicate reporting in `TestMap/Docker/validation/runner/test/test_cli.py`
- [X] T052 [US5] Add merge tests proving only artifact records returned by the current invocation are inputs and stale, prior raw merge, prior normalized merge, and report-generator outputs are excluded in `TestMap/Docker/validation/runner/test/test_cli.py`
- [X] T053 [US5] Add sidecar reconciliation tests for artifact SHA-256 values, merge input order, duplicate references, raw output, normalized output, and merge failure in `TestMap/Docker/validation/runner/test/test_cli.py`

### Implementation for User Story 5

- [X] T054 [US5] Return exact recent TRX and Cobertura paths from collector attempts instead of counts alone and accumulate those artifact records across solutions/projects in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T055 [US5] Replace `dedupe_by_name` with deterministic SHA-256 plus normalized target/framework scope comparison, retaining distinct same-name and cross-scope artifacts in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T056 [US5] Change `merge_coverage_reports` to accept only accumulated current-run artifact records, preserve deterministic input order, and never recursively rescan the coverage directory in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T057 [US5] Complete the sidecar merge section atomically with inputs, duplicates, outputs, status, and reason for merge/report-generator outcomes in `TestMap/Docker/validation/runner/src/testmap_runner/cli.py`
- [X] T058 [US5] Run the User Story 5 merge suite and record the same-name, duplicate, and stale-artifact evidence in `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: Merge input is lossless for the current run and cannot be contaminated by prior
artifacts.

---

## Phase 8: User Story 6 - Publish a Corrected Validation Cohort (Priority: P3)

**Goal**: Audit corrected semantics, validate the failure and success canaries, then recollect all 344
selected pinned repositories without fabricating historical evidence.

**Independent Test**: Run the existing MSR coverage audit against legacy and corrected fixture
databases, execute the two pinned canaries, and then recollect the full selected manifest; verify
terminal statuses, reconciliation, counters, provenance, and zero false `HasCoverage` claims.

### Tests for User Story 6

> Write these tests first and confirm the legacy audit cannot interpret the corrected schema.

- [X] T059 [P] [US6] Add legacy/corrected database audit tests for policy, status, usable flag, nullable IDs, pending/nonterminal rows, reason completeness, reconciliation, counter availability, impossible counters, and false `HasCoverage` claims in `Analysis/tests/test_msr_coverage_audit.py`
- [X] T060 [P] [US6] Add MSR frame tests proving raw/mapped observation grain is retained, nullable source IDs survive export, legacy counters remain unavailable, and corrected policy/status/provenance fields are emitted in `Analysis/tests/test_build_msr_datasets.py`

### Implementation for User Story 6

- [X] T061 [US6] Update the existing coverage audit to read both legacy and corrected schemas, use the corrected status contract, reconcile child rows, validate counters, and block publication defects in `Analysis/src/analysis/msr_coverage_audit.py`
- [X] T062 [US6] Update the existing MSR dataset builder to export raw and mapped coverage observations with nullable source IDs, availability, attribution status/reason, report policy/status, and run provenance in `Analysis/src/analysis/build_msr_datasets.py`
- [X] T063 [US6] Generate a fresh two-target pinned manifest for `microsoft/artifacts-credprovider@df94ab890995c300ca293d3e8f06a3dad77fdff2` and `nbuilder/nbuilder@2769d20e112b56201873ddd3397e7dd8dd3e93a6`, verify its exact commits, and record the temporary manifest path/hash in `specs/006-coverage-data-integrity/validation-report.md`
- [ ] T064 [US6] Run the `microsoft/artifacts-credprovider` canary into a fresh output root, verify required fallback and truthful final status whether or not fallback yields coverage, and record DB/sidecar/log/CSV evidence in `specs/006-coverage-data-integrity/validation-report.md`
- [ ] T065 [US6] Run the `nbuilder/nbuilder` canary into the same fresh validation campaign, verify usable coverage, exact counters, constructor observations, and raw/mapped reconciliation, and record evidence in `specs/006-coverage-data-integrity/validation-report.md`
- [ ] T066 [US6] Recollect all 344 selected repositories from their pinned commits into a new output/database root without overwriting historical runs and record manifest, configuration, environment, start/end times, and terminal target outcomes in `specs/006-coverage-data-integrity/validation-report.md`
- [ ] T067 [US6] Regenerate corrected `msr_coverage.csv` and coverage audit artifacts, verify all 344 terminal statuses and every `HasCoverage = true` row, and record frame/audit hashes and defect counts in `Replication/MSRValidation/audit/` and `specs/006-coverage-data-integrity/validation-report.md`

**Checkpoint**: The corrected cohort is reproducible, internally reconciled, and separated from
legacy coverage semantics.

---

## Phase 9: Polish and Cross-Cutting Validation

**Purpose**: Complete documentation, full regression, contract inspection, and constitution review.

- [X] T068 [P] Update coverage collection, fallback, status, nullable attribution, counter availability, constructor, and historical-policy documentation in `README.md` and `Analysis/msr_validation_bugs.md`
- [X] T069 [P] Re-run every focused command in `specs/006-coverage-data-integrity/quickstart.md`, correct stale filters or expectations there, and record results in `specs/006-coverage-data-integrity/validation-report.md`
- [X] T070 Run `dotnet test .\TestMap.slnx` with a unique temporary `--artifacts-path` and record project/test totals and failures in `specs/006-coverage-data-integrity/validation-report.md`
- [X] T071 Run the full Python runner suite and `Analysis` pytest suite with isolated temporary inputs and record totals and failures in `specs/006-coverage-data-integrity/validation-report.md`
- [ ] T072 Inspect one corrected failure report, one partial report, and one mapped report across sidecar, SQLite rows, project-validation CSV, and MSR frame; record exact reconciliation and provenance evidence in `specs/006-coverage-data-integrity/validation-report.md`
- [X] T073 Complete a constitution-focused review of row grain, pinned identity, fallback provenance, artifact isolation, missingness, mapped-only consumers, historical compatibility, and publication blockers in `specs/006-coverage-data-integrity/validation-report.md`
- [X] T074 Confirm the delivered implementation added no new coverage table, injected service, repository family, package dependency, public CLI option, or standalone export pipeline and document any unavoidable deviation in `specs/006-coverage-data-integrity/validation-report.md`

---

## Dependencies and Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: Starts immediately and provides deterministic fixtures and a validation log.
- **Phase 2 — Foundation**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 — US1**: Depends on Foundation and establishes raw-first persistence and attribution.
- **Phase 4 — US2**: Depends on US1 because usable status is derived after attribution.
- **Phase 5 — US3**: Depends on Foundation; it can proceed alongside US1/US2 after shared models exist.
- **Phase 6 — US4**: Depends on US1 and Foundation; it can proceed alongside US2/US3 once raw rows
  exist.
- **Phase 7 — US5**: Depends on US3's provider-attempt/artifact records.
- **Phase 8 — US6**: Depends on US1–US5 and their focused tests.
- **Phase 9 — Polish**: Depends on all implemented stories and desired cohort validation.

### User Story Dependency Graph

```text
Setup
  -> Foundation
      -> US1 Preserve observations
          -> US2 Truthful outcomes
          -> US4 Complete counters / mapped consumers
      -> US3 Provider fallback
          -> US5 Current-run merge integrity
      -> US1 + US2 + US3 + US4 + US5
          -> US6 Corrected cohort
              -> Polish / full audit
```

### User Story Independence

- **US1**: Testable with one parsed report and isolated SQLite database; no runner change required.
- **US2**: Testable with status/sidecar fixtures and persisted US1 rows; no live repository run
  required.
- **US3**: Testable entirely with mocked runner commands and temporary artifacts.
- **US4**: Testable with deterministic Cobertura line/condition fixtures and isolated consumer data.
- **US5**: Testable with temporary same-name, duplicate, and stale artifact files.
- **US6**: Testable first with fixture databases, then the two pinned canaries, and finally the frozen
  344-repository manifest.

### Within Each Phase

- Write and run the listed failing tests before implementation.
- Complete model/entity changes before migration generation.
- Persist raw rows before terminal attribution.
- Complete provider attempt capture before merge refactoring.
- Complete all focused tests and both canaries before starting the 344-repository recollection.
- Do not mark a checkpoint complete from report-row existence or zero-filled counters alone.

## Parallel Opportunities

- T002 and T003 can run in parallel after T001.
- T004–T006 can run in parallel; T007 and T008 can then run in parallel before T009–T011 converge.
- T013 can run in parallel with repository test T016; T014–T015 then extend the same mapper test file
  before sequential mapper changes T017–T021.
- US2 test tasks T023–T026 can run in parallel before collector/mapper/repository/writer integration.
- T033–T034 are sequential because both extend the same runner test file.
- US4 test tasks T040–T043 can run in parallel; mapped-consumer implementations T047–T049 can run in
  parallel after counter semantics stabilize.
- T051–T053 are sequential because all three extend the same runner test file.
- US6 fixture tests T059–T060 can run in parallel; canary execution remains sequential so their
  evidence shares one controlled campaign.
- Documentation T068 and focused quickstart validation T069 can run in parallel after implementation.

## Parallel Example: User Story 1

```text
Task T013: raw-first/idempotence tests in MapCoverageServiceTests.cs
Task T016: repository raw-identity tests
```

Then add T014 and T015 sequentially because they share `MapCoverageServiceTests.cs` with T013.

## Parallel Example: User Story 2

```text
Task T023: collection outcome tests
Task T024: report identity/link tests
Task T025: BuildTestResultCollector integration tests
Task T026: project-validation CSV contract tests
```

## Parallel Example: User Story 3

```text
US3 task sequence T033–T039 can run alongside the US1/US2 branch after Foundation; T033 and T034
remain sequential because they share test_cli.py.
```

## Parallel Example: User Story 4

```text
Task T040: pure counter tests
Task T041: persistence/availability tests
Task T042: candidate and metric consumer tests
Task T043: risk, evidence, and comparison tests
```

## Parallel Example: User Story 5

```text
US5 task sequence T051–T058 can run alongside US4 after US3 completes; T051–T053 remain sequential
because they share test_cli.py.
```

## Parallel Example: User Story 6

```text
Task T059: corrected coverage audit tests
Task T060: corrected MSR frame tests
```

## Implementation Strategy

### MVP First: User Story 1

1. Complete deterministic fixtures and the in-place schema foundation.
2. Write failing raw-persistence, attribution, and constructor tests.
3. Refactor only `MapCoverageService` and existing coverage mappings/repositories.
4. Validate exact raw-to-terminal reconciliation in isolated SQLite.
5. Stop and review the row grain before changing collection status, runner behavior, or consumers.

### Incremental Delivery

1. **US1**: Stop silently dropping parsed observations and restore constructor attribution.
2. **US2**: Make collection outcomes and `HasCoverage` truthful.
3. **US3**: Restore preferred-to-default provider fallback.
4. **US4**: Populate exact counters and constrain mapped consumers.
5. **US5**: Make current-run merging lossless and isolated.
6. **US6**: Validate canaries and publish a corrected 344-repository cohort.
7. **Polish**: Run full regression, cross-artifact inspection, and constitution review.

### Minimal-Change Guardrails

1. Prefer adding properties and branches to existing types over creating new runtime types.
2. Keep the status sidecar as plain JSON stored verbatim on the existing report row.
3. Keep counter calculation a pure static helper with no registration or injected dependencies.
4. Keep raw and mapped coverage in the existing child rows; nullable IDs are attribution, not a new
   evidence table.
5. Keep the existing CSV and append fields; do not create a second validation exporter.
6. Require an explicit plan amendment before adding any table, service, repository family, package,
   public option, or new pipeline.

## Notes

- `[P]` tasks modify separate files or independent test areas and have no unmet same-phase dependency.
- `[US1]` through `[US6]` map directly to the six specification stories.
- Tests must fail for the intended missing behavior before implementation and pass at each checkpoint.
- Historical zero counters remain unavailable; no migration task may infer them from rates.
- Test failure, provider failure, coverage availability, parse outcome, and attribution remain separate.
- Constructors are covered source members but remain excluded by the existing `member.Kind == "method"`
  candidate rule.
- Do not commit canary/cohort runtime logs, temporary databases, cloned workspaces, or build artifacts.
