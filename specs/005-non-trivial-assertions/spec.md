# Feature Specification: Non-Trivial Assertion Detection

**Feature Branch**: `005-non-trivial-assertions`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "Classify generated-test assertions by whether their asserted values have a backward data-flow lineage to production code, while preserving trivial, traced/non-trivial, unresolved, and unavailable outcomes for defensible generated-test evaluation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Classify Assertion Lineage (Priority: P1)

As a researcher evaluating generated tests, I need each recognized assertion classified according to
whether the value it observes is derived from production code, so that syntactically valid but
meaningless assertions do not inflate test-quality claims.

**Why this priority**: Assertion-level lineage is the core measurement. Without it, downstream
filtering and reporting cannot distinguish production-connected assertions from constants and
test-local computations.

**Independent Test**: Analyze a fixture containing direct production calls, local-variable
assignments, test-helper returns, literal assertions, ambiguous assignments, and over-depth paths;
verify that every recognized assertion receives the expected category and evidence.

**Acceptance Scenarios**:

1. **Given** an assertion whose observed value directly comes from a production member, **When** the
   assertion is analyzed, **Then** it is classified as traced/non-trivial and records that production
   member as lineage evidence.
2. **Given** an assertion whose observed value is stored in a local variable assigned from a
   production member, **When** the assertion is analyzed, **Then** the assignment is followed and the
   assertion is classified as traced/non-trivial.
3. **Given** an assertion whose observed value comes from a test helper that returns a
   production-derived value within the configured depth, **When** the assertion is analyzed, **Then**
   the helper path is followed and the assertion is classified as traced/non-trivial.
4. **Given** an assertion whose observed inputs contain only literals or test-local computations,
   **When** the complete bounded analysis finds no production lineage, **Then** it is classified as
   trivial.
5. **Given** an assertion whose lineage exceeds the configured depth or cannot be resolved
   unambiguously, **When** the assertion is analyzed, **Then** it is classified as unresolved rather
   than trivial or traced.

---

### User Story 2 - Audit and Filter Quality Evidence (Priority: P2)

As a researcher preparing generated-test results, I need raw assertion observations and filtered
non-trivial counts to remain reconcilable, so that exclusions are visible and reviewers can reproduce
the reported quality measures.

**Why this priority**: A useful classifier must not silently discard trivial, unresolved, missing, or
unsupported observations. The filter is scientifically defensible only when its inputs, policy, and
aggregation are auditable.

**Independent Test**: Produce results for generated tests containing traced, trivial, unresolved,
mixed, and no recognized assertions; verify that raw counts reconcile with categorized counts and
that the traced-only view includes only assertions with confirmed production lineage.

**Acceptance Scenarios**:

1. **Given** a generated test containing multiple recognized assertions with different categories,
   **When** results are summarized, **Then** each assertion remains a separate observation and the
   generated-test summary reports the count of each category.
2. **Given** a generated test with no recognized assertions, **When** results are summarized,
   **Then** it receives an explicit no-recognized-assertions status rather than a zero-quality or
   successful classification.
3. **Given** analysis cannot run because required source or semantic evidence is unavailable,
   **When** results are recorded, **Then** measurement availability and its reason are retained and
   no assertion is inferred to be trivial.
4. **Given** a traced-only quality report, **When** assertions are selected, **Then** only
   traced/non-trivial assertion observations are included and trivial, unresolved, unavailable, and
   no-recognized-assertion observations remain separately countable.

---

### User Story 3 - Preserve Fair Experimental Interpretation (Priority: P3)

As a research reviewer, I need assertion lineage to use the same definitions across generation lanes
and remain distinct from coverage and mutation sensitivity, so that comparisons do not overstate
what the new measurement proves.

**Why this priority**: Production lineage establishes connection, not fault sensitivity or complete
oracle strength. Shared policy and explicit metric boundaries protect lane fairness and prevent
invalid claims.

**Independent Test**: Analyze equivalent generated test code attributed to the same candidate in
both supported generation lanes; verify identical assertion categories and policy provenance, while
coverage, mutation, validation, and acceptance outcomes remain independently reported.

**Acceptance Scenarios**:

1. **Given** equivalent assertions produced through different generation lanes, **When** they are
   analyzed under the same policy version and depth, **Then** they receive the same lineage category
   and evidence semantics.
2. **Given** a traced assertion with no observed mutation improvement, **When** results are reported,
   **Then** it may be described as production-connected but not as fault-sensitive.
3. **Given** a mutation score increase alongside only trivial or unresolved assertions, **When**
   results are reported, **Then** the mutation result remains available but is not attributed to a
   discriminating assertion.
4. **Given** an agentic attempt that creates multiple tests, **When** assertion results are
   aggregated, **Then** assertion observations remain test-specific while attempt-scoped impact
   measurements remain attributed to the overall attempt unless independently measured per-test
   evidence exists.

### Edge Cases

- When a local has multiple feasible reaching assignments, it is traced only when a definite
  production-derived reaching value can be established without contradictory or unknown alternatives;
  otherwise its lineage is unresolved.
- Reassignments use the reaching value applicable at the assertion, not an earlier shadowed value.
- Cycles among test helpers terminate at the configured depth and are classified as unresolved.
- Ambiguous dynamic dispatch, reflection, runtime-generated values, unsupported indirection, and
  unavailable referenced source produce unresolved lineage rather than a guessed target.
- Awaited values, exception assertions, fluent assertion chains, properties, and constructors follow
  the same category vocabulary when their observed value or operation can be identified.
- Unsupported or custom assertion forms are not silently treated as trivial; supported assertion
  forms and non-recognition counts are disclosed with the measurement policy.
- A production call made only during arrange or act does not make an unrelated literal assertion
  traced.
- A production-derived value used only as a branch condition does not establish assertion data
  lineage in this version.
- A self-comparison or otherwise weak oracle involving a production-derived value is traced under
  this lineage definition; the result must not be presented as proof of logical strength.
- Analysis of a generated test that was not successfully applied or cannot be associated with a test
  member is unavailable with a reason, not trivial.
- Historical observations created before this measurement exists remain distinguishable from newly
  measured trivial or unresolved assertions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST identify recognized assertion occurrences within each generated or
  modified test member attributable to an evaluation attempt and retain an assertion-level identity
  and source location.
- **FR-002**: For each recognized assertion, the system MUST analyze every value-bearing input that
  participates in the asserted observation.
- **FR-003**: When an assertion input references a local value, the system MUST follow the applicable
  reaching assignment or assignments backward from the assertion.
- **FR-004**: The system MUST recursively inspect the expression that produced each followed value.
- **FR-005**: A confirmed call, construction, or value access into production code in the followed
  lineage MUST establish production lineage for that assertion input.
- **FR-006**: When a followed value is returned by test-support code, the system MUST continue through
  that helper within the configured analysis depth.
- **FR-007**: The analysis depth MUST be bounded, MUST default to four inter-member or assignment
  hops, and MUST be recorded with a named measurement-policy version.
- **FR-008**: Every recognized assertion with available analysis evidence MUST receive exactly one
  category: `Traced`, `Trivial`, or `Unresolved`.
- **FR-009**: An assertion MUST be `Traced` when at least one observed input has a confirmed,
  unambiguous data-lineage path to production code.
- **FR-010**: An assertion MUST be `Trivial` only when all observed inputs have been completely
  analyzed within the configured bound and every lineage ends in literals or test-local computation
  without a production symbol.
- **FR-011**: An assertion MUST be `Unresolved` when no observed input is confirmed as traced and at
  least one lineage cannot be completed because of depth, ambiguity, unsupported indirection, missing
  source, or another documented analysis limitation.
- **FR-012**: Mixed feasible definitions for the same observed value MUST be classified
  conservatively as unresolved unless all relevant definitions support the same definitive
  classification.
- **FR-013**: Production-derived control conditions MUST NOT by themselves establish traced assertion
  data lineage in this version.
- **FR-014**: The system MUST distinguish assertion classification from measurement availability.
  Analysis that cannot start or complete at the test-member level MUST be recorded as unavailable
  with a reason and MUST NOT be converted to a classification.
- **FR-015**: Each assertion observation MUST retain the generated test, evaluation attempt, intended
  candidate source member, resolved production member or members when available, category, confidence
  or resolution status, depth used, policy version, and a reproducible trace summary.
- **FR-016**: Generated-test summaries MUST separately report traced, trivial, and unresolved
  assertion counts, plus explicit statuses for no recognized assertions and unavailable analysis.
- **FR-017**: Raw and traced-only assertion measures MUST both be available, with an aggregation rule
  that reconciles assertion counts to generated tests and attempts without changing observation
  grain.
- **FR-018**: Trivial or unresolved assertion observations MUST remain retained for audit and MUST
  NOT be silently deleted by filtering.
- **FR-019**: Assertion lineage MUST use the same policy version, category definitions, depth, and
  supported assertion catalog across comparable generation lanes.
- **FR-020**: For attempts that create multiple tests, assertion lineage MUST remain attributed to
  each generated test; attempt-level coverage or mutation impact MUST NOT be reassigned to individual
  assertions or tests without independent per-test measurement.
- **FR-021**: Assertion lineage, validation success, coverage impact, and mutation impact MUST remain
  separate measurements. No one measurement MUST be inferred from another.
- **FR-022**: This version MUST NOT change generation acceptance or candidate eligibility solely
  because an assertion is trivial or unresolved; any later use as a gate requires a separately
  versioned policy.
- **FR-023**: Existing observations without assertion-lineage evidence MUST remain readable and MUST
  be represented as not measured under this policy rather than as zero, trivial, or unresolved.
- **FR-024**: Reports and exports that expose assertion-lineage results MUST include category
  definitions, measurement availability, policy version, and enough identifiers to reproduce joins
  to repository revision, candidate, test, attempt, lane, model or tool, and configuration.
- **FR-025**: Required assertion-analysis or export failures MUST be visible and MUST prevent a
  publication-ready dataset from passing its integrity audit when categorized assertion evidence is
  claimed.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Prevent syntactic assertion counts and passing tests from being interpreted as
  meaningful oracle evidence when asserted values have no demonstrated production-code lineage. The
  measurement supports a defensible production-connected assertion subset while explicitly not
  claiming logical strength or mutant sensitivity.
- **Unit of analysis**: The primary unit is one recognized assertion occurrence in one generated test
  member. Generated-test and attempt summaries are derived aggregations that retain category counts.
  Attempt-scoped mutation or coverage remains at attempt scope when per-test attribution is
  unavailable.
- **Sampling and eligibility**: All recognized assertions in successfully materialized generated or
  modified test members attributable to eligible evaluation attempts are in the assertion sampling
  frame. Unsupported assertion syntax, unavailable source, and failed test-member attribution are
  reported as non-recognition or unavailable measurement rather than silently excluded. This feature
  does not alter candidate selection, cohort membership, or randomization.
- **Comparison contract**: Comparable lanes use the same immutable candidate cohort, repository
  revision, assertion catalog, analysis depth, category rules, and measurement-policy version.
  Lane-specific production mechanisms do not change shared classification semantics. Multiple tests
  created by one agentic attempt retain test-level assertion rows and attempt-level impact
  attribution.
- **Measurement contract**: `Traced` means at least one assertion input has confirmed data lineage to
  production code. `Trivial` means all analyzed inputs definitively terminate without production
  lineage. `Unresolved` means no traced input was confirmed and at least one lineage is incomplete or
  ambiguous. `Unavailable` is a measurement status, not an assertion category. A generated test is
  included in the traced-only assertion subset when it contains at least one traced assertion;
  category counts remain available for mixed tests. No control-dependence, logical-strength, or
  mutation-kill attribution claim is implied.
- **Missingness and failures**: Unavailable analysis retains a reason such as missing source,
  unsupported test-member attribution, failed semantic loading, or required analysis failure.
  Unrecognized assertion forms are counted separately. Missing and unresolved values are never
  converted to trivial, zero, or traced. Required collection failures fail visibly.
- **Provenance contract**: Every assertion observation is traceable to repository identity and exact
  commit, project and run, candidate source member, generated test member, generation attempt, lane,
  model or tool identity, configuration snapshot, supported-assertion catalog, depth setting, and
  measurement-policy version. Trace evidence identifies the resolved production lineage when found.
- **Compatibility**: Existing validation, coverage, mutation, cohorts, attempt outcomes, and
  acceptance semantics retain their meanings. Historical data has assertion-lineage availability
  marked as not measured. New assertion-level and derived fields require an explicit schema and
  export-contract version; incompatible downstream analyses must opt into the new contract rather
  than silently reinterpret old rows.
- **Audit criteria**: A publication-ready dataset is blocked when assertion rows cannot reconcile to
  generated-test summaries, required provenance or policy versions are missing, categories fall
  outside the complete vocabulary, unavailable observations lack reasons, lane policies differ in a
  shared comparison, traced rows lack reproducible lineage evidence, or historical missingness is
  represented as an observed category.

### Key Entities *(include if feature involves data)*

- **Assertion Observation**: One recognized assertion occurrence in one generated test, including its
  location, category, measurement status, intended candidate, policy provenance, and trace summary.
- **Assertion Lineage Step**: One auditable step from an asserted input through a value definition or
  helper boundary toward a production member or documented dead end.
- **Generated-Test Assertion Summary**: Derived per-test counts of traced, trivial, and unresolved
  assertions, together with no-recognized-assertion or unavailable status.
- **Assertion Measurement Policy**: The named version of category rules, supported assertion forms,
  depth bound, scope limitations, and aggregation semantics used for an observation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A deterministic fixture suite covering direct calls, local assignments, helper returns,
  literal-only assertions, reassignment, mixed reaching definitions, recursion cycles, ambiguous
  dispatch, over-depth paths, and unavailable source classifies 100% of recognized assertions
  according to the specified outcome vocabulary.
- **SC-002**: In every completed pilot run, 100% of recognized assertion occurrences in eligible,
  available generated test members have exactly one assertion category and complete policy
  provenance.
- **SC-003**: For every generated-test and attempt summary, raw assertion totals reconcile exactly to
  traced, trivial, and unresolved counts; no-recognized-assertion and unavailable cases reconcile
  separately.
- **SC-004**: Integrity audits detect 100% of seeded missing-reason, invalid-category,
  missing-policy-version, broken-lineage, and cross-lane policy-mismatch defects before a dataset is
  declared publication-ready.
- **SC-005**: Equivalent test and candidate fixtures evaluated through each supported generation lane
  produce identical assertion categories and trace conclusions in 100% of comparison cases.
- **SC-006**: Existing historical observations remain queryable with 100% of absent assertion-lineage
  measurements represented as not measured, never as observed zero, trivial, traced, or unresolved.
- **SC-007**: On the representative multi-repository pilot set, adding assertion-lineage measurement
  increases median end-to-end attempt duration by no more than 10%.
- **SC-008**: Every result presentation that uses a traced-only assertion subset also displays or
  links to the excluded trivial, unresolved, unavailable, and non-recognized counts and states that
  lineage does not establish mutation sensitivity or complete oracle strength.

## Assumptions

- The first version evaluates generated or modified tests within TestMap's currently supported
  statically analyzable language scope and for which source context is available.
- The supported assertion catalog covers the test frameworks and assertion styles already recognized
  by TestMap; additions to that catalog are policy-versioned.
- Four hops is the default backward-lineage bound. Experiments may configure a different bound only
  when the chosen value is captured in policy provenance and held constant across comparable lanes.
- Production code means analyzed code in the target repository and revision that is outside the
  identified test and test-support scope.
- Interprocedural analysis in this version follows value-returning test helpers. Custom assertion
  wrappers, field-sensitive aliasing, reflection, and other unsupported indirection may remain
  unresolved.
- Control dependence is explicitly outside this version. A production-derived branch condition alone
  does not make an assertion traced.
- The operational term "non-trivial" means production-connected by data lineage only. It does not
  detect all logical tautologies; for example, a self-comparison of the same production-derived value
  may still be traced.
- Assertion-lineage results are observational evidence for filtering and reporting, not a generation
  acceptance gate in this version.
- No historical backfill is required. Previously collected observations remain available with an
  explicit not-measured status.
