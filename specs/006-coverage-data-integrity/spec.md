# Feature Specification: Coverage Data Integrity

**Feature Branch**: `006-coverage-data-integrity`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "Correct coverage collection, persistence, attribution, and reporting so failed collection is never reported as coverage, provider fallback works, raw observations are not silently dropped, exact counters and constructors are retained, and corrected validation data can be reproduced."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Preserve Every Coverage Observation (Priority: P1)

As a researcher collecting coverage, I need every parsed class and member observation retained before
source attribution, so that unresolved mappings remain visible and later analysis can distinguish
missing evidence from evidence that could not be matched.

**Why this priority**: The current workflow silently drops observations when a class or member cannot
be matched. That loss cannot be reconstructed from the resulting database and can bias coverage-based
selection, measurement, and reporting.

**Independent Test**: Process a coverage fixture containing matched classes and members, unmatched
classes and members, overloaded methods, instance constructors, static constructors, and
out-of-project entries; verify that every parsed observation is retained exactly once with an
explicit attribution outcome and that mapped and unmapped totals reconcile to the raw input.

**Acceptance Scenarios**:

1. **Given** a parsed member observation that matches a known source member, **When** coverage is
   recorded, **Then** the raw observation is retained and linked to that member.
2. **Given** a parsed class or member observation that has no source match, **When** coverage is
   recorded, **Then** it is retained with no source link and an explicit unmatched reason.
3. **Given** an instance or static constructor in coverage data, **When** attribution runs, **Then**
   the constructor is considered for matching and is not discarded solely because it is a
   constructor.
4. **Given** several overloads or constructors with the same display name, **When** attribution runs,
   **Then** each observation is matched using its available identity evidence or remains explicitly
   ambiguous rather than being guessed or dropped.

---

### User Story 2 - Report Truthful Collection Outcomes (Priority: P1)

As an operator running repository validation, I need collection failures, empty results, and usable
coverage to have distinct outcomes, so that `HasCoverage` never claims success when no usable
in-project coverage was collected.

**Why this priority**: In the validation cohort, 147 of 344 selected repositories were reported as
having coverage without usable member or class coverage. Treating failure as success corrupts cohort
selection and every downstream claim that relies on coverage availability.

**Independent Test**: Run fixtures for an unavailable provider, a failed provider, no artifacts, an
empty parsed report, an all-unmatched report, and a report with mapped in-project observations;
verify the complete outcome vocabulary and the derived `HasCoverage` value in every case.

**Acceptance Scenarios**:

1. **Given** collection cannot start or fails before producing an artifact, **When** the result is
   recorded, **Then** the failure and reason are explicit and `HasCoverage` is false.
2. **Given** a report exists but contains no usable mapped in-project observations, **When** the
   result is recorded, **Then** the report remains auditable, its outcome states that no usable
   coverage was collected, and `HasCoverage` is false.
3. **Given** at least one valid in-project coverage observation is successfully parsed and
   attributed, **When** the result is recorded, **Then** the outcome states that usable coverage was
   collected and `HasCoverage` is true.
4. **Given** test execution fails but still produces a valid coverage artifact, **When** collection
   completes, **Then** test failure and coverage availability are reported independently.

---

### User Story 3 - Recover Through Provider Fallback (Priority: P1)

As an operator collecting coverage across diverse repositories, I need the configured preferred
provider to fall back to the default provider when it is unavailable, fails, or produces no usable
artifact, so that provider-specific gaps do not silently eliminate otherwise collectible coverage.

**Why this priority**: Most false coverage claims in the validation data never attempted a fallback,
including 82 cases where the first provider was unavailable.

**Independent Test**: Run a repository fixture where the preferred provider is unavailable and the
default provider succeeds, plus fixtures where the preferred command fails, returns no artifact, or
succeeds; verify attempt order, final outcome, and preserved evidence for every provider attempt.

**Acceptance Scenarios**:

1. **Given** the preferred provider is unavailable, **When** coverage is requested, **Then** the
   default provider is attempted and both attempt outcomes are retained.
2. **Given** the preferred provider command returns an error and no usable coverage artifact, **When**
   collection continues, **Then** the default provider is still attempted.
3. **Given** the preferred provider produces usable coverage, **When** collection completes, **Then**
   no unnecessary fallback is required and the successful provider is identified.
4. **Given** all permitted providers fail or produce no usable coverage, **When** collection
   completes, **Then** the final result is an explicit unavailable or failed outcome rather than an
   empty successful report.

---

### User Story 4 - Retain Complete Measurements (Priority: P2)

As a data analyst, I need exact covered and valid line and branch counts alongside rates, so that I
can audit calculations, aggregate correctly, and avoid interpreting absent counters as observed
zeros.

**Why this priority**: Existing entity rows retain rates while line and branch counters are zero,
making 245,806 coverage observations internally incomplete and preventing trustworthy aggregation.

**Independent Test**: Process a deterministic report with known line and branch totals at member and
class scope; verify that exact covered and valid counts survive collection, attribution, persistence,
and export and reconcile with the reported rates.

**Acceptance Scenarios**:

1. **Given** a parsed observation with covered and valid line counts, **When** it is recorded and
   exported, **Then** both counts retain their original values at the observation's declared scope.
2. **Given** a parsed observation with covered and valid branch counts, **When** it is recorded and
   exported, **Then** both counts retain their original values at the observation's declared scope.
3. **Given** a source format that does not provide a counter, **When** the observation is recorded,
   **Then** that counter is unavailable with a reason rather than stored as an observed zero.
4. **Given** exact counts and a derived rate, **When** integrity validation runs, **Then** impossible
   values or inconsistent calculations are flagged.

---

### User Story 5 - Merge Only the Intended Coverage Evidence (Priority: P2)

As an operator running multi-project or multi-target builds, I need all distinct reports from the
current collection attempt included even when they share a filename, so that valid coverage is not
lost or contaminated by stale artifacts.

**Why this priority**: Different projects and target frameworks commonly emit the same report
filename. Deduplicating by filename can discard distinct data, while reusing old outputs can blend
evidence from different attempts.

**Independent Test**: Collect two different current-run reports with the same filename, an identical
duplicate of one report, and a stale report from an earlier run; verify that both distinct reports
are included once, the content-identical duplicate is identified, and the stale report is excluded.

**Acceptance Scenarios**:

1. **Given** two current-run reports with the same filename but different content, **When** reports
   are combined, **Then** both contribute to the collection result.
2. **Given** two current-run artifacts with identical coverage content, **When** reports are combined,
   **Then** they contribute once and the duplicate decision is auditable.
3. **Given** a report left by an earlier attempt, **When** the current attempt is combined, **Then**
   the stale report does not contribute to the current result.

---

### User Story 6 - Publish a Corrected Validation Cohort (Priority: P3)

As a research owner, I need historical affected results clearly identified and the selected validation
cohort recollected under the corrected policy, so that old missing data is not silently reinterpreted
and new analysis is based on comparable evidence.

**Why this priority**: The 147 repositories without usable coverage require recollection, but the 197
repositories with coverage are also incomplete because counters, constructors, unmatched entries, or
same-name reports may have been lost. Existing databases generally do not contain enough raw evidence
to repair these defects in place.

**Independent Test**: Validate the corrected behavior on one previously unsuccessful repository and
one previously successful repository, then recollect all 344 selected repositories and run integrity
audits that distinguish legacy unavailable fields from newly observed zeros.

**Acceptance Scenarios**:

1. **Given** a historical result created before this policy, **When** it is read, **Then** absent
   counters and raw observations are identified as legacy not-measured evidence rather than inferred
   zeros.
2. **Given** the previously unsuccessful `microsoft/artifacts-credprovider` validation target, **When**
   it is recollected, **Then** provider fallback is attempted when required and the final coverage
   outcome truthfully reflects the artifacts obtained.
3. **Given** the previously successful `nbuilder/nbuilder` validation target, **When** it is
   recollected, **Then** its usable coverage remains available and all newly required integrity
   fields reconcile.
4. **Given** the corrected policy has passed canary validation, **When** the 344-repository selected
   cohort is recollected, **Then** every repository has a complete collection outcome and all usable
   coverage satisfies the new integrity audit before comparative analysis resumes.

### Edge Cases

- A report that parses successfully but contains no classes or members is retained as an empty parsed
  report and does not make `HasCoverage` true.
- A report containing only external, generated, unsupported, or otherwise out-of-project code retains
  those observations with explicit classification and does not make in-project coverage available.
- A partially attributable report may make `HasCoverage` true when at least one valid in-project
  observation is mapped, while all unmatched observations and their reasons remain countable.
- Missing filenames, normalized-name collisions, nested types, compiler-generated names, explicit
  interface members, generic members, overloads, instance constructors, and static constructors must
  resolve deterministically or remain explicitly unmatched or ambiguous.
- A provider may return a failing test outcome and still leave a usable artifact; provider-attempt,
  test-execution, artifact, parsing, and attribution outcomes remain separate.
- A preferred-provider-only mode suppresses fallback only when the operator explicitly selects that
  mode; merely naming a preferred provider does not suppress the default fallback.
- Reports with a shared filename are never considered duplicates on filename alone.
- Repeated observations from distinct modules, projects, target frameworks, or attempts retain their
  scope and are not collapsed merely because their source identities look alike.
- Covered counts may be zero, but valid counts and availability must establish whether that zero was
  observed. Covered counts cannot exceed corresponding valid counts.
- A collection that is interrupted after raw persistence but before attribution remains recoverable
  and visibly incomplete rather than appearing to have no observations.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST record a distinct coverage collection outcome for each requested
  repository, project, and collection attempt.
- **FR-002**: The collection outcome vocabulary MUST distinguish at least not attempted, provider
  unavailable, collection failed, no artifact, artifact parse failed, parsed with no usable
  in-project coverage, partially attributed usable coverage, and fully attributed usable coverage.
- **FR-003**: Every non-success collection outcome MUST retain a reason and the stage and provider at
  which it occurred when known.
- **FR-004**: `HasCoverage` MUST be true only when the collection contains at least one valid,
  attributed, in-project coverage observation usable by downstream coverage analysis.
- **FR-005**: The existence of a report record, an empty artifact, an all-unmatched report, or a
  failed collection placeholder MUST NOT by itself make `HasCoverage` true.
- **FR-006**: Test execution outcome, provider-attempt outcome, artifact availability, parse outcome,
  attribution outcome, and final coverage availability MUST remain separately reportable.
- **FR-007**: Coverage collection MUST attempt the default fallback provider when the preferred
  provider is unavailable, fails, or produces no usable artifact, unless an explicit provider-only
  policy is selected.
- **FR-008**: Fallback eligibility MUST depend on usable artifact availability, not solely on the
  preferred provider command's return status.
- **FR-009**: Every provider attempt MUST retain its provider identity, order, outcome, failure reason,
  and produced-artifact identities.
- **FR-010**: The final collection result MUST identify which provider or providers supplied the
  retained coverage evidence.
- **FR-011**: Only artifacts attributable to the current collection attempt MUST contribute to that
  attempt's coverage result.
- **FR-012**: Distinct current-attempt reports MUST be retained even when their filenames are
  identical.
- **FR-013**: Duplicate elimination MUST be based on equivalent report content and scope rather than
  filename alone, and every eliminated duplicate MUST be auditable.
- **FR-014**: Every successfully parsed class and member observation MUST be durably retained before
  source attribution can remove or transform it.
- **FR-015**: Raw observations MUST retain sufficient original identity and scope evidence to audit
  attribution and support a later remapping attempt, including report, provider, project or module,
  class, member, signature when available, source file when available, and measurement scope.
- **FR-016**: Source class and member links on coverage observations MUST allow no match; a missing
  link MUST NOT cause the observation to be deleted.
- **FR-017**: Every raw observation MUST receive exactly one attribution outcome, including matched,
  unmatched, ambiguous, out-of-project, unsupported, or pending after interruption, with a reason
  wherever the outcome is not matched.
- **FR-018**: Attribution summaries MUST reconcile raw observation counts exactly to matched and each
  non-matched outcome at class and member scope.
- **FR-019**: Instance and static constructors MUST be eligible for attribution under the same
  auditable rules as other members and MUST NOT be unconditionally excluded.
- **FR-020**: Overloaded members and constructors MUST use available identity evidence to avoid
  incorrect matches; insufficient or conflicting evidence MUST produce an explicit ambiguous or
  unmatched outcome.
- **FR-021**: Each class and member observation MUST retain exact covered and valid line counts when
  supplied by the coverage source.
- **FR-022**: Each class and member observation MUST retain exact covered and valid branch counts when
  supplied by the coverage source.
- **FR-023**: A counter not supplied by the coverage source MUST be represented as unavailable rather
  than an observed zero.
- **FR-024**: Rates derived from exact counts MUST declare their scope and MUST reconcile with those
  counts within the documented rounding policy.
- **FR-025**: Coverage consumers that select candidates, calculate gaps, score risk, or report mapped
  source coverage MUST use valid attributed in-project observations and MUST NOT treat unmatched raw
  observations as candidate coverage.
- **FR-026**: Constructor observations MAY contribute to source coverage summaries but MUST NOT enter
  a method-only candidate pool unless a separately versioned eligibility policy explicitly permits
  constructors.
- **FR-027**: Reports and exports MUST expose final coverage availability, collection and attribution
  outcomes, exact available counters, unavailable-value reasons, policy version, and raw-to-mapped
  reconciliation counts at their declared unit of analysis.
- **FR-028**: Historical records that lack raw observations or exact counters MUST remain readable and
  MUST identify those fields as legacy not measured rather than factual zeros or complete mappings.
- **FR-029**: Incompatible corrected semantics MUST receive an explicit schema or measurement-policy
  version so historical and corrected datasets cannot be silently combined.
- **FR-030**: The corrected 344-repository validation cohort MUST be produced by recollection from the
  pinned repository revisions because missing raw evidence MUST NOT be fabricated from incomplete
  historical rows.
- **FR-031**: A publication-ready dataset MUST fail its integrity audit when it contains false
  `HasCoverage` claims, unexplained non-success outcomes, unreconciled observations, impossible
  counters, missing required provenance, cross-attempt artifacts, or silent legacy-to-corrected
  reinterpretation.
- **FR-032**: Static-analysis results unaffected by coverage collection MAY remain reusable, but any
  coverage-dependent cohort, metric, export, or claim MUST use corrected coverage evidence or be
  clearly marked as based on the legacy policy.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Prevent collection failures, empty reports, lost artifacts, dropped raw
  entries, absent counters, and excluded constructors from biasing coverage availability and
  coverage-dependent MSR or generated-test analyses. The feature makes missingness and attribution
  loss observable instead of silently turning them into success or zero.
- **Unit of analysis**: Provider attempts and artifacts are attempt-scoped; parsed observations are
  class- or member-scoped within one report and collection attempt; attribution links an observation
  to at most one source entity under the applicable policy; repository summaries are explicit
  aggregations and never substitute for observation-grain evidence.
- **Sampling and eligibility**: Coverage collection does not change the selected repository cohort.
  Coverage-dependent candidate eligibility uses usable attributed in-project observations only.
  Unmatched, external, unsupported, and unavailable observations remain in the audit population but
  are excluded from mapped candidate scoring under an explicit reason.
- **Comparison contract**: Comparable runs and experiment lanes must use the same coverage
  availability definition, provider fallback policy, artifact-isolation rule, attribution policy,
  counter semantics, and schema version. Existing retry, budget, source-revision, and lane-isolation
  rules remain unchanged.
- **Measurement contract**: `HasCoverage` means at least one valid attributed in-project coverage
  observation. Exact covered and valid counts retain their source scope; rates are derived or
  source-reported measurements with a declared rounding policy. Raw, matched, unmatched, and excluded
  counts must reconcile. Collection success does not imply test success, complete attribution, or
  complete source coverage.
- **Missingness and failures**: Collection and attribution use the complete outcome vocabulary in
  FR-002 and FR-017. Missing artifacts, unavailable providers, parse failures, absent counters,
  ambiguous mappings, and legacy absence retain reasons where known. Missing values are never
  converted to zero, and failure placeholders are never converted to coverage success.
- **Provenance contract**: Every attempt and observation is traceable to normalized repository
  identity, exact commit, project or module, run and attempt identifiers, provider and provider order,
  artifact identity, collection and attribution policy versions, source scope, and the configuration
  needed to reproduce selection, fallback, merging, and mapping decisions.
- **Compatibility**: Existing databases and exports remain readable, but legacy zero counters and
  absent raw observations are classified as not measured unless supported by retained original
  artifacts. Coverage-dependent cohorts, training frames, risk scores, before/after comparisons, and
  notebooks must opt into corrected semantics or disclose legacy policy use. The corrected validation
  corpus requires recollecting all 344 selected repositories; mutation attribution and mutant-line
  offset defects are outside this feature.
- **Audit criteria**: Analysis is blocked when coverage availability lacks usable evidence, provider
  attempts are missing, current-run artifact scope is unprovable, raw counts do not reconcile,
  constructors or unmapped observations disappear without an outcome, exact available counters are
  lost, unavailable values appear as zero, policy or revision provenance is missing, or historical
  and corrected semantics are combined without an explicit compatibility decision.

### Key Entities *(include if feature involves data)*

- **Coverage Collection Attempt**: One request to collect coverage for a pinned target, including test
  outcome, provider attempts, final availability, reasons, and policy provenance.
- **Coverage Provider Attempt**: One ordered use of a coverage provider, including its outcome and the
  artifacts it produced or failed to produce.
- **Coverage Artifact**: One current-attempt report with content identity, source scope, parse outcome,
  and duplicate relationship when applicable.
- **Raw Coverage Observation**: One parsed class or member measurement retained independently of
  whether it can be attributed, including exact available counters and original identity evidence.
- **Coverage Attribution**: The policy-versioned outcome linking a raw observation to a source class
  or member or recording why no safe link was made.
- **Coverage Integrity Summary**: A derived attempt, project, or repository summary that reconciles
  artifacts and raw, matched, unmatched, excluded, and unavailable observations without changing
  their grain.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Across deterministic fixtures for every collection and attribution outcome, 100% of
  `HasCoverage` values agree with the presence or absence of at least one usable attributed
  in-project observation.
- **SC-002**: In fixtures where the preferred provider is unavailable, fails, or produces no usable
  artifact, the default provider is attempted in 100% of cases unless provider-only mode is
  explicitly selected, and every attempt outcome is retained.
- **SC-003**: For every processed fixture and corrected validation run, 100% of parsed class and member
  observations reconcile exactly to matched, unmatched, ambiguous, out-of-project, unsupported, or
  pending outcomes; none disappear silently.
- **SC-004**: A deterministic fixture containing instance constructors, static constructors, and
  overloaded constructors retains 100% of their coverage observations and either attributes each
  correctly or records an explicit non-match reason.
- **SC-005**: For every observation whose source report provides line or branch counters, 100% of
  covered and valid values survive persistence and export unchanged and pass covered-not-greater-than-
  valid and rate-reconciliation checks.
- **SC-006**: A merge fixture with two distinct same-name reports, one content-identical duplicate,
  and one stale report includes both distinct current-run reports exactly once, records the duplicate,
  and excludes the stale report.
- **SC-007**: Recollection of the pinned `microsoft/artifacts-credprovider` canary at commit
  `df94ab890995c300ca293d3e8f06a3dad77fdff2` records the required fallback attempt when the preferred
  provider produces no usable artifact and reports the resulting availability truthfully.
- **SC-008**: Recollection of the pinned `nbuilder/nbuilder` canary at commit
  `2769d20e112b56201873ddd3397e7dd8dd3e93a6` preserves usable in-project coverage and passes all new
  artifact, observation, counter, constructor, attribution, and availability audits.
- **SC-009**: All 344 selected validation repositories are recollected from their pinned revisions;
  100% receive a complete collection outcome and 100% of repositories reported with `HasCoverage`
  pass the corrected integrity audit before the cohort is used for comparative analysis.
- **SC-010**: Integrity audits detect 100% of seeded false-success, missing-fallback, same-filename
  loss, stale-artifact, silent-drop, constructor-exclusion, missing-counter, impossible-counter,
  unreconciled-total, and legacy-as-zero defects.
- **SC-011**: Historical results remain queryable with 100% of unsupported absent counters and raw
  observations represented as legacy not measured, never as newly observed zero or complete
  attribution.

## Assumptions

- A usable coverage observation is a valid parsed class or member observation attributed to source
  code in the pinned target repository and accepted by the active coverage policy.
- Naming a preferred provider expresses attempt order; it does not disable the default fallback.
  Operators who require one provider may use an explicit provider-only policy.
- Raw persistence precedes attribution so an interrupted or improved mapping process can be audited or
  resumed without recollecting when the complete original evidence is present.
- Existing static-analysis evidence may be reused when its repository identity and pinned commit
  match, but corrected coverage evidence is recollected for all 344 selected validation repositories.
- `microsoft/artifacts-credprovider` and `nbuilder/nbuilder` are the initial failure and regression
  canaries respectively; broader acceptance requires the full selected cohort.
- Coverage-dependent candidate scoring remains based on attributed source members. Unmatched raw
  observations and constructors do not become candidate methods merely because they are now retained.
- Mutation attribution, mutant source-line offsets, and unrelated test-discovery defects are outside
  this feature and require separate specifications.
