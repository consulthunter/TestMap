# Tasks: Non-Trivial Assertion Detection

**Input**: Design documents from `specs/005-non-trivial-assertions/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [assertion-lineage-contract.md](contracts/assertion-lineage-contract.md)

**Tests**: Tests are required because this feature changes measurement, classification, persistence,
export grain, audit behavior, and scientific interpretation. Write each phase's tests first and
verify that they fail for the intended missing behavior before implementing that phase.

**Organization**: Tasks are grouped by user story. User Story 1 establishes the independently
testable classifier, User Story 2 makes its evidence auditable and filterable, and User Story 3
integrates the same policy across both evaluation lanes.

## Active Process and Temporary Build Constraint

TestMap is currently running. Implementation must not terminate it, replace its loaded binaries,
clean its active output directories, reset its workspace, or reuse its live database/log paths.

- Allocate a unique disposable directory beneath the operating-system temporary directory for every
  build/test validation session.
- Prefer `dotnet build/test --artifacts-path <temporary-path>` for focused validation.
- If a tool such as migration generation still touches locked repository outputs, make a disposable
  copy of the current working source, excluding `.git`, `bin`, `obj`, logs, databases, and runtime
  artifacts; build or generate in that copy and bring back only intentional source artifacts.
- Resolve and inspect temporary paths before cleanup. Never recursively delete a repository root,
  home directory, unresolved variable, or active TestMap output.
- Use isolated temporary SQLite databases and fixture workspaces. Do not point tests at the running
  program's database.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes different files and does not depend on incomplete
  work in the same phase.
- **[Story]**: Maps tasks to User Story 1, 2, or 3.
- Every task names the concrete file or directory it changes.

---

## Phase 1: Setup and Safe Validation

**Purpose**: Establish the shared configuration/model surface and protect the running program from
build, migration, database, and workspace interference.

- [X] T001 Add live-process-safe temporary build, migration-copy, isolated-database, and cleanup commands to `specs/005-non-trivial-assertions/quickstart.md`
- [X] T002 [P] Add the shared enabled/default-depth assertion evaluation settings in `TestMap/Models/Configuration/Experiment/AssertionLineageEvaluationConfig.cs`, `TestMap/Models/Configuration/Experiment/ExperimentEvaluationConfig.cs`, and `TestMap/Config/default-config.json`
- [X] T003 [P] Add the policy version, catalog version, category/status/reason vocabularies, hop rules, and transient analysis result models in `TestMap/Models/Experiment/Assertions/` and `TestMap/Services/StaticAnalysis/Assertions/AssertionLineagePolicy.cs`

---

## Phase 2: Foundational Roslyn and Configuration Prerequisites

**Purpose**: Provide shared policy validation, deterministic semantic identity, test/project scope,
and reusable fixtures required by every story.

**⚠️ CRITICAL**: No user story work begins until this phase passes.

- [X] T004 [P] Add failing tests for default depth four, invalid depth rejection, policy snapshotting, and shared lane configuration in `TestMap.UnitTests/Configuration/AssertionLineageEvaluationConfigTests.cs` and `TestMap.UnitTests/TestGeneration/ExperimentConfigurationValidatorTests.cs`
- [X] T005 Implement assertion-lineage configuration validation and immutable effective-policy resolution in `TestMap/Services/Experiment/Execution/ExperimentConfigurationValidator.cs`, `TestMap/Models/Configuration/Experiment/ExperimentEvaluationConfig.cs`, and `TestMap/Services/StaticAnalysis/Assertions/AssertionLineagePolicy.cs`
- [X] T006 [P] Add failing overload, constructor/property, source-location, production-vs-test-project, and pre-indexed dispatch tests in `TestMap.UnitTests/StaticAnalysis/Assertions/RoslynMemberSymbolIndexTests.cs`
- [X] T007 Extract overload-safe symbol normalization, document/declaration lookup, member indexing, production/test-support classification, and dispatch indexing into `TestMap/Services/StaticAnalysis/RoslynMemberSymbolIndex.cs` and `TestMap/Services/StaticAnalysis/RoslynAnalysisUtilities.cs`
- [X] T008 Preserve `roslyn-source-test-trace-v1` behavior while consuming the shared utilities in `TestMap/Services/StaticAnalysis/RoslynSourceTestTraceService.cs` and extend regression coverage in `TestMap.UnitTests/TestGeneration/RoslynSourceTestTraceServiceTests.cs`
- [X] T009 Create reusable two-project semantic fixtures, minimal supported-framework assertion stubs, in-memory SQLite setup, and deterministic member identities in `TestMap.UnitTests/StaticAnalysis/Assertions/AssertionLineageTestWorkspace.cs`

**Checkpoint**: Configuration, production/test-support identity, and semantic fixtures are stable.

---

## Phase 3: User Story 1 - Classify Assertion Lineage (Priority: P1) 🎯 MVP

**Goal**: Classify every recognized assertion in an attributable test fixture as `Traced`,
`Trivial`, or `Unresolved` using assertion-operand backward slicing.

**Independent Test**: Run the assertion-lineage unit fixture corpus and verify direct calls,
assignments, and in-bound helper returns trace; literal/test-local assertions are trivial; and
ambiguous, unsupported, cyclic, or over-depth paths are unresolved.

### Tests for User Story 1

> Write these tests first and confirm they fail for the missing catalog/slice behavior.

- [X] T010 [P] [US1] Add semantic terminal recognition and one-logical-assertion tests for xUnit, NUnit, MSTest, FluentAssertions, and Shouldly in `TestMap.UnitTests/StaticAnalysis/Assertions/AssertionPatternCatalogTests.cs`
- [X] T011 [P] [US1] Add observed-operand role tests for expected/actual, predicates, NUnit constraints, fluent receivers, Shouldly receivers, messages, and exception delegates in `TestMap.UnitTests/StaticAnalysis/Assertions/AssertionOperandExtractorTests.cs`
- [X] T012 [P] [US1] Add exact-program-point reaching-definition tests for initializers, reassignment, branches, loops, early exits, and flow captures in `TestMap.UnitTests/StaticAnalysis/Assertions/ReachingDefinitionAnalysisTests.cs`
- [X] T013 [P] [US1] Add helper-return, parameter-substitution, async, exactly-four-hop, fifth-hop, cycle, field, reflection, dynamic, and ambiguous-dispatch tests in `TestMap.UnitTests/StaticAnalysis/Assertions/AssertionLineageSliceTests.cs`
- [X] T014 [P] [US1] Add end-category tests for direct production members, literals, unrelated arrange calls, mixed definitions, production-only control conditions, self-comparison, unsupported shapes, and target relation in `TestMap.UnitTests/StaticAnalysis/Assertions/RoslynAssertionLineageAnalysisServiceTests.cs`

### Implementation for User Story 1

- [X] T015 [P] [US1] Implement versioned semantic assertion-terminal matching and legacy-fallback recognition in `TestMap/Services/StaticAnalysis/Assertions/AssertionPatternCatalog.cs` and route occurrence recognition through it from `TestMap/Services/StaticAnalysis/CSharpAnalysisRules.cs`
- [X] T016 [P] [US1] Implement framework-specific observed-operand extraction, fluent receiver unwrapping, constraint values, and exception-delegate bodies in `TestMap/Services/StaticAnalysis/Assertions/AssertionOperandExtractor.cs`
- [X] T017 [US1] Implement the reachable-block fixed-point reaching-definition index for local declarations, simple assignments, and flow captures in `TestMap/Services/StaticAnalysis/Assertions/ReachingDefinitionAnalysis.cs`
- [X] T018 [P] [US1] Implement unique production-member, constructor/property/field, helper-scope, candidate-relation, and ambiguous-dispatch resolution in `TestMap/Services/StaticAnalysis/Assertions/RoslynProductionMemberResolver.cs`
- [X] T019 [US1] Implement recursive expression slicing, helper returns, parameter substitution, hop accounting, memoization, cycle detection, path caps, and stable terminal reasons in `TestMap/Services/StaticAnalysis/Assertions/AssertionLineageSlicer.cs`
- [X] T020 [US1] Implement compositional and alternative-path lattices with deterministic trace paths in `TestMap/Services/StaticAnalysis/Assertions/AssertionLineageClassifier.cs`
- [X] T021 [US1] Implement member discovery, CFG association by source span, logical assertion ordering, expression hashing, and complete test summaries in `TestMap/Services/StaticAnalysis/Assertions/RoslynAssertionLineageAnalysisService.cs` and `TestMap/Services/StaticAnalysis/Assertions/IAssertionLineageAnalysisService.cs`
- [X] T022 [US1] Register the catalog, resolver, slicer, and shared analysis service in `TestMap/Services/ServiceCollectionExtensions.cs`
- [X] T023 [US1] Run the User Story 1 focused tests with a unique temporary `--artifacts-path`, confirm the running TestMap process remains untouched, and record the command/result in `specs/005-non-trivial-assertions/quickstart.md`

**Checkpoint**: User Story 1 is independently usable as an in-memory semantic classifier.

---

## Phase 4: User Story 2 - Audit and Filter Quality Evidence (Priority: P2)

**Goal**: Persist immutable raw assertion evidence, publish reconciled summary and assertion grains,
derive traced-only views, and block invalid publication datasets.

**Independent Test**: Seed complete, mixed, no-recognized-assertion, unavailable, and historical
measurements; persist and export them; build raw and traced-only datasets; and verify all category,
missingness, provenance, and reconciliation audits.

### Tests for User Story 2

> Write these tests first and confirm schema, reconciliation, and audit failures are observable.

- [X] T024 [P] [US2] Add domain invariant and aggregation tests for all four evidence grains in `TestMap.UnitTests/TestGeneration/AssertionLineageModelTests.cs`
- [X] T025 [P] [US2] Add repository tests for owner exclusivity, immutable snapshots, ordered paths, cascade behavior, nullable unavailable counts, and atomic writes in `TestMap.UnitTests/Persistence/AssertionLineageRepositoryTests.cs`
- [X] T026 [P] [US2] Add migration tests proving new-schema creation and prior-head historical attempts derive `NotMeasured` in `TestMap.UnitTests/Persistence/AssertionLineageMigrationTests.cs` and `TestMap.IntegrationTests/Persistence/MigrationSchemaTests.cs`
- [X] T027 [P] [US2] Add schema-4 attempt/generated-test row, null-count, manifest, and assertion-sidecar contract tests in `TestMap.UnitTests/TestGeneration/ExperimentResultsWriterTests.cs` and `TestMap.UnitTests/TestGeneration/AssertionObservationWriterTests.cs`
- [X] T028 [P] [US2] Add schema-4 reader, assertion-sidecar normalization, raw reconciliation, historical `NotMeasured`, and traced-only view tests in `Analysis/tests/test_assertion_lineage.py` and `Analysis/tests/test_build_evaluation_dataset.py`
- [X] T029 [P] [US2] Add blocking audit tests for invalid categories, missing reasons/policy, duplicate/orphan IDs, broken path terminals, traced-without-production, count mismatch, missing sidecar, and historical zero conversion in `Analysis/tests/test_analysis_fixes.py` and `Analysis/tests/test_pinned_target_audits.py`

### Implementation for User Story 2

- [X] T030 [P] [US2] Add EF entities for attempt measurements, generated-test summaries, assertion observations, and ordered lineage steps in `TestMap/Persistence/Ef/Entities/Experiment/Assertions/`
- [X] T031 [P] [US2] Add domain/entity mappings, column constraints, owner checks, unique indexes, path ordering, and relationships in `TestMap/Persistence/Ef/Mapping/Experiment/Assertions/` and `TestMap/Persistence/Ef/Configuration/Entities/Experiment/Assertions/`
- [X] T032 [US2] Add assertion DbSets and parent/child navigation configuration in `TestMap/Persistence/Ef/TestMapDbContext.cs`, `TestMap/Persistence/Ef/Entities/Experiment/GenerationAttemptEntity.cs`, and `TestMap/Persistence/Ef/Entities/AgentTools/ToolAttemptEntity.cs`
- [X] T033 [US2] Generate the additive assertion-lineage migration in a disposable temporary workspace copy, then bring back only the migration and snapshot updates to `TestMap/Migrations/`, `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`, and `TestMap/schema.sql`
- [X] T034 [US2] Implement atomic insert/query behavior and historical `NotMeasured` projection in `TestMap/Persistence/Ef/Repositories/Experiment/Assertions/AssertionLineageMeasurementRepository.cs`
- [X] T035 [US2] Implement per-test and attempt aggregation with null-preserving unavailable semantics and exact reconciliation in `TestMap/Services/Experiment/Reporting/AssertionLineageSummaryService.cs`
- [X] T036 [P] [US2] Add schema-4 assertion status, policy/depth, counts, timing, and attribution fields in `TestMap/Services/Experiment/Reporting/ExperimentResultFileRow.cs`
- [X] T037 [US2] Update row formatting, strict schema validation, metadata, and manifest generation for results schema 4.0 in `TestMap/Services/Experiment/Reporting/ExperimentResultsWriter.cs`
- [X] T038 [P] [US2] Implement assertion schema-1.0 sidecar emission with complete parent provenance and serialized ordered paths in `TestMap/Services/Experiment/Reporting/AssertionObservationWriter.cs` and `TestMap/Services/Experiment/Reporting/IAssertionObservationWriter.cs`
- [X] T039 [US2] Implement collection-time category, reason, owner, terminal, reconciliation, policy, and export-completeness validation in `TestMap/Services/Experiment/Reporting/AssertionLineageAuditService.cs`
- [X] T040 [P] [US2] Add assertion-table readers and schema-4/sidecar version handling in `Analysis/src/analysis/db.py`, `Analysis/src/analysis/files.py`, and `Analysis/src/analysis/schema.py`
- [X] T041 [US2] Normalize assertion measurements and observations, derive traced-only views, and retire regex/invocation fallback as classified evidence in `Analysis/src/analysis/normalize.py` and `Analysis/src/analysis/build_evaluation_dataset.py`
- [X] T042 [US2] Enforce assertion contract reconciliation and publication blockers in `Analysis/src/analysis/audit_evaluation_data.py`
- [X] T043 [US2] Run the persistence, export, and Python analysis checkpoints against isolated temporary SQLite databases and temporary .NET artifacts, then record results in `specs/005-non-trivial-assertions/quickstart.md`

**Checkpoint**: User Story 2 can persist, export, filter, and audit classifier results without lane
orchestration.

---

## Phase 5: User Story 3 - Preserve Fair Experimental Interpretation (Priority: P3)

**Goal**: Invoke and persist the same assertion policy in both evaluation lanes before rollback while
leaving acceptance, coverage, mutation, retries, and attribution unchanged.

**Independent Test**: Execute equivalent deterministic generated tests through mocked TestMap and
agent-tool lanes and verify identical assertion evidence, explicit multi-test aggregation, unchanged
dynamic metrics/acceptance, and successful end-to-end export/audit.

### Tests for User Story 3

> Write these tests first and confirm lane-specific gaps fail before orchestration changes.

- [ ] T044 [P] [US3] Add TestMap-lane tests for post-refresh analysis, transient evidence, persistence after execution IDs, unavailable early outcomes, and pre-rollback ordering in `TestMap.UnitTests/TestGeneration/GeneratedTestAssertionLineageTests.cs`
- [ ] T045 [P] [US3] Add agent-tool tests for post-link per-test analysis, no-link unavailable measurement, multi-test summaries, and pre-measurement/pre-rollback ordering in `TestMap.UnitTests/AgentTools/ToolAttemptAssertionLineageTests.cs`
- [X] T046 [P] [US3] Add equivalent-code lane-parity and non-regression tests for acceptance, repair stopping, coverage, mutation, retry, and attribution in `TestMap.UnitTests/TestGeneration/AssertionLineageLaneParityTests.cs` and `TestMap.UnitTests/TestGeneration/ExperimentOrchestrationServiceTests.cs`
- [ ] T047 [P] [US3] Add a deterministic two-lane source-to-export-to-audit fixture without live model calls in `TestMap.EndToEndTests/AssertionLineageExperimentEndToEndTests.cs`

### Implementation for User Story 3

- [X] T048 [P] [US3] Carry transient assertion-analysis results and applied-source identity without changing persisted legacy execution semantics in `TestMap/Services/TestGeneration/Execution/IGeneratedTestExecutionService.cs` and `TestMap/Models/Experiment/TestExecution.cs`
- [X] T049 [US3] Invoke the shared analyzer after generated-test metadata refresh/member resolution and before workspace rollback in `TestMap/Services/TestGeneration/Execution/GeneratedTestExecutionService.cs`
- [X] T050 [US3] Persist the TestMap-lane measurement only after generation-attempt and test-execution IDs exist, including explicit unavailable evidence for eligible early outcomes, in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T051 [US3] Invoke and persist the agent-tool measurement after post-attempt analysis and generated-test linking but before dynamic measurement and rollback in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs` and `TestMap/Services/Experiment/Execution/ToolAttemptGeneratedTestService.cs`
- [X] T052 [US3] Resolve the agent attempt's intended source member directly from its candidate and persist `Unavailable` or `NotApplicable` for missing links/source without using highest-confidence source-test mapping as the target in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T053 [US3] Populate TestMap child summaries, agent child summaries, explicit agent attempt aggregates, and separate impact attribution in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs` and `TestMap/Services/Experiment/Reporting/AssertionLineageSummaryService.cs`
- [X] T054 [US3] Enforce equal policy/catalog/depth across comparable lanes and include the shared effective policy in experiment provenance in `TestMap/Services/Experiment/Execution/ExperimentConfigurationValidator.cs`, `TestMap/Models/Experiment/ExperimentRun.cs`, and `TestMap/Persistence/Ef/Mapping/Experiment/ExperimentRunMappingExtensions.cs`
- [ ] T055 [US3] Run both-lane unit and end-to-end checkpoints with a unique temporary artifact/workspace/database root, verify the active TestMap process is unchanged, and record results in `specs/005-non-trivial-assertions/quickstart.md`

**Checkpoint**: All three user stories work together with identical cross-lane semantics.

---

## Phase 6: Polish and Cross-Cutting Validation

**Purpose**: Complete performance, documentation, compatibility, full regression, and
publication-readiness checks without interfering with the running program.

- [X] T056 [P] Add separate analyzer timing and a deterministic 1,000-assertion performance sentinel in `TestMap/Services/StaticAnalysis/Assertions/RoslynAssertionLineageAnalysisService.cs` and `TestMap.UnitTests/StaticAnalysis/Assertions/AssertionLineagePerformanceTests.cs`
- [X] T057 [P] Update assertion category, status, sidecar, non-claim, and schema-4 documentation in `README.md`, `Analysis/README.md`, and `specs/005-non-trivial-assertions/contracts/assertion-lineage-contract.md`
- [X] T058 Replace remaining publication-path schema-3 assumptions and document intentional legacy-only fixtures in `Analysis/src/analysis/`, `Analysis/tests/`, and `TestMap.UnitTests/TestGeneration/ExperimentResultsWriterTests.cs`
- [X] T059 Run `dotnet test .\TestMap.slnx` with a unique temporary `--artifacts-path`; if any tool still touches locked outputs, validate from a disposable source copy and record the exact safe command in `specs/005-non-trivial-assertions/validation-report.md`
- [X] T060 Run the full `uv run pytest` suite from `Analysis/` with isolated temporary inputs and record results in `specs/005-non-trivial-assertions/validation-report.md`
- [ ] T061 Execute the complete deterministic two-lane workflow, inspect the SQLite database, schema-4 results, assertion sidecar, manifest, normalized datasets, and strict audit together, and record evidence in `specs/005-non-trivial-assertions/validation-report.md`
- [ ] T062 Run paired feature-off/feature-on measurements on the same frozen multi-repository pilot and record median attempt overhead plus raw analyzer timings in `specs/005-non-trivial-assertions/validation-report.md`
- [ ] T063 Re-run every command in `specs/005-non-trivial-assertions/quickstart.md`, correct stale paths or expectations there, and confirm all temporary directories are outside active TestMap outputs
- [X] T064 Complete a constitution-focused compatibility review of row grain, target identity, missingness, policy versions, multi-test attribution, acceptance non-regression, and schema opt-in in `specs/005-non-trivial-assertions/validation-report.md`

---

## Dependencies and Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: Starts immediately and establishes safe validation plus shared surface.
- **Phase 2 — Foundational**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 — User Story 1**: Depends on Phase 2 and creates the classifier MVP.
- **Phase 4 — User Story 2**: Depends on User Story 1 result semantics and makes them durable,
  exportable, filterable, and auditable.
- **Phase 5 — User Story 3**: Depends on User Stories 1 and 2 so both lanes can persist the same
  evidence and publish it under one contract.
- **Phase 6 — Polish**: Depends on all desired stories and performs full safe validation.

### User Story Dependency Graph

```text
Setup
  -> Foundational
      -> US1 Classifier
          -> US2 Persistence / Export / Audit
              -> US3 Shared Two-Lane Integration
                  -> Polish / Pilot Validation
```

### User Story Independence

- **User Story 1**: Independently testable through semantic fixture inputs and in-memory analysis
  results; it does not need persistence or lane orchestration.
- **User Story 2**: Independently testable by feeding deterministic User Story 1 results into
  isolated databases, writers, Python normalization, and audits.
- **User Story 3**: Independently testable with mocked deterministic lane inputs and the completed
  evidence contract; no live model or active program shutdown is required.

### Within Each User Story

- Write and run the phase's failing tests before implementation.
- Define data/vocabulary before services that consume them.
- Implement semantic analysis before orchestration.
- Implement persistence before export and export before downstream normalization/audit.
- Complete the phase checkpoint before advancing.

## Parallel Opportunities

- T002 and T003 can run in parallel after T001 because they change configuration and domain/policy
  files separately.
- T004 and T006 can run in parallel; T005 follows T004 and T007 follows T006.
- US1 test tasks T010–T014 can run in parallel after T009.
- US1 implementations T015, T016, and T018 can run in parallel; T017 and those components converge
  at T019–T021.
- US2 test tasks T024–T029 can run in parallel after US1.
- US2 entity/config tasks T030–T031 and result-row task T036 can run in parallel; persistence,
  migration, writers, and Python consumers follow their respective prerequisites.
- US3 test tasks T044–T047 can run in parallel after US2.
- T048 and the agent-specific preparation for T051 can proceed in parallel before orchestration
  convergence.
- T056 and T057 can run in parallel once all stories are complete.

## Parallel Example: User Story 1

```text
Task T010: AssertionPatternCatalogTests.cs
Task T011: AssertionOperandExtractorTests.cs
Task T012: ReachingDefinitionAnalysisTests.cs
Task T013: AssertionLineageSliceTests.cs
Task T014: RoslynAssertionLineageAnalysisServiceTests.cs
```

After those tests fail as expected:

```text
Task T015: AssertionPatternCatalog.cs and CSharpAnalysisRules.cs
Task T016: AssertionOperandExtractor.cs
Task T018: RoslynProductionMemberResolver.cs
```

## Parallel Example: User Story 2

```text
Task T024: AssertionLineageModelTests.cs
Task T025: AssertionLineageRepositoryTests.cs
Task T026: AssertionLineageMigrationTests.cs and MigrationSchemaTests.cs
Task T027: Writer contract tests
Task T028: Python normalization tests
Task T029: Python audit tests
```

## Parallel Example: User Story 3

```text
Task T044: GeneratedTestAssertionLineageTests.cs
Task T045: ToolAttemptAssertionLineageTests.cs
Task T046: Lane parity and orchestration non-regression tests
Task T047: AssertionLineageExperimentEndToEndTests.cs
```

## Implementation Strategy

### MVP First: User Story 1

1. Complete safe setup and foundational semantic identity.
2. Write the US1 classifier fixtures first.
3. Implement the catalog, operand extraction, reaching definitions, recursive slice, and lattice.
4. Run only US1 tests through a disposable artifact path.
5. Stop and review category semantics before creating schema or export contracts in code.

### Incremental Delivery

1. **US1**: Deliver deterministic in-memory assertion classification.
2. **US2**: Add immutable evidence grains, schema 4.0, sidecar export, filtering, and audits.
3. **US3**: Integrate both lanes using the shared policy without changing dynamic outcomes.
4. **Polish**: Run safe full regression, deterministic end-to-end audit, and paired pilot timing.

### Live-Process-Safe Execution

1. Keep source edits in the primary workspace.
2. Direct routine .NET build/test outputs to a unique temporary artifact root.
3. Use isolated fixture workspaces and databases for every test.
4. Use a disposable current-source copy for migration generation or any tool that insists on touching
   locked outputs.
5. Bring back only intentional migration/source files and verify them in the primary workspace.
6. Never stop the running program or clean/delete its active outputs as part of this feature.

## Notes

- `[P]` tasks change separate files and have no dependency on incomplete work in the same phase.
- `[US1]`, `[US2]`, and `[US3]` map directly to the specification's prioritized stories.
- Existing `invocations.is_assertion` remains an occurrence inventory; it is not classified lineage.
- Existing source-test mappings retain whole-test reachability semantics.
- `Traced` means production data lineage, not logical oracle strength or assertion-caused mutant kill.
- Historical schema-3 rows remain legacy evidence and derive `NotMeasured` for assertion lineage.
- Do not commit runtime logs, temporary databases, temporary workspaces, or temporary build artifacts.
