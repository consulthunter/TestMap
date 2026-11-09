# Tasks: Token Usage Accounting

**Input**: Design documents from `/specs/007-token-usage-accounting/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [token-usage-contract.md](contracts/token-usage-contract.md)

**Tests**: Required because this feature changes experimental measurement, persistence, canonical
exports, repair-chain cost, and statistical interpretation. Write the listed tests first and confirm
they fail for the intended reason before implementing each behavior.

**Organization**: Tasks are grouped by user story. Existing projects, services, models, parsers, and
test fixtures are extended in place; no new token subsystem, database table, sidecar, or analysis
pipeline is introduced.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it targets different files and does not depend on an incomplete task.
- **[Story]**: User story from [spec.md](spec.md).
- Every task names the exact files it creates or modifies.

## Phase 1: Setup and Baseline

**Purpose**: Establish the pre-change test baseline without changing product behavior.

- [X] T001 Run the focused pre-change commands and record the current token-accounting baseline under a dated “Implementation Baseline” note in `specs/007-token-usage-accounting/quickstart.md`

---

## Phase 2: Foundational Token Contract and Persistence

**Purpose**: Add the shared vocabulary, nullable model shape, and persistence contract required by
all three user stories.

**⚠️ CRITICAL**: Complete this phase before implementing lane behavior or analysis.

### Tests for the foundation

- [X] T002 [P] Add failing generation step/attempt round-trip tests for nullable totals, input/output, usage status/source/policy, and legacy values in `TestMap.UnitTests/Persistence/GenerationStepRepositoryTests.cs`, `TestMap.UnitTests/Persistence/GenerationAttemptMappingExtensionsTests.cs`, and `TestMap.UnitTests/Persistence/GenerationAttemptRepositoryTests.cs`
- [X] T003 [P] Add failing agent-tool round-trip tests for status, policy, complete-only availability, and derived total in `TestMap.UnitTests/AgentTools/ToolAttemptMappingExtensionsTests.cs` and `TestMap.UnitTests/AgentTools/ToolAttemptRepositoryTests.cs`
- [X] T004 [P] Add failing schema assertions for new columns, nullable generation totals, and unchanged historical token values in `TestMap.IntegrationTests/Persistence/MigrationSchemaTests.cs`

### Foundation implementation

- [X] T005 Define the five usage statuses, `token-accounting-v1`, `cl100k-local-estimate`, and status/component validation helpers in `TestMap/Models/Experiment/TokenUsageVocabulary.cs`
- [X] T006 Extend generation step, pipeline metadata/result, and generation attempt models with nullable components/totals, usage metadata, and derived-total invariants in `TestMap/Models/Experiment/GenerationStep.cs`, `TestMap/Models/Experiment/GenerationAttempt.cs`, and `TestMap/Services/TestGeneration/ITestGenerationPipelineService.cs`
- [X] T007 [P] Extend the existing agent-tool model with usage status/policy and a nullable derived `TotalTokens` property while retaining `UsageAvailable` and `EstimatedPromptTokens` in `TestMap/Models/AgentTools/ToolAttempt.cs`
- [X] T008 Update generation entities, configurations, mappings, and average-step queries for nullable totals and usage metadata in `TestMap/Persistence/Ef/Entities/Experiment/GenerationStepEntity.cs`, `TestMap/Persistence/Ef/Entities/Experiment/GenerationAttemptEntity.cs`, `TestMap/Persistence/Ef/Configuration/Entities/Experiment/GenerationStepEntityConfiguration.cs`, `TestMap/Persistence/Ef/Configuration/Entities/Experiment/GenerationAttemptEntityConfiguration.cs`, `TestMap/Persistence/Ef/Mapping/Experiment/GenerationStepMappingExtensions.cs`, `TestMap/Persistence/Ef/Mapping/Experiment/GenerationAttemptMappingExtensions.cs`, and `TestMap/Persistence/Ef/Repositories/Experiment/GenerationStepRepository.cs`
- [X] T009 [P] Update agent-tool entity, configuration, and mapping for status/policy while leaving total derived in `TestMap/Persistence/Ef/Entities/AgentTools/ToolAttemptEntity.cs`, `TestMap/Persistence/Ef/Configuration/Entities/AgentTools/ToolAttemptEntityConfiguration.cs`, and `TestMap/Persistence/Ef/Mapping/AgentTools/ToolAttemptMappingExtensions.cs`
- [X] T010 Generate the additive EF migration, label existing generation rows as legacy/missing without splitting their prompt-only totals, preserve existing tool components, update the model snapshot, and synchronize `TestMap/Migrations/`, `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`, and `TestMap/schema.sql`

**Checkpoint**: The shared model and database can represent complete, partial, missing,
not-applicable, estimated, reported, and historical token evidence without zero sentinels.

---

## Phase 3: User Story 1 - Trustworthy Per-Attempt Token Usage (Priority: P1) 🎯 MVP

**Goal**: Persist and export input, output, derived total, status, source, and policy for each built-in
generation attempt and agent-tool attempt.

**Independent Test**: Execute deterministic fake chat and inference generations plus retained
artifacts for all seven tools; verify each step/attempt/database/export row matches the contract and
distinguishes estimated from reported usage.

### Tests for User Story 1

- [X] T011 [P] [US1] Add failing chat/inference tests proving system-plus-user input estimation, response output estimation, derived total, `complete-estimated`, source, and policy in `TestMap.UnitTests/TestGeneration/TestGenerationPipelineServiceConfigurationTests.cs`
- [X] T012 [P] [US1] Add failing step retry tests for repeated input, returned empty-response output, exception-with-unknown-output partial status, and later-success partial retention in `TestMap.UnitTests/TestGeneration/TestGenerationPipelineServiceRepairTests.cs`
- [X] T013 [P] [US1] Extend failing tool fixtures for Codex/Claude/Gemini/OpenHands/mini-swe-agent/Aider/Copilot aggregate precedence, partial/missing status, policy, Gemini JSON mode, and derived total in `TestMap.UnitTests/AgentTools/AgentToolEvaluationLaneTests.cs`
- [X] T014 [P] [US1] Add failing attempt aggregation tests proving applicable-step filtering, complete/partial/missing reduction, and nullable total invariants in `TestMap.UnitTests/TestGeneration/ExperimentOrchestrationServiceTests.cs`
- [X] T015 [P] [US1] Add failing export-contract tests for per-attempt input/output/total, usage classification/source/policy, and repeated child-row attribution in `TestMap.UnitTests/TestGeneration/ExperimentResultsWriterTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Keep `GenerateAsync` text-only and expose tokenizable request content segments by generation mode through `TestMap/Services/TestGeneration/Providers/Abstractions/IAiGenerationProvider.cs` and `TestMap/Services/TestGeneration/Providers/Abstractions/SemanticKernelGenerationProviderBase.cs`; update only the existing fake providers in `TestMap.UnitTests/TestGeneration/TestGenerationPipelineServiceConfigurationTests.cs` and `TestMap.UnitTests/TestGeneration/TestGenerationPipelineServiceRepairTests.cs`
- [X] T017 [US1] Replace prompt-only `TokenCount` calculation with per-invocation input/output estimation and conservative retry accumulation in `TestMap/Services/TestGeneration/TestGenerationPipelineService.cs`
- [X] T018 [US1] Mark context-building, disabled, skipped, and fallback steps not-applicable and ensure structured-step copying retains all usage fields in `TestMap/Services/TestGeneration/TestGenerationPipelineService.cs`
- [X] T019 [US1] Propagate step usage through `MapSteps`, aggregate applicable generation steps into attempt input/output/nullable total/status/source/policy, and populate LLM result rows from the aggregate in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T020 [US1] Normalize tool usage status/policy and harden aggregate-versus-detail precedence, OpenHands aliases, mini-swe trajectory scoping, and complete-only `UsageAvailable` in `TestMap/Services/Experiment/Evaluation/AgentTools/AgentToolEvaluationLane.cs`
- [X] T021 [P] [US1] Align Gemini’s default and explicit stream-JSON/JSON log paths so usage artifacts are discoverable in `TestMap/Services/AgentTools/AgentToolLogPathResolver.cs` and `TestMap.UnitTests/AgentTools/AgentToolLogPathResolverTests.cs`
- [X] T022 [US1] Add `usage_policy_version` to the canonical result row/header/value serialization and use the derived tool total rather than duplicating the addition logic in `TestMap/Services/Experiment/Reporting/ExperimentResultFileRow.cs`, `TestMap/Services/Experiment/Reporting/ExperimentResultsWriter.cs`, and `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T023 [US1] Gate legacy database reports on complete usage and handle nullable generation totals without treating missing as zero in `TestMap/Services/Experiment/Reporting/IExperimentAnalysisService.cs` and `TestMap/Services/Experiment/Reporting/ExperimentAnalysisService.cs`
- [X] T024 [US1] Run the focused User Story 1 commands and record pass/fail evidence in the “Per-Attempt Validation” section of `specs/007-token-usage-accounting/quickstart.md`

**Checkpoint**: Per-attempt token accounting is independently usable. Built-in observations are
explicit estimates; agent-tool observations are explicit reports; totals reconcile or remain null.

---

## Phase 4: User Story 2 - Complete Repair-Chain Cost (Priority: P1)

**Goal**: Preserve each attempt’s own components while exporting correct cumulative input, output,
and total cost through the current point in a repair chain.

**Independent Test**: Run a deterministic initial generation plus two repairs with complete and
incomplete component variants; verify per-attempt values, cumulative propagation, independent-attempt
behavior, and terminal-chain cost counted once.

### Tests for User Story 2

- [X] T025 [P] [US2] Add failing repair tests for complete cumulative input/output/total, missing-component propagation, independent attempt reset, and retry-versus-repair separation in `TestMap.UnitTests/TestGeneration/TestGenerationPipelineServiceRepairTests.cs` and `TestMap.UnitTests/TestGeneration/ExperimentOrchestrationServiceTests.cs`
- [X] T026 [P] [US2] Add failing writer tests for `cumulative_input_tokens`, `cumulative_output_tokens`, derived `cumulative_tokens`, and null propagation on attempt and child rows in `TestMap.UnitTests/TestGeneration/ExperimentResultsWriterTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] Add nullable transient cumulative input/output fields and make cumulative total nullable/derived in `TestMap/Models/Experiment/GenerationAttempt.cs`
- [X] T028 [US2] Extend the existing ordered generation/repair loops to accumulate each component once, invalidate only the missing cumulative component, and avoid summing running cumulative totals in `TestMap/Services/Experiment/Execution/ExperimentOrchestrationService.cs`
- [X] T029 [US2] Add cumulative input/output fields to result rows and serialize them adjacent to existing token columns while retaining existing `cumulative_tokens` as derived total in `TestMap/Services/Experiment/Reporting/ExperimentResultFileRow.cs` and `TestMap/Services/Experiment/Reporting/ExperimentResultsWriter.cs`
- [X] T030 [US2] Run the focused repair-chain and writer tests and record evidence in the “Repair-Chain Validation” section of `specs/007-token-usage-accounting/quickstart.md`

**Checkpoint**: Repair-chain cost is available in the canonical export without a new table, and
candidate cost can use the terminal cumulative observation exactly once.

---

## Phase 5: User Story 3 - Comparable Lane and Tool Analysis (Priority: P2)

**Goal**: Retain, validate, and summarize per-attempt and cumulative input/output/total values without
mixing reported, estimated, partial, missing, or repeated child-row observations.

**Independent Test**: Normalize deterministic mixed-lane fixtures containing independent attempts,
repair chains, repeated child rows, every usage status, and deliberate invariant violations; verify
component summaries and audits.

### Tests for User Story 3

- [X] T031 [P] [US3] Add failing shared-schema tests for usage status/source/policy, input/output/total, and cumulative input/output/total fields in `Analysis/tests/test_schema.py`
- [X] T032 [P] [US3] Add failing normalization tests for numeric coercion, effective input/output/total, terminal repair-chain selection, independent agent-tool cost, and repeated-child-row safety in `Analysis/tests/test_normalize.py` and `Analysis/tests/test_build_evaluation_dataset.py`
- [X] T033 [P] [US3] Add failing summary and audit fixtures for reported-versus-estimated grouping, component missingness, contradictory totals, invalid status/component combinations, negative/saturated values, and broken cumulative chains in `Analysis/tests/test_analysis_fixes.py` and `Analysis/tests/test_pinned_target_audits.py`

### Implementation for User Story 3

- [X] T034 [US3] Add token component, cumulative, classification, source, and policy fields to the normalized shared attempt contract in `Analysis/src/analysis/schema.py`
- [X] T035 [US3] Coerce the new numeric fields, preserve nullable/status metadata, validate per-row total invariants, and compute lane-fair effective input/output/total at attempt and candidate grains in `Analysis/src/analysis/normalize.py`
- [X] T036 [P] [US3] Extend cost and completeness summaries with input/output/total splits and usage-status counts without summing repeated child rows in `Analysis/src/analysis/summaries.py`
- [X] T037 [P] [US3] Replace the total-only token audit with component/status/source/policy/total/cumulative integrity checks and publication-blocking severity where required in `Analysis/src/analysis/audit_evaluation_data.py`
- [X] T038 [US3] Preserve the new token fields through evaluation-dataset construction and candidate aggregation in `Analysis/src/analysis/build_evaluation_dataset.py`
- [X] T039 [US3] Run the focused Python contract tests and record evidence in the “Analysis Validation” section of `specs/007-token-usage-accounting/quickstart.md`

**Checkpoint**: Analysts can compare component costs by lane/tool and measurement quality, and audits
prevent incomplete or double-counted repair costs from entering confirmatory results.

---

## Phase 6: Polish and Cross-Cutting Verification

**Purpose**: Verify migration safety, cross-contract consistency, performance, and complete regression
coverage after all desired user stories are implemented.

- [X] T040 [P] Run the migration/schema integration tests and reconcile the generated migration, snapshot, and hand-maintained schema with `TestMap.IntegrationTests/Persistence/MigrationSchemaTests.cs`, `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`, and `TestMap/schema.sql`
- [X] T041 Run the complete .NET regression suite from `TestMap.slnx` and resolve token-related regressions only in the files listed by `specs/007-token-usage-accounting/plan.md`
- [X] T042 Run the complete Python regression suite from `Analysis/pyproject.toml` and resolve token-related regressions in `Analysis/src/analysis/` and `Analysis/tests/`
- [X] T043 Execute the deterministic orchestration/export/database/analysis validation sequence from `specs/007-token-usage-accounting/quickstart.md` and verify it against `specs/007-token-usage-accounting/contracts/token-usage-contract.md`
- [ ] T044 Run the frozen small pilot, inspect database/CSV/normalized/audit outputs together, record the measured accounting overhead and compatibility outcome in `specs/007-token-usage-accounting/quickstart.md`, and update `specs/007-token-usage-accounting/plan.md` only if an implementation decision changed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 — Setup**: No dependencies.
- **Phase 2 — Foundation**: Depends on T001 and blocks all user stories.
- **Phase 3 — US1**: Depends on T002–T010. This is the technical MVP.
- **Phase 4 — US2**: Depends on US1 because repair cumulative values consume the per-attempt
  components established by US1.
- **Phase 5 — US3**: Fixture/test authoring can begin after Phase 2, but implementation and final
  validation depend on US1 and US2 export contracts.
- **Phase 6 — Polish**: Depends on all stories included in the release.

### User Story Dependencies

- **US1 (P1)**: No dependency on another story after the shared foundation.
- **US2 (P1)**: Depends on US1’s persisted per-attempt component contract; independently testable with
  deterministic generation attempts once that contract exists.
- **US3 (P2)**: Consumes US1 and US2 canonical fields; independently testable with CSV fixtures while
  runtime implementation proceeds.

### Within Each User Story

- Write the story’s tests and confirm the intended failure before implementation.
- Complete model/contract changes before service aggregation.
- Complete service aggregation before export and downstream analysis.
- Run the story checkpoint before starting dependent production changes.
- Do not treat legacy totals, missing components, or prompt estimates as complete usage to make a
  test pass.

### Detailed Task Dependencies

- T005 blocks T006 and T007.
- T006–T007 block T008–T009; T008–T009 block T010.
- T011–T012 and T016 block T017–T018.
- T006, T008, T014, and T017–T018 block T019.
- T003, T007, T009, and T013 block T020; T021 may proceed in parallel with T020.
- T015 and T019–T020 block T022; T006 and T019 block T023.
- T025 and T027 block T028; T026 and T028 block T029.
- T031 blocks T034; T032 and T034 block T035 and T038.
- T033 blocks T036–T037; T034–T037 block T038–T039.
- T040–T044 begin only after T024, T030, and T039 pass.

## Parallel Opportunities

- Foundation test tasks T002–T004 can run in parallel.
- After T005, generation model work T006 and tool model work T007 can run in parallel.
- Generation persistence T008 and tool persistence T009 can run in parallel.
- US1 tests T011–T015 target separate existing test files and can run in parallel.
- Tool parser work T020 and Gemini path work T021 can run in parallel after their tests exist.
- US2 test tasks T025–T026 can run in parallel.
- US3 tests T031–T033 can run in parallel.
- After schema/normalization contracts settle, summary work T036 and audit work T037 can run in
  parallel.
- Final migration verification T040 can run in parallel with initial full-suite execution, but T043
  and T044 wait for all automated suites.

## Parallel Example: User Story 1

```text
Task T011: Add chat/inference estimation tests in TestGenerationPipelineServiceConfigurationTests.cs
Task T012: Add retry/partial tests in TestGenerationPipelineServiceRepairTests.cs
Task T013: Extend all tool parser fixtures in AgentToolEvaluationLaneTests.cs
Task T014: Add attempt aggregation tests in ExperimentOrchestrationServiceTests.cs
Task T015: Add canonical export tests in ExperimentResultsWriterTests.cs
```

After the tests are in place:

```text
Task T020: Harden tool normalization in AgentToolEvaluationLane.cs
Task T021: Align Gemini artifact paths in AgentToolLogPathResolver.cs and its tests
```

## Parallel Example: User Story 2

```text
Task T025: Add repair accumulation tests in pipeline/orchestration test files
Task T026: Add cumulative CSV contract tests in ExperimentResultsWriterTests.cs
```

## Parallel Example: User Story 3

```text
Task T031: Extend Analysis/tests/test_schema.py
Task T032: Extend normalization and dataset tests
Task T033: Extend summary and audit fixtures
```

After normalization fields are established:

```text
Task T036: Extend Analysis/src/analysis/summaries.py
Task T037: Extend Analysis/src/analysis/audit_evaluation_data.py
```

## Implementation Strategy

### Technical MVP — User Story 1

1. Complete T001–T010.
2. Write and fail T011–T015.
3. Complete T016–T023.
4. Run T024 and inspect database plus canonical CSV evidence.
5. Stop if per-attempt totals or status/source/policy do not reconcile.

US1 is independently useful, but the feature is not ready for repair-lane experiments until US2 is
complete.

### Incremental Delivery

1. **Foundation**: Shared vocabulary, nullable totals, migration, mappings.
2. **US1**: Honest per-attempt estimates/reports and canonical export.
3. **US2**: Repair-chain cumulative components and terminal cost.
4. **US3**: Lane-fair normalization, summaries, and blocking audits.
5. **Polish**: Full regression, deterministic workflow, and frozen pilot.

### Minimal-Change Guardrails

- Reuse the existing SharpToken encoder and generation pipeline.
- Keep `IAiGenerationProvider.GenerateAsync` returning text.
- Reuse existing generation step input/output columns.
- Add no token event, repair cost, or tool total table.
- Keep tool parsing in `AgentToolEvaluationLane` and extend current fixtures.
- Keep canonical results schema 4.0 and use `token-accounting-v1` for semantic versioning.
- Keep repair cumulative values transient/exported and reconstructible from persisted attempts.
- Reuse existing analysis modules and fixture builders.

## Notes

- `[P]` tasks target different files and can proceed concurrently after their listed dependencies.
- `[US1]`, `[US2]`, and `[US3]` map directly to the specification’s user stories.
- Commit after each task or tightly related task group.
- Preserve unrelated worktree changes.
- A passing test is insufficient if the row grain, missingness, status/source/policy, or repair
  cumulative semantics violate the token contract.
