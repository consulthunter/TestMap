# Assertion Lineage Measurement and Export Contract

## Contract Versions

- **Measurement policy**: `assertion-lineage-v1`
- **Assertion catalog**: `assertion-catalog-v1`
- **Experiment results CSV**: `results_schema_version = 4.0`
- **Assertion observation CSV**: `assertion_schema_version = 1.0`

These values are immutable for the semantics below. A consumer must reject unsupported versions
rather than infer compatibility.

## Category Contract

Each recognized assertion with available analysis has exactly one category:

| Category | Required meaning |
|---|---|
| `Traced` | At least one observed operand has a definite data-lineage path to a uniquely resolved production member |
| `Trivial` | Every observed operand was completely analyzed within policy and all paths terminate in literals or test-local computation |
| `Unresolved` | No operand is definitely traced and at least one required path is incomplete, ambiguous, unsupported, cyclic, missing, or over depth |

`Unavailable` and `NotMeasured` are measurement statuses, never assertion categories.
`NoRecognizedAssertions` is a generated-test summary status, never a zero-quality category.

## Reduction Contract

### Components of One Expression or Assertion

| Inputs | Result |
|---|---|
| Any definite `Traced` input | `Traced` |
| No `Traced`, at least one `Unresolved` | `Unresolved` |
| All `Trivial` | `Trivial` |

### Alternative Definitions or Helper Returns

| Feasible alternatives | Result |
|---|---|
| All `Traced` | `Traced` |
| All `Trivial` | `Trivial` |
| Any mixture or any `Unresolved` | `Unresolved` |

Branch predicates determine feasible definitions but are not themselves assertion-lineage evidence.

## Hop Contract

- Assertion operands begin at depth zero.
- Local read to reaching definition consumes one hop.
- Test-helper call to helper body/return consumes one hop.
- Helper parameter substitution is included in the helper hop.
- Conversions, parentheses, `await`, fluent assertion wrappers, and assertion-owned lambdas do not
  consume a hop.
- Production reached after hop four is traced under the default policy.
- A required fifth hop is `Unresolved` with `DepthExceeded`.
- Cycles and path-cap exhaustion are unresolved.

## Required Status Vocabulary

### Measurement

`Complete`, `Partial`, `Unavailable`, `NotApplicable`, and derived `NotMeasured`.

### Generated-test summary

`Classified`, `NoRecognizedAssertions`, and `Unavailable`.

### Stable unresolved reasons

At minimum:

- `DepthExceeded`
- `CycleDetected`
- `AmbiguousDefinitions`
- `AmbiguousDispatch`
- `DynamicInvocation`
- `Reflection`
- `UnknownDelegateTarget`
- `UnsupportedAssertionShape`
- `UnsupportedWrite`
- `UnsupportedAlias`
- `UnsupportedFieldFlow`
- `MissingDefinition`
- `MissingSource`
- `SemanticModelUnavailable`
- `ProjectLoadFailure`
- `PathCapExceeded`

### Stable unavailable reasons

At minimum:

- `NoAppliedTestArtifact`
- `GeneratedTestMemberUnresolved`
- `PostAttemptAnalysisSkipped`
- `SemanticProjectUnavailable`
- `SourceUnavailable`
- `AnalysisDisabled`
- `AnalysisFailure`
- `HistoricalNotMeasured`

Free-text summaries may accompany these codes but cannot replace them.

## Results Schema 4.0

Existing attempt, generated-test, and test-result row kinds remain. Attempt and generated-test rows
add:

- `assertion_measurement_status`
- `assertion_measurement_reason`
- `assertion_policy_version`
- `assertion_catalog_version`
- `assertion_max_depth`
- `recognized_assertion_count`
- `unrecognized_assertion_count`
- `traced_assertion_count`
- `trivial_assertion_count`
- `unresolved_assertion_count`
- `no_recognized_assertions`
- `assertion_analysis_duration_ms`
- `assertion_attribution`

### Row semantics

- TestMap attempt rows carry their generated test's summary or unavailable status.
- Agent-tool attempt rows carry explicit sums across generated-test children and use
  `assertion_attribution = attempt_aggregate`.
- Agent-tool generated-test rows carry the child summary and use
  `assertion_attribution = generated_test`.
- Test-result rows do not acquire assertion-level mutation or coverage attribution.
- Unavailable category counts are blank, never zero.
- Historical schema-3 rows are accepted only in an explicit legacy analysis path and derive
  `NotMeasured`.

## Assertion Observation Sidecar 1.0

The companion file name is derived by replacing the results file's trailing `.csv` with
`.assertions.csv`. It contains one row per recognized assertion and includes:

- assertion schema version and observation ID;
- canonical attempt ID, experiment run ID/UID/series, lane, model/tool, and attempt number;
- repository identity, exact resolved commit, target and manifest/source hashes;
- candidate method ID and intended source member ID;
- generated-test summary, execution/tool-child, and member IDs when available;
- test member name, file, content hash, assertion ordinal and source span;
- framework/style, assertion method, recognition kind, and expression hash;
- category, resolution code, target relation, and depth reached;
- resolved production member IDs;
- policy version, catalog version, and effective maximum depth;
- deterministic trace summary and serialized ordered lineage paths;
- analysis timestamp.

The sidecar never drops trivial or unresolved assertions. A traced-only view is derived with
`category == "Traced"`; canonical raw data remains unchanged.

## Reconciliation Invariants

For every classified generated-test summary:

```text
recognized = traced + trivial + unresolved
```

For every attempt measurement:

```text
recognized = sum(child recognized)
traced     = sum(child traced)
trivial    = sum(child trivial)
unresolved = sum(child unresolved)
```

The number of assertion sidecar rows for a generated-test summary equals its recognized count.
Unavailable children are counted separately and do not contribute zero assertions.

## Manifest Contract

The results manifest records:

- results and assertion-sidecar schema versions and paths;
- assertion policy and catalog versions;
- effective depth and path cap;
- category and status definitions;
- data-dependence-only scope;
- no control-dependence credit;
- the statement that traced lineage does not establish logical strength or mutation sensitivity;
- the statement that coverage/mutation remain at their independently declared scopes;
- whether strict assertion publication audit passed.

## Publication Audit

The assertion evidence audit blocks publication-ready status when any of the following occurs:

- unsupported or blank schema/policy/catalog version;
- invalid category or status vocabulary;
- unavailable/partial evidence without a stable reason;
- missing or duplicate assertion identity;
- orphan summary, observation, or lineage step;
- non-contiguous path steps or missing path terminal;
- traced assertion without a terminal resolved production member;
- trivial assertion with unresolved or production terminal evidence;
- failed per-test or attempt reconciliation;
- sidecar row count or identity mismatch;
- cross-lane policy/catalog/depth mismatch for comparable candidates;
- historical missingness represented as zero or an observed category;
- assertion sidecar or manifest generation failure when assertion evidence is claimed.

## Compatibility and Non-Claims

- Results schema 3.0 does not contain classified assertion lineage.
- Regex or invocation-count fallback may support legacy inventory displays but cannot populate
  `Traced`, `Trivial`, or `Unresolved` for publication analysis.
- Schema-4 consumers must preserve `Unavailable`, `NotApplicable`, and derived `NotMeasured` as
  distinct missingness states; none may be converted to observed zero counts.
- A traced-only analytical view is a filter over the immutable raw observation grain, never a
  replacement for or rewrite of that grain.
- Lineage does not prove the assertion distinguishes correct from faulty behavior.
- Mutation results do not prove the assertion caused a mutant kill; uncaught exceptions and other
  effects remain separate.
- This contract does not change generation acceptance, repair stopping, candidate eligibility,
  coverage calculation, or mutation calculation.
