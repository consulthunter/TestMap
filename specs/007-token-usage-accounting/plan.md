# Implementation Plan: Token Usage Accounting

**Branch**: `not created by Spec Kit` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-token-usage-accounting/spec.md`

## Summary

Extend the existing token-accounting path rather than introducing a new measurement subsystem. The
built-in LLM lane will continue using the existing SharpToken `cl100k_base` encoder, but it will
estimate the complete TestMap-supplied request and returned response separately, persist nullable
input/output values on generation steps and attempts, derive total only when both components are
complete, and identify the observation as `complete-estimated`. The agent-tool lane will retain its
existing artifact parsers and persisted input/output fields, add explicit status and policy, derive
total in the model/export, and harden tool-specific double-counting behavior. Existing repair-chain
orchestration will accumulate nullable input, output, and total values without creating a new table.
Canonical results and Python analysis will preserve the split and remain chain-aware.

## Technical Context

**Language/Version**: C# on .NET 10; Python 3.11+ for the analysis package

**Primary Dependencies**: SharpToken 2.0.6 (`cl100k_base`), Microsoft Semantic Kernel 1.77.0 with
existing provider connectors, Entity Framework Core 10.0.9 with SQLite, pandas, pytest

**Storage**: Existing SQLite `generation_steps`, `generation_attempts`, and `tool_attempts` tables;
existing experiment-results CSV schema 4.0 with additive token-policy and cumulative-component
columns; no new tables or sidecars

**Testing**: Existing xUnit unit and integration projects; deterministic fake generation providers;
existing tool-artifact parser fixtures; Python `pytest` normalization, summary, and audit fixtures

**Target Platform**: Windows and Linux hosts supported by the TestMap CLI and Docker evaluation
workflows

**Project Type**: .NET command-line research and generated-test evaluation tool with a Python
analysis package

**Performance Goals**: Token accounting adds no more than 5% to median TestMap-owned attempt
processing time on a small frozen pilot, excluding provider/tool execution; no additional model calls

**Constraints**: Built-in values are estimates, never provider-reported; request estimates include
the TestMap system message in chat mode but exclude unknown provider framing; missing values remain
null; complete total always equals input plus output; retry and repair costs count each applicable
invocation once; existing raw tool artifacts remain authoritative

**Scale/Scope**: One token observation per provider-applicable generation step, one aggregate per
generation or tool attempt, and transient/exported cumulative values for repair chains across
thousands of attempts; seven supported agent tools

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **Scientific purpose**: This feature changes evaluation cost evidence and downstream analysis. It
  protects cross-lane efficiency claims from prompt-only LLM counts, omitted output usage, ambiguous
  agent-tool availability, and repair-chain double-counting. It does not alter generation quality,
  validation, coverage, mutation, or candidate selection.
- **Units and scope**: The built-in measurement unit is a provider-applicable generation step;
  attempt values aggregate applicable steps. Agent-tool usage remains attempt-scoped. Repair
  cumulative values are ordered chain observations. Candidate cost uses the terminal cumulative
  observation once. Repository/run summaries use explicit aggregation over candidate values.
- **Eligibility and pairing**: Candidate cohorts, baselines, randomization, budgets, and experiment
  eligibility are unchanged. Every attempted provider or tool invocation is eligible for cost
  measurement, including failed, empty, timed-out, and repaired attempts. Local/skipped steps are
  `not-applicable`, not excluded attempts.
- **Provenance**: Existing repository, commit, run, candidate, attempt, lane, provider/model, tool,
  prompt, response, and artifact provenance is retained. Usage status, source, and
  `token-accounting-v1` are persisted/exported so estimates and tool-reported values remain
  distinguishable.
- **Lane fairness and isolation**: Both lanes expose nullable input/output/derived-total fields. The
  TestMap lane explicitly reports estimates; tools report artifact-derived usage. The distinction is
  retained rather than claiming equal measurement precision. Existing isolation, ordering, budgets,
  retries, and rollback behavior do not change.
- **Missingness and failures**: Missing and partial components remain null and prevent a complete
  total. Known components may be retained on partial observations. No missing cost becomes zero.
  Generation failure and usage availability remain separate. Non-provider steps use
  `not-applicable` and do not contaminate completeness.
- **Verification**: Focused unit tests cover request/response estimation, chat versus inference,
  retries, statuses, totals, each tool parser, and repair accumulation. Mapping/repository and
  migration tests cover persistence and legacy rows. Export-contract tests cover additive columns.
  Python fixtures cover normalization, chain-aware summaries, invariants, and audits. A deterministic
  orchestration fixture replaces live provider calls; a small frozen pilot checks overhead.
- **Compatibility**: No tables are added and existing identifiers, cohorts, outcomes, and raw
  artifacts retain meaning. Existing component and total column names remain. Legacy prompt-only
  generation values receive legacy/missing policy metadata and are excluded from complete cost
  analysis. Existing complete tool components can derive total without a rerun. Results schema 4.0
  remains because the export change is additive and existing token meanings are preserved; the new
  accounting-policy field versions the changed measurement behavior.

### Post-Design Re-check

The design reuses existing attempt and step grains, derives totals rather than persisting independent
contradictory values, and keeps repair cumulative values transient/reconstructible instead of adding
a table. Status, source, and policy preserve measurement quality and historical meaning. TestMap
estimates and agent-tool reports remain explicitly separable in the same export. All constitution
gates remain satisfied without an exception.

## Project Structure

### Documentation (this feature)

```text
specs/007-token-usage-accounting/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── token-usage-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Models/
│   ├── Experiment/
│   │   ├── GenerationStep.cs
│   │   └── GenerationAttempt.cs
│   └── AgentTools/
│       └── ToolAttempt.cs
├── Services/
│   ├── TestGeneration/
│   │   ├── ITestGenerationPipelineService.cs
│   │   ├── TestGenerationPipelineService.cs
│   │   └── Providers/Abstractions/
│   │       ├── IAiGenerationProvider.cs
│   │       └── SemanticKernelGenerationProviderBase.cs
│   └── Experiment/
│       ├── Evaluation/AgentTools/AgentToolEvaluationLane.cs
│       ├── Execution/ExperimentOrchestrationService.cs
│       └── Reporting/
│           ├── ExperimentResultFileRow.cs
│           ├── ExperimentResultsWriter.cs
│           └── ExperimentAnalysisService.cs
├── Persistence/Ef/
│   ├── Entities/{Experiment,AgentTools}/
│   ├── Configuration/Entities/{Experiment,AgentTools}/
│   ├── Mapping/{Experiment,AgentTools}/
│   └── Repositories/{Experiment,AgentTools}/
├── Migrations/
└── schema.sql

TestMap.UnitTests/
├── TestGeneration/
│   ├── TestGenerationPipelineServiceConfigurationTests.cs
│   ├── TestGenerationPipelineServiceRepairTests.cs
│   ├── ExperimentOrchestrationServiceTests.cs
│   └── ExperimentResultsWriterTests.cs
├── AgentTools/
│   ├── AgentToolEvaluationLaneTests.cs
│   ├── ToolAttemptMappingExtensionsTests.cs
│   └── ToolAttemptRepositoryTests.cs
└── Persistence/
    ├── GenerationStepRepositoryTests.cs
    ├── GenerationAttemptMappingExtensionsTests.cs
    └── GenerationAttemptRepositoryTests.cs

TestMap.IntegrationTests/
└── Persistence/MigrationSchemaTests.cs

Analysis/
├── src/analysis/
│   ├── schema.py
│   ├── normalize.py
│   ├── summaries.py
│   └── audit_evaluation_data.py
└── tests/
    ├── test_schema.py
    ├── test_normalize.py
    ├── test_analysis_fixes.py
    └── test_build_evaluation_dataset.py
```

**Structure Decision**: Extend the current pipeline metadata, domain/EF mappings, orchestration,
writer, tool parser, and analysis modules in place. Add only small status/policy fields and derived
properties. Do not add a provider-usage response hierarchy, token service, token-event table,
repair-cost table, new project, or new analysis pipeline.

## Phase 0: Research Decisions

Research is consolidated in [research.md](research.md). The principal decisions are:

1. Keep the built-in lane entirely estimated with the existing SharpToken encoder for this policy;
   do not add provider-specific usage extraction or change `GenerateAsync` to return a new result
   hierarchy.
2. Estimate the content TestMap submits: the system and user message content in chat mode, or the
   prompt in inference mode. Count the returned response separately. Provider framing and hidden
   context remain outside the estimate and are disclosed by status/source/policy.
3. Reuse nullable generation-step input/output columns and the existing total columns. Make canonical
   totals nullable and derived. Preserve legacy values but gate them with legacy/missing policy
   metadata rather than attempting a false backfill.
4. Add nullable attempt input/output plus usage status/source/policy; retain the existing total name
   for compatibility. Derive agent-tool total from existing components instead of adding a database
   column.
5. Accumulate retry usage inside the logical step and repair usage inside existing orchestration.
   Keep repair cumulative fields transient and exported because the chain can be reconstructed from
   persisted attempts and parent links.
6. Normalize all seven existing tool parsers under `token-accounting-v1`, preferring aggregate fields
   over aliases/details and preserving the existing source preference order.
7. Extend results schema 4.0 additively and version token semantics with the policy field rather than
   creating schema 5.0 solely for new optional columns.
8. Extend the current Python normalized schema, chain-aware effective-cost functions, summaries, and
   audits; do not introduce a second cost dataset.

## Phase 1: Design and Contracts

### Runtime Design

- The generation provider contract continues returning response text. A minimal provider input-text
  helper exposes the exact TestMap-supplied message content used for estimation: chat providers
  return the shared system and user segments; inference providers return only the prompt. Existing
  provider implementations continue inheriting the shared behavior.
- `ExecuteStepAsync` estimates input for every invocation before the call and output for every
  returned response, including an empty response. It accumulates retry components. An exception with
  no response leaves output incomplete and classifies the logical step as partial even if a later
  retry succeeds.
- `GenerationStepMetadata` and `GenerationStep` carry nullable input/output/total plus status, source,
  and policy. Existing `generation_steps.input_tokens` and `output_tokens` are populated. The existing
  step total becomes nullable and canonical for new policy rows; historical prompt-only values are
  retained under legacy metadata.
- Local context, fallback, disabled, and skipped steps set status `not-applicable`, leave components
  null, and are excluded from attempt aggregation. Provider-applicable steps use
  `complete-estimated`, `partial`, or `missing` as evidence permits.
- `GenerationAttempt` aggregates applicable steps. It persists nullable input/output and a nullable
  derived `TotalTokensUsed`, along with status/source/policy. Complete estimated attempts use
  `cl100k-local-estimate` and `token-accounting-v1`; mixed or incomplete steps retain explicit
  aggregate status.
- `ToolAttempt` retains persisted input/output, estimated-prompt diagnostic, usage source, and legacy
  availability flag. It adds status and policy. `UsageAvailable` is true only for a complete reported
  observation. `TotalTokens` is a nullable derived property and is not stored independently.
- Existing agent-tool parsing remains in `AgentToolEvaluationLane`. Codex and Copilot aggregate
  fields take precedence over cached/reasoning details; Claude's disjoint cache input categories are
  added; Gemini uses its aggregate total/input policy and also reads configured JSON output;
  OpenHands chooses prompt/input and completion/output aliases before adding disjoint cache/reasoning
  details; mini-swe-agent sums per-trajectory-call usage; Aider uses its latest cumulative footer.
- Existing repair orchestration tracks nullable cumulative input and output alongside nullable total.
  Each component remains available only while every applicable preceding attempt supplies that
  component. Independent attempts use their own values. Result rows export all three cumulative
  values.
- Results rows retain existing per-attempt token columns and add
  `cumulative_input_tokens`, `cumulative_output_tokens`, and `usage_policy_version`. Existing
  `cumulative_tokens` remains the derived cumulative total. Header, manifest, and writer tests are
  updated without changing row grain.
- Python normalization retains/coerces the component, cumulative, status, source, and policy fields.
  It computes lane-fair effective input/output/total values using terminal LLM repair-chain cost and
  self-contained agent-tool attempt cost. Summaries expose component totals and completeness;
  audits enforce status/component/total and cumulative-chain invariants.
- The migration adds only the missing component/status/source/policy columns and changes the two
  canonical generation total columns to nullable. Existing generation rows are labelled legacy and
  are not considered complete. Existing tool rows derive status from their stored components where
  safe; raw values and artifacts are unchanged.

### Test Design

- Extend pipeline unit tests to assert chat input includes system plus user content, inference input
  excludes the system content, response tokens populate output, total is derived, empty responses
  count zero output, exception/retry combinations become partial, and local/skipped steps are
  not-applicable.
- Modify fake providers only enough to expose estimation input segments; do not add a new fake usage
  framework. Add focused assertions to existing configuration and repair test classes.
- Extend generation step/attempt mapping and repository tests for nullable totals, split components,
  status/source/policy, legacy rows, and average-step queries excluding non-complete usage.
- Extend orchestration tests for per-attempt aggregation, initial plus repair cumulative input/output/
  total, independent attempts, missing component propagation, and terminal-chain cost without double
  counting.
- Modify writer tests for the three additive columns, complete/partial/missing/not-applicable rows,
  and total invariants. Preserve existing assertion and coverage column contracts.
- Extend existing agent-tool parser fixtures for aggregate-versus-detail precedence, OpenHands aliases,
  Gemini JSON mode, mini-swe trajectory scoping, partial usage, usage status/policy, and derived total.
  Retain at least one fixture for every supported tool.
- Add one migration/schema integration assertion for new columns, nullable totals, and historical
  compatibility; update `schema.sql` and the EF snapshot.
- Extend Python fixtures to prove numeric coercion, reported-versus-estimated filtering, component
  summaries, repair-chain effective components, missingness, contradictory totals, and invalid
  cumulative values. Reuse existing result-row fixture builders.
- Run the existing deterministic orchestration tests as the end-to-end boundary; no live provider or
  tool call is required. Run a small frozen pilot only for the performance criterion and inspect the
  database, CSV, normalized dataset, and audit together.

## Complexity Tracking

No constitution violations or exceptional architectural complexity are required.
