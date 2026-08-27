# Implementation Plan: Non-Trivial Assertion Detection

**Branch**: `005-non-trivial-assertions` | **Date**: 2026-07-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-non-trivial-assertions/spec.md`

## Summary

Add an attempt-scoped, assertion-grain measurement that classifies each recognized generated-test
assertion as `Traced`, `Trivial`, or `Unresolved` by following its observed operands backward through
control-flow-aware local definitions and bounded test-helper returns until production code or a
documented terminal is reached. Use one policy-versioned assertion catalog and analysis service for
the TestMap and agent-tool lanes. Persist immutable per-attempt measurements, per-test summaries,
per-assertion observations, and ordered lineage steps before workspace rollback. Publish explicit
availability and category counts in results schema 4.0 plus an assertion-grain sidecar. Do not alter
candidate eligibility, generation acceptance, coverage, or mutation semantics.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: Microsoft.CodeAnalysis 5.6.0 (`IOperation` and control-flow graph APIs),
Entity Framework Core 10.0.9 with SQLite, existing TestMap static-analysis and experiment services

**Storage**: SQLite assertion measurement, generated-test summary, observation, and lineage-step
tables; experiment results CSV schema 4.0; assertion-observation CSV schema 1.0; results manifest

**Testing**: xUnit unit, integration, and end-to-end-style fixture tests; Python `pytest` checks for
dataset normalization, audits, and reporting

**Target Platform**: Windows and Linux hosts supported by the TestMap CLI and Docker evaluation
workflows

**Project Type**: .NET command-line research and generated-test evaluation tool with a Python
analysis package

**Performance Goals**: Median end-to-end attempt duration increases by no more than 10% on the frozen
multi-repository pilot; a deterministic 1,000-assertion fixture provides a regression sentinel

**Constraints**: Data dependence only; no control-dependence credit; default four-hop bound;
alternative reaching definitions must agree; ambiguous or unsupported flows remain unresolved;
same effective policy across lanes; analysis never changes acceptance; missing evidence is never
zero or trivial

**Scale/Scope**: One measurement per evaluation attempt and policy; zero or more generated-test
summaries per attempt; one observation per recognized assertion; multiple lineage paths and ordered
steps per assertion; thousands of attempts and tens of assertions per generated test

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **Scientific purpose**: This feature changes evaluation and analysis evidence. It protects claims
  that assertion counts represent production-connected observations rather than tautologies or
  unrelated test-local values. It does not claim logical oracle strength or fault sensitivity.
- **Units and scope**: The primary unit is one recognized assertion occurrence. Per-test summaries
  retain traced, trivial, and unresolved counts. Attempt rows aggregate child tests explicitly.
  Agent-tool coverage and mutation remain attempt-scoped and are never attributed to child tests or
  assertions without independent measurements.
- **Eligibility and pairing**: Candidate cohorts, eligibility, randomization, baselines, retries, and
  before/after pairing are unchanged. The assertion sampling frame contains recognized assertions in
  generated or modified test members attributable to an attempt. Measurement may be unavailable
  without excluding the parent attempt.
- **Provenance**: Every measurement retains repository and commit through its experiment run,
  candidate and source member, attempt and lane owner, generated-test identity and content hash,
  model/tool and configuration through the parent attempt, effective depth, catalog version,
  lineage-policy version, timestamps, and trace evidence.
- **Lane fairness and isolation**: Both lanes call the same catalog and analyzer with the same
  candidate identity, depth, and policy. TestMap analysis runs after generated-test metadata refresh;
  agent-tool analysis runs after post-attempt refresh and generated-test linking. Both complete
  before rollback. Ordering, budgets, retries, and workspace isolation remain unchanged.
- **Missingness and failures**: `Unavailable` is a measurement status with a required reason;
  `Unresolved` is an observed assertion category with a required resolution code; historical absence
  is derived as `NotMeasured`. No unavailable, historical, unsupported, or over-depth evidence is
  converted to zero, trivial, or traced. Runtime collection failure remains visible but does not
  change generation acceptance.
- **Verification**: Unit fixtures cover catalog recognition, operand roles, control-flow joins,
  reaching definitions, helper returns, depth and cycles, classification reduction, and target
  relation. Persistence and migration tests cover all grains and historical compatibility.
  Integration tests exercise both lanes before rollback. Export-contract and Python dataset/audit
  tests cover schema 4.0, reconciliation, strict missingness, and traced-only views. A deterministic
  end-to-end fixture and frozen pilot cover full workflow and performance.
- **Compatibility**: Existing database rows, validation outcomes, cohorts, coverage, mutation, and
  acceptance retain their meanings. The database migration is additive and does not backfill
  classifications. Results CSV changes incompatibly from schema 3.0 to 4.0; analysis readers require
  explicit schema-4 opt-in for assertion claims while retaining an explicit legacy path for general
  schema-3 analysis. The assertion sidecar starts at schema 1.0.

### Post-Design Re-check

The design preserves four explicit grains instead of embedding assertion facts in attempt rows,
stores effective policy alongside evidence, records no-assertion and unavailable cases at the
measurement/summary grain, and keeps raw observations while deriving traced-only views. Cross-lane
execution uses the same service and runs before workspace rollback. Schema and export versions
change explicitly, and publication audits block inconsistent or missing claimed evidence. All
constitution gates remain satisfied.

## Project Structure

### Documentation (this feature)

```text
specs/005-non-trivial-assertions/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── assertion-lineage-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Models/
│   ├── Configuration/Experiment/
│   │   └── AssertionLineageEvaluationConfig.cs
│   └── Experiment/Assertions/
│       ├── AssertionLineageMeasurement.cs
│       ├── GeneratedTestAssertionSummary.cs
│       ├── AssertionObservation.cs
│       ├── AssertionLineageStep.cs
│       └── AssertionLineageVocabulary.cs
├── Services/
│   ├── StaticAnalysis/Assertions/
│   │   ├── IAssertionLineageAnalysisService.cs
│   │   ├── RoslynAssertionLineageAnalysisService.cs
│   │   ├── AssertionPatternCatalog.cs
│   │   ├── ReachingDefinitionAnalysis.cs
│   │   └── AssertionLineagePolicy.cs
│   ├── StaticAnalysis/
│   │   ├── CSharpAnalysisRules.cs
│   │   └── RoslynSourceTestTraceService.cs
│   ├── Experiment/Execution/
│   │   ├── ExperimentOrchestrationService.cs
│   │   └── ToolAttemptGeneratedTestService.cs
│   ├── Experiment/Reporting/
│   │   ├── ExperimentResultFileRow.cs
│   │   ├── ExperimentResultsWriter.cs
│   │   └── AssertionObservationWriter.cs
│   └── ServiceCollectionExtensions.cs
├── Persistence/Ef/
│   ├── Entities/Experiment/Assertions/
│   ├── Configuration/Entities/Experiment/Assertions/
│   ├── Mapping/Experiment/Assertions/
│   ├── Repositories/Experiment/Assertions/
│   └── TestMapDbContext.cs
├── Migrations/
└── schema.sql

TestMap.UnitTests/
├── StaticAnalysis/Assertions/
├── TestGeneration/ExperimentResultsWriterTests.cs
├── TestGeneration/ExperimentOrchestrationServiceTests.cs
├── AgentTools/ToolAttemptGeneratedTestServiceTests.cs
└── Persistence/

TestMap.IntegrationTests/
├── StaticAnalysis/AssertionLineagePipelineTests.cs
└── Persistence/MigrationSchemaTests.cs

TestMap.EndToEndTests/
└── AssertionLineageExperimentEndToEndTests.cs

Analysis/
├── src/analysis/
│   ├── db.py
│   ├── files.py
│   ├── schema.py
│   ├── normalize.py
│   ├── build_evaluation_dataset.py
│   └── audit_evaluation_data.py
└── tests/
    ├── test_assertion_lineage.py
    ├── test_build_evaluation_dataset.py
    └── test_analysis_fixes.py
```

**Structure Decision**: Keep semantic classification beside existing Roslyn static analysis, keep
attempt orchestration in the existing evaluation services, and add normalized experiment evidence
under the existing EF and domain boundaries. Extract only reusable symbol/document indexing from the
source-test tracer; do not merge the two analyses because whole-test reachability and
assertion-operand lineage answer different questions. Extend the existing canonical results pipeline
and Python analysis package so collection, export, filtering, and publication audits share one
versioned contract.

## Phase 0: Research Decisions

Research is consolidated in [research.md](research.md). The principal decisions are:

1. Build a dedicated `IOperation` and control-flow-graph analyzer; use persisted invocations only to
   reconcile identity because they do not contain data-flow evidence.
2. Replace broad name-only measured classification with a versioned assertion catalog that identifies
   one logical terminal assertion and its observed operand roles.
3. Compute reaching definitions over reachable control-flow blocks. Exclude branch predicates as
   lineage evidence and classify disagreement across feasible definitions or helper returns as
   unresolved.
4. Count local-definition and test-helper transitions against a default four-hop bound; normalize
   expression wrappers without consuming depth; cap paths and detect cycles deterministically.
5. Resolve production versus test support from project/document ownership, not test attributes.
   Record whether traced members match the intended candidate without changing `Traced`, which means
   production-connected.
6. Persist one measurement per attempt, child summaries per generated test, assertion observations,
   and ordered lineage steps. Historical attempts without measurements derive `NotMeasured`.
7. Run one shared analyzer after each lane's existing semantic refresh/link point and before rollback.
   Assertion analysis failures record unavailable evidence and never change generation acceptance.
8. Bump canonical results to schema 4.0, add an assertion-observation schema 1.0 sidecar, retire regex
   assertion counts as publication evidence, and enforce reconciliation through blocking audits.

## Phase 1: Design and Contracts

### Runtime Design

- `AssertionPatternCatalog` matches assertion terminals by resolved containing type, namespace, method
  signature, and style. It supplies operand roles for classic static assertions, constraint forms,
  fluent receiver chains, Shouldly extensions, and exception delegates. Syntactic fallback can
  preserve an occurrence but cannot produce a confident traced or trivial classification when its
  operand shape is unknown.
- `RoslynAssertionLineageAnalysisService` receives the attributable generated-test members, intended
  candidate source member, effective policy, and refreshed semantic project. It finds each logical
  assertion once, builds a control-flow graph per member, and snapshots assertion identity by ordinal,
  span, and expression hash.
- `ReachingDefinitionAnalysis` performs a deterministic forward fixed-point pass over reachable basic
  blocks. Local reads use the definitions reaching that exact operation. Declarator initializers and
  simple local assignments are supported; unsupported writes, aliases, fields, and flow constructs
  produce explicit unresolved terminals.
- The recursive slice unwraps conversions, awaits, tuples, arrays, interpolations, and ordinary
  expressions; follows locals; substitutes helper arguments for parameters; and visits reachable
  helper return values. Direct production invocations, constructions, and value accesses terminate
  as traced.
- Composite operands use `Traced > Unresolved > Trivial`; alternative reaching definitions and helper
  returns require unanimous outcomes or reduce to unresolved. The analyzer does not traverse control
  predicates as evidence.
- The four-hop policy counts each local-definition edge and test-helper boundary; parameter
  substitution belongs to its helper hop. Production reached on hop four is traced. A fifth hop,
  cycle, path cap, ambiguous dispatch, reflection, dynamic call, missing source, or unsupported
  aliasing is unresolved with a stable reason.
- Shared Roslyn utilities provide symbol normalization, document/declaration lookup, production/test
  project classification, overload-safe member resolution, and a precomputed dispatch index.
  Existing source-test mappings retain their current resolver version and behavior.
- In the TestMap lane, analysis runs after project metadata refresh and generated-test member
  resolution while the applied source still exists. The transient analysis result travels with the
  test execution and is persisted after generation-attempt and execution IDs exist.
- In the agent-tool lane, analysis runs after post-attempt project refresh and generated-test linking,
  before build measurement and rollback. It receives the candidate source member directly rather
  than inferring the target from the highest-confidence source-test mapping.
- Persistence writes the attempt measurement, all generated-test summaries, observations, and steps
  atomically. A failed analyzer produces an unavailable measurement or summary with a stable reason;
  it does not erase partial raw artifacts or alter validation/classification.
- Results schema 4.0 adds assertion availability, policy, depth, and category aggregates to attempt
  and generated-test rows. Agent attempt rows aggregate child counts with explicit
  `attempt_aggregate` attribution; child rows remain generated-test grain.
- `AssertionObservationWriter` emits every recognized assertion to a schema-1.0 sidecar with parent
  attempt/candidate/provenance and compact lineage evidence. Traced-only datasets are derived filters;
  raw rows remain canonical.
- The assertion audit validates complete vocabularies, required reasons and policy, count
  reconciliation, owner links, traced terminal evidence, export completeness, and equal shared policy
  across comparable lanes before publication-ready output is accepted.

### Test Design

- Catalog unit tests resolve minimal semantic stubs for xUnit, NUnit, MSTest, FluentAssertions, and
  Shouldly; prove operand selection, one logical fluent assertion, exception delegates, and
  unsupported-shape handling.
- Analyzer fixtures cover direct methods, constructors, properties, awaits, local initializers,
  reassignment, four-hop helpers, production-preserving external transforms, literals, test-local
  expressions, unrelated arrange calls, and production-only control conditions.
- Conservative fixtures cover mixed reaching definitions, loops, flow captures, compound/ref/out
  writes, helper cycles, fifth-hop paths, fields, aliases, reflection, dynamic calls, ambiguous
  dispatch, missing source, and path caps.
- Persistence tests prove exact row grains, immutable source/hash snapshots, ordered branch paths,
  nullable counts for unavailable summaries, owner invariants, cascade behavior, and historical
  `NotMeasured` compatibility.
- Orchestration tests run equivalent source through both lanes, verify identical policy/category
  results before rollback, and prove validation, acceptance, coverage, mutation, retry, and workspace
  behavior are unchanged.
- Export and Python tests require schema 4.0, verify raw and traced-only views, retire regex counts as
  classified evidence, reconcile all grains, and seed every publication-blocking audit defect.
- The end-to-end fixture performs attribution, semantic refresh, classification, persistence, export,
  dataset construction, and audit for both lanes without a live model call.
- Performance validation records analyzer duration separately and compares feature-on/off runs over
  the same frozen pilot cohort, revisions, configuration, ordering, and lane matrix.

## Complexity Tracking

No constitution violations or exceptional architectural complexity are required.
