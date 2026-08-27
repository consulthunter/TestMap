# Phase 0 Research: Non-Trivial Assertion Detection

## Decision 1: Use a Dedicated Assertion-Lineage Analyzer

**Decision**: Add a dedicated analyzer over Roslyn semantic operations and control-flow graphs.
Reuse shared workspace, document, symbol-normalization, and member-index utilities, but do not add
assertion lineage to `RoslynSourceTestTraceService`.

**Rationale**: The existing source-test tracer intentionally skips assertion calls and answers
whole-test reachability. Assertion lineage starts from assertion operands and needs local definitions,
branch joins, helper returns, and explicit dead ends. Persisted invocation rows contain occurrence and
symbol identity but not the data flow required to reconstruct those facts.

**Alternatives considered**:

- Extend source-test mappings: rejected because any arrange/act call could make an unrelated
  `Assert.True(true)` look connected.
- Analyze persisted invocation text: rejected because it loses local assignments, control flow,
  overload-safe operand roles, and helper return evidence.
- Dynamic taint instrumentation: rejected for this version because it changes execution, is difficult
  across reference types and test frameworks, and exceeds the data-dependence requirement.

## Decision 2: Introduce a Versioned Assertion Pattern Catalog

**Decision**: Match logical assertion terminals by semantic framework/type/method identity and record
operand roles in `assertion-catalog-v1`. Cover the styles already claimed by TestMap: xUnit, NUnit,
MSTest, FluentAssertions, and Shouldly. Preserve legacy syntactic recognition only as unresolved
evidence when no reliable operand shape is known.

**Rationale**: The current name-based rules can classify unrelated methods named `Equal`, can count
the preparatory FluentAssertions `Should()` call, and do not distinguish expected, actual, message,
constraint, receiver, or exception-delegate roles. A catalog makes one logical assertion and its
value-bearing inputs reproducible.

**Alternatives considered**:

- Continue broad method-name matching: rejected because false positives and double-counting would
  contaminate the primary assertion grain.
- Require a single framework: rejected because TestMap already analyzes multiple common styles.
- Treat unsupported custom assertions as trivial: rejected because absence of a catalog rule is
  missing semantic knowledge, not evidence of no production lineage.

## Decision 3: Compute Reaching Definitions Over the Control-Flow Graph

**Decision**: Build one control-flow graph per attributable test member and run a forward fixed-point
reaching-definition analysis over reachable basic blocks. Track local declarator initializers,
simple assignments, and flow captures. Snapshot the reaching set immediately before each assertion.

**Rationale**: Lexically walking to the nearest assignment fails at branches, loops, and early exits.
Roslyn's control-flow graph exposes reachable blocks, operations, predecessors, and branch values;
however, it does not directly provide the reaching definition attached to a local use, so TestMap
must compute that lattice. `SemanticModel.AnalyzeDataFlow` summarizes reads and writes for a region
but does not replace per-program-point reaching definitions.

**Alternatives considered**:

- Nearest preceding syntax assignment: rejected as incorrect across branch joins.
- Region-level data-flow summaries alone: rejected because they do not identify which definitions
  reach a particular assertion use.
- Full interprocedural points-to analysis: rejected as unnecessary for the bounded local/helper scope.

**Primary references**:

- [Roslyn ControlFlowGraph](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.flowanalysis.controlflowgraph?view=roslyn-dotnet-5.0.0)
- [Roslyn BasicBlock](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.flowanalysis.basicblock?view=roslyn-dotnet-5.0.0)
- [Roslyn ISimpleAssignmentOperation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.operations.isimpleassignmentoperation?view=roslyn-dotnet-5.0.0)

## Decision 4: Use Separate Composition and Alternative-Path Lattices

**Decision**:

- For components of one expression or separate assertion operands: any confirmed `Traced` input
  establishes production data dependence; otherwise `Unresolved` dominates `Trivial`.
- For mutually exclusive reaching definitions or helper return paths: all feasible alternatives must
  agree. All traced is traced, all trivial is trivial, and every mixture or unresolved alternative is
  unresolved.

**Rationale**: `Assert.Equal(42, result)` must trace when `result` is production-derived even though
the expected literal is local. Conversely, a branch that sometimes assigns a production value and
sometimes a literal must not be optimistically called traced.

**Alternatives considered**:

- Let any possible traced definition win at branch joins: rejected because the runtime assertion may
  observe a non-production definition.
- Require every assertion operand to trace: rejected because normal expected literals would make
  direct value assertions unresolved.

## Decision 5: Define Depth and Unsupported Behavior Precisely

**Decision**: Start assertions at depth zero. Following a local reaching definition consumes one hop;
crossing into a test helper consumes one hop; helper parameter substitution is part of that helper
hop. Conversions, fluent wrappers, `await`, and assertion-owned lambdas consume no hop. Production
reached after the fourth hop is traced; a fifth hop is unresolved. Cycles and a deterministic path cap
are unresolved.

**Rationale**: A stable counting rule is necessary for reproduction and cross-lane fairness.
Expression wrappers should not consume the budget, while transitions that expand analysis scope
should.

**Alternatives considered**:

- Reuse `TargetSelection.MaxTestCodeDepth`: rejected because it governs whole-test source mapping,
  defaults to three, and has different hop semantics.
- Unlimited recursion: rejected because helper cycles and generated control flow can make cost
  unbounded.
- Classify over-depth paths as trivial: rejected because analysis incompleteness is not negative
  evidence.

## Decision 6: Resolve Production Scope From Project Ownership

**Decision**: Determine production and test-support code from solution project/document ownership and
the candidate context. Stop at a uniquely resolved production method, constructor, property, or
field. Pre-index virtual/interface implementations once; ambiguous dispatch is unresolved. Record
whether traced terminals match the intended candidate, another production member, or a mixture.

**Rationale**: Test helpers commonly lack test attributes, so `IsTestMember == false` is not a valid
production definition. Candidate relation is useful evidence, but the feature's `Traced` category
means connection to production code, not necessarily the intended member.

**Alternatives considered**:

- Infer the target from the tool lane's highest-confidence source-test mapping: rejected because that
  mapping may select a different production member than the attempt candidate.
- Enumerate all compilation types for every operand: rejected for performance; dispatch candidates
  should be indexed once.
- Treat any unresolved virtual call as traced: rejected because the production terminal would not be
  reproducible.

## Decision 7: Persist Four Explicit Evidence Grains

**Decision**: Persist an attempt-owned assertion measurement, generated-test summaries, assertion
observations, and ordered lineage steps. Use exactly one parent owner per measurement
(`GenerationAttempt` or `ToolAttempt`). Historical attempts without a measurement are read as
`NotMeasured`; no historical rows are backfilled.

**Rationale**: A measurement row represents no-recognized-assertion and unavailable outcomes even
when there are zero assertion rows. Test summaries preserve multi-test agent attribution.
Observation and step rows preserve raw evidence and branching paths without hiding them in aggregate
JSON. The four grains support exact reconciliation and later policy review.

**Alternatives considered**:

- Add one boolean to generated-test execution: rejected because it collapses multiple assertions,
  mixed categories, missingness, and agent attempts with several tests.
- Store only observations: rejected because no-assertion and whole-test unavailable cases would
  disappear.
- Reuse mutable invocation/source-test mapping rows as evidence: rejected because later project
  refreshes can replace mappings and invocations do not snapshot attempt-specific lineage.

## Decision 8: Integrate Both Lanes Before Rollback

**Decision**: Use the same service and effective policy in both lanes. In the TestMap lane, classify
after project metadata refresh and generated-test member resolution, carrying the transient result
until the generation attempt and execution are persisted. In the agent-tool lane, classify linked
generated-test members after post-attempt analysis/linking and before measurement cleanup and
rollback. Persist an unavailable attempt measurement when required linkage or semantic evidence is
missing.

**Rationale**: These points provide fresh semantic source, stable member attribution, and the
candidate identity while preserving existing workspace isolation. Assertion results remain
observational and cannot affect acceptance or repair stopping.

**Alternatives considered**:

- Analyze after rollback: rejected because generated source no longer exists.
- Run separate analyzers per lane: rejected because policy drift would invalidate comparison.
- Fail generation acceptance when analysis is unavailable: rejected because the feature is a
  measurement, and acceptance semantics are explicitly unchanged.

## Decision 9: Version Results and Retire Heuristic Counts for Claims

**Decision**: Bump the existing results contract from 3.0 to 4.0, add assertion measurement columns to
attempt and generated-test rows, and emit a schema-1.0 assertion-observation sidecar. Update the
manifest with policy/catalog/depth and the data-dependence limitation. The Python pipeline uses
semantic observation rows for classified evidence; its existing regex fallback remains legacy-only
and cannot satisfy publication audits.

**Rationale**: New columns and filtering semantics change the canonical data contract. Explicit
versions prevent historical missingness from being interpreted as zero and make downstream
notebooks opt into the new claim.

**Alternatives considered**:

- Keep results schema 3.0 and add optional columns: rejected because older readers currently require
  exact schema semantics and could silently reinterpret missing evidence.
- Export only traced counts: rejected because exclusions would be irreconcilable.
- Continue regex counts for LLM tests: rejected because text occurrence is not semantic lineage and
  the LLM execution now has member attribution.

## Decision 10: Measure Cost Separately and In Context

**Decision**: Record assertion-analysis duration per attempt. Add a deterministic 1,000-assertion
fixture as a regression sentinel, then validate the specification's 10% limit with paired
feature-off/feature-on runs over the same frozen multi-repository pilot, revisions, configuration,
ordering, and lane matrix.

**Rationale**: Microbenchmarks identify algorithmic regressions but cannot establish end-to-end
research cost. Paired pilot runs control the major sources of attempt-duration variation.

**Alternatives considered**:

- Use only the synthetic fixture: rejected because it does not represent solution loading, helper
  shapes, or lane orchestration.
- Compare unrelated experiment runs: rejected because model, repository, cache, and environment
  differences would confound the result.
