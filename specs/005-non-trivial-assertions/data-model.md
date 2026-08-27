# Data Model: Non-Trivial Assertion Detection

## Grain Overview

| Entity | Grain | Purpose |
|---|---|---|
| Assertion Lineage Measurement | One evaluation attempt under one effective policy | Represents availability and reconciled attempt aggregates, including zero-assertion and unavailable cases |
| Generated-Test Assertion Summary | One generated or modified test within a measurement | Preserves test-specific status and category counts, including multi-test agent attempts |
| Assertion Observation | One recognized logical assertion occurrence | Stores the primary `Traced`, `Trivial`, or `Unresolved` observation |
| Assertion Lineage Step | One ordered step on one operand path of one assertion | Preserves auditable definitions, helper transitions, terminals, and unresolved reasons |
| Assertion Measurement Policy | One immutable code-defined policy version | Defines catalog version, default depth, hop accounting, lattice, limitations, and aggregation |

## Assertion Lineage Measurement

### Fields

| Field | Type | Rules |
|---|---|---|
| Id | integer | Primary key |
| ProjectId | integer | Required project owner |
| ExperimentRunId | integer | Required experiment owner |
| CandidateMethodId | integer | Required selected candidate |
| GenerationAttemptId | integer, nullable | Set only for the TestMap lane |
| ToolAttemptId | integer, nullable | Set only for the agent-tool lane |
| ProducerLane | string | Required stable lane vocabulary |
| Status | enum string | `Complete`, `Partial`, `Unavailable`, or `NotApplicable`; historical `NotMeasured` is derived when no row exists |
| FailureCode | string, nullable | Required when status is `Unavailable` or `Partial` because of a collection failure |
| FailureReason | string, nullable | Sanitized explanation corresponding to the failure code |
| PolicyVersion | string | Required immutable lineage-policy version |
| AssertionCatalogVersion | string | Required immutable catalog version |
| MaxDepth | integer | Required effective bound; positive and shared across comparable lanes |
| EligibleTestCount | integer, nullable | Null when availability prevents determining eligibility |
| AnalyzedTestCount | integer, nullable | Null when unavailable |
| UnavailableTestCount | integer, nullable | Null when unavailable at attempt scope |
| NoRecognizedAssertionTestCount | integer, nullable | Null when unavailable |
| RecognizedAssertionCount | integer, nullable | Null when unavailable |
| UnrecognizedAssertionCount | integer, nullable | Null when unsupported occurrences cannot be inventoried |
| TracedAssertionCount | integer, nullable | Null when unavailable |
| TrivialAssertionCount | integer, nullable | Null when unavailable |
| UnresolvedAssertionCount | integer, nullable | Null when unavailable |
| AnalysisDurationMs | real, nullable | Measured analyzer time, independent of generation and validation duration |
| StartedAt / CompletedAt | timestamps | UTC measurement interval |

### Ownership and Validation

- Exactly one of `GenerationAttemptId` and `ToolAttemptId` is non-null.
- One measurement is allowed per parent attempt, policy version, catalog version, and effective depth.
- Complete counts are non-negative and satisfy:
  `RecognizedAssertionCount = TracedAssertionCount + TrivialAssertionCount + UnresolvedAssertionCount`.
- `AnalyzedTestCount + UnavailableTestCount = EligibleTestCount` when all three are available.
- Counts are null, not zero, when the attempt measurement is unavailable.
- Parent-attempt deletion cascades to measurement evidence.
- The parent attempt supplies repository revision, configuration, lane model/tool, and canonical
  attempt identity; the measurement snapshots the policy that interpreted the source.

## Generated-Test Assertion Summary

### Fields

| Field | Type | Rules |
|---|---|---|
| Id | integer | Primary key |
| AssertionLineageMeasurementId | integer | Required parent |
| TestMemberId | integer, nullable | Resolved generated/modified test member when available |
| GeneratedTestExecutionId | integer, nullable | TestMap-lane owner when available |
| ToolAttemptGeneratedTestId | integer, nullable | Agent-tool child owner when available |
| TestMethodName | string | Snapshot; required when known |
| TestFilePath | string | Snapshot; required when known |
| TestMemberContentHash | string | Snapshot binding evidence to analyzed source |
| FallbackIdentityHash | string | Deterministic identity when no member row is available |
| Status | enum string | `Classified`, `NoRecognizedAssertions`, or `Unavailable` |
| StatusReason | string, nullable | Required for unavailable; optional stable explanation for no recognized assertions |
| RecognizedAssertionCount | integer, nullable | Null when unavailable |
| UnrecognizedAssertionCount | integer, nullable | Count of candidate assertion forms unsupported by the measured catalog |
| TracedAssertionCount | integer, nullable | Null when unavailable |
| TrivialAssertionCount | integer, nullable | Null when unavailable |
| UnresolvedAssertionCount | integer, nullable | Null when unavailable |

### Ownership and Validation

- A classified or no-recognized summary belongs to exactly one generated-test owner when that owner
  exists; unavailable fallback summaries may use the attempt parent plus `FallbackIdentityHash`.
- Within a measurement, a resolved test member is unique.
- `Classified` requires at least one recognized assertion and exact category reconciliation.
- `NoRecognizedAssertions` requires recognized and category counts equal to zero; it is not a
  successful or trivial classification.
- `Unavailable` requires a reason and null category counts.
- Attempt aggregates equal the sum of non-null child counts. Unavailable child tests are counted
  separately and never contribute zero assertions.

## Assertion Observation

### Fields

| Field | Type | Rules |
|---|---|---|
| Id | integer | Primary key |
| GeneratedTestAssertionSummaryId | integer | Required parent summary |
| InvocationId | integer, nullable | Optional reconciliation link to the existing invocation inventory |
| Ordinal | integer | Zero-based lexical logical-assertion order within the test member |
| Framework | string | Catalog framework/style identifier |
| AssertionMethod | string | Resolved terminal assertion signature or stable fallback |
| RecognitionKind | enum string | `Semantic` or `SyntacticFallback` |
| FilePath | string | Attempt-time source snapshot |
| StartLine / StartColumn / EndLine / EndColumn | integers | Zero-based source span |
| ExpressionHash | string | Hash of normalized assertion source and location |
| Category | enum string | Exactly one of `Traced`, `Trivial`, or `Unresolved` |
| ResolutionCode | string | Stable reason such as `ProductionInvocation`, `AllInputsTestLocal`, `DepthExceeded`, or `AmbiguousDefinitions` |
| DepthReached | integer | Maximum followed hop depth |
| TargetRelation | enum string | `Candidate`, `OtherProduction`, `MixedProduction`, `NoProduction`, or `Unknown` |
| TraceSummary | string | Deterministic, sanitized human-readable trace |

### Identity and Validation

- `(GeneratedTestAssertionSummaryId, Ordinal)` is unique.
- `(GeneratedTestAssertionSummaryId, ExpressionHash)` prevents accidental duplicate logical
  assertions after fluent-chain normalization.
- `Traced` requires at least one terminal production-member lineage step.
- `Trivial` requires all terminal paths to be complete test-local/literal terminals.
- `Unresolved` requires at least one unresolved terminal and no definitely traced assertion operand.
- A syntactic fallback with unknown operand roles cannot be `Traced` or `Trivial`.
- `InvocationId` is not the assertion identity because later static-analysis refreshes may update
  inventory rows; span, ordinal, expression hash, and content hash form the immutable snapshot.

## Assertion Lineage Step

### Fields

| Field | Type | Rules |
|---|---|---|
| Id | integer | Primary key |
| AssertionObservationId | integer | Required parent |
| InputIndex | integer | Assertion observed-operand index |
| PathIndex | integer | Deterministic branch/alternative path index |
| StepIndex | integer | Ordered step index within the path |
| StepKind | enum string | `AssertionInput`, `LocalRead`, `ReachingDefinition`, `Expression`, `HelperCall`, `HelperReturn`, `ProductionMember`, `Literal`, `TestLocal`, `DepthLimit`, `Cycle`, `AmbiguousDispatch`, `Unsupported`, or `MissingSource` |
| Depth | integer | Hop depth at this step |
| SymbolDisplay | string | Stable semantic symbol display when available |
| MemberId | integer, nullable | Resolved member for helper or production steps |
| FilePath / source span | nullable location | Evidence location when available |
| Outcome | enum string | `Continue`, `Traced`, `Trivial`, or `Unresolved` |
| ReasonCode | string | Stable reason vocabulary |
| Summary | string | Deterministic sanitized explanation |

### Ordering and Validation

- `(AssertionObservationId, InputIndex, PathIndex, StepIndex)` is unique.
- Step indexes are contiguous within a path.
- Every path has exactly one terminal step.
- Terminal `ProductionMember` steps have outcome `Traced` and a resolved production `MemberId`.
- Depth-limit, cycle, ambiguous-dispatch, unsupported, and missing-source terminals have outcome
  `Unresolved`.
- Literal and completely analyzed test-local terminals have outcome `Trivial`.

## Assertion Measurement Policy

The policy is an immutable code-defined descriptor rather than a user-editable database row.
Every measurement snapshots:

- policy version (`assertion-lineage-v1`);
- catalog version (`assertion-catalog-v1`);
- effective maximum depth (default `4`);
- hop-accounting rule;
- assertion and alternative-path reduction lattices;
- supported assertion styles;
- path cap and cycle semantics;
- data-dependence-only limitation;
- explicit separation from mutation sensitivity and acceptance.

Changing category meaning, hop accounting, supported terminal semantics, or aggregation requires a
new policy or catalog version. Increasing catalog coverage without changing existing rule meaning
requires at least a catalog-version change.

## State Transitions

```text
Attempt has materialized changes
  -> measurement pending
     -> Complete
        -> child Classified / NoRecognizedAssertions
     -> Partial
        -> some children classified, at least one Unavailable
     -> Unavailable
        -> no trustworthy classification counts
     -> NotApplicable
        -> attempt produced no eligible generated/modified test artifact

No measurement row on a historical attempt
  -> derived NotMeasured
```

Persisted measurements and observations are immutable attempt evidence. A new analysis policy creates
a new measurement identity rather than overwriting prior classifications.

## Relationships

```text
ExperimentRun
  └── CandidateMethod
      ├── GenerationAttempt ─┐
      └── ToolAttempt ───────┴── AssertionLineageMeasurement
                                  └── GeneratedTestAssertionSummary
                                      └── AssertionObservation
                                          └── AssertionLineageStep
```

The generated-test summary optionally links to `GeneratedTestExecution` or
`ToolAttemptGeneratedTest`. Candidate and attempt ownership remain authoritative; source-test
mappings may provide context but never substitute for the attempt's intended candidate.

## Compatibility

- The migration adds tables and indexes without backfilling historical categories.
- Readers derive `NotMeasured` when an eligible historical attempt has no measurement row.
- Existing `invocations.is_assertion` remains an occurrence inventory, not classified lineage.
- Existing source-test mappings retain their whole-test reachability meaning.
- Existing coverage, mutation, validation, classification, and acceptance columns do not change.
- Results schema 4.0 and assertion-observation schema 1.0 are explicit opt-ins for new analyses.
