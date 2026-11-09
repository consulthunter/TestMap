# Quickstart: Validate Token Usage Accounting

## Prerequisites

- .NET 10 SDK available to the repository.
- Python environment for `Analysis/` dependencies.
- No live model credentials are required for deterministic validation.

## Implementation Baseline — 2026-08-27

Before product changes, the focused suites passed with the existing token-accounting behavior:

- Built-in generation configuration/repair: 15 passed.
- Persistence, orchestration, and results writer: 56 passed.
- Agent-tool usage parsing/persistence: 37 passed.
- Migration schema: 9 passed.
- Python schema/normalization/analysis/dataset: 140 passed.

The .NET runs emitted the pre-existing NU1903 advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11. Tests that create temporary repositories or pytest fixtures
needed a writable test-owned temporary directory in the sandbox; rerunning with that access did not
reveal product failures.

## 1. Run focused built-in-lane tests

```powershell
dotnet test TestMap.UnitTests/TestMap.UnitTests.csproj --filter "FullyQualifiedName~TestGenerationPipelineServiceConfigurationTests|FullyQualifiedName~TestGenerationPipelineServiceRepairTests"
```

Expected outcomes:

- Chat estimates include system and user content; inference estimates include only submitted prompt
  content.
- Returned response content populates output and complete total.
- Retry input is accumulated; an invocation with unobservable output produces partial usage.
- Local/skipped/fallback steps are not applicable.

## 2. Run persistence and orchestration tests

```powershell
dotnet test TestMap.UnitTests/TestMap.UnitTests.csproj --filter "FullyQualifiedName~GenerationStepRepositoryTests|FullyQualifiedName~GenerationAttemptRepositoryTests|FullyQualifiedName~GenerationAttemptMappingExtensionsTests|FullyQualifiedName~ExperimentOrchestrationServiceTests|FullyQualifiedName~ExperimentResultsWriterTests"
```

Expected outcomes:

- Step and attempt split components, nullable totals, status, source, and policy round-trip.
- Attempt totals are derived from applicable steps.
- Initial and repair attempts expose correct cumulative input/output/total.
- Missing components propagate without becoming zero.
- CSV headers and rows follow [the token contract](contracts/token-usage-contract.md).

### Per-Attempt Validation — 2026-08-27

- Built-in configuration/repair accounting: 20 passed.
- Generation/tool persistence foundation: 42 passed.
- Agent-tool parser, path, mapping, and repository coverage: 48 passed.
- Orchestration and canonical writer coverage: 31 passed.

## 3. Run all agent-tool parser fixtures

```powershell
dotnet test TestMap.UnitTests/TestMap.UnitTests.csproj --filter "FullyQualifiedName~AgentToolEvaluationLaneTests|FullyQualifiedName~ToolAttemptMappingExtensionsTests|FullyQualifiedName~ToolAttemptRepositoryTests"
```

Expected outcomes:

- Codex, Claude, Gemini, OpenHands, mini-swe-agent, Aider, and Copilot fixtures normalize under one
  policy.
- Aggregate/detail aliases are not double-counted.
- Gemini stream JSON and JSON-file modes are covered.
- Complete, partial, and missing tool rows use consistent availability semantics.

## 4. Verify the database migration

```powershell
dotnet test TestMap.IntegrationTests/TestMap.IntegrationTests.csproj --filter "FullyQualifiedName~MigrationSchemaTests"
```

Expected outcomes:

- New component and usage metadata columns exist.
- Generation totals accept null for incomplete observations.
- Historical generation values remain legacy rather than being split.
- Existing tool components remain intact.

### Repair-Chain Validation — 2026-08-27

The focused repair, orchestration, and writer slice passed 42 tests, including component-wise
cumulative propagation, missing-component invalidation, independent-attempt reset, and split CSV
serialization.

## 5. Run analysis contract tests

```powershell
python -m pytest Analysis/tests/test_schema.py Analysis/tests/test_normalize.py Analysis/tests/test_analysis_fixes.py Analysis/tests/test_build_evaluation_dataset.py
```

Expected outcomes:

- Input/output/cumulative components are numeric normalized fields.
- Reported and estimated observations remain distinguishable.
- Repair candidates use terminal cumulative components exactly once.
- Summaries expose component totals and missingness.
- Audits detect contradictory totals, invalid status/component combinations, and bad cumulative
  chains.

### Analysis Validation — 2026-08-27

The schema, normalization, summaries/audits, dataset, and pinned-target contract slice passed 154
tests using the repository virtual environment and a test-owned temporary directory.

## 6. Run the full regression suite

```powershell
dotnet test TestMap.slnx
python -m pytest Analysis/tests
```

### Full Regression Evidence — 2026-08-27

- .NET: 1,165 unit, 39 integration, and 16 end-to-end tests passed (1,220 total).
- Python: 196 tests passed after the final token-integrity audit cases were added.
- Final token-focused slice: 101 unit tests passed.
- Final migration schema slice: 10 integration tests passed.
- Final Gemini path-resolver slice: 8 unit tests passed, including the implicit
  `stream-json` container default.

The runs continued to emit the pre-existing NU1903 advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11; no token-accounting regression failed.

## 7. Inspect a small pilot

For a frozen small pilot containing at least one successful built-in attempt, one repair chain, and
one agent-tool attempt, inspect together:

1. `generation_steps` and `generation_attempts` component/status/policy values.
2. `tool_attempts` reported components and derived totals.
3. Canonical result-row per-attempt and cumulative fields.
4. Normalized candidate-level effective input/output/total.
5. Audit output and TestMap-owned timing before/after the feature.

The pilot passes when all complete totals reconcile, repair cost is counted once, no missing value is
zero-filled, measurement quality is visible, and median added processing time is at most 5%.
