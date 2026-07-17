# Feature Specification: Pinned Repository Targets

**Feature Branch**: `not created - no before-specify branch hook configured`

**Created**: 2026-07-16

**Status**: Draft

**Input**: Define immutable repository-plus-commit evaluation targets, create target manifests from
CSV data, verify availability, materialize exact revisions, isolate revision data, and enforce the
pinned commit throughout TestMap experiments.

## Clarifications

### Session 2026-07-16

- Q: Which fields from `licenseFilteredRepoList.csv` are required for target creation? → A: Use
  `name` and `lastCommitSHA`, derive the canonical repository URL from `name`, and preserve the input
  filename and row number for provenance.
- Q: How is the delimiter determined for repository import files? → A: Automatically detect comma-
  or tab-delimited input, allow an explicit delimiter override, and fail visibly when detection is
  ambiguous.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create a Reproducible Target Manifest (Priority: P1)

As a researcher, I can create a versioned target manifest from a repository dataset using its
`owner/repository` name and last commit SHA so the complete experimental sampling frame is fixed
before evaluation begins.

**Why this priority**: A stable manifest is the provenance root for every later clone, cohort,
attempt, result, exclusion, and analysis.

**Independent Test**: Provide a CSV containing valid, duplicate, and malformed repository rows.
Create a manifest and verify that valid targets are normalized and deterministically ordered,
duplicates are removed, source rows remain traceable, and rejected rows are reported with reasons.

**Acceptance Scenarios**:

1. **Given** a delimited repository file with `name` and `lastCommitSHA` columns, **When** the
   researcher creates a target manifest, **Then** each valid unique repository revision appears
   exactly once and its canonical repository URL is derived from `name`.
2. **Given** two rows that normalize to the same repository and commit, **When** the manifest is
   created, **Then** one target is emitted and both source rows remain auditable.
3. **Given** a row with a missing or malformed URL, repository identity, or commit, **When** the
   manifest is created, **Then** the row is excluded from the manifest and included in a rejection
   report with its source row and reason.
4. **Given** the same CSV and creation options, **When** the manifest is created repeatedly, **Then**
   its target content and ordering are identical.
5. **Given** a repository file whose `.csv` extension contains tab-delimited records, **When** the
   manifest is created without a delimiter override, **Then** the header and records are parsed using
   the detected tab delimiter.
6. **Given** input for which comma-versus-tab detection is ambiguous, **When** no delimiter override
   is supplied, **Then** manifest creation fails visibly without silently misreading columns.

---

### User Story 2 - Verify the Sampling Frame Before a Run (Priority: P1)

As a researcher, I can verify every declared target before scheduling an evaluation so inaccessible
repositories and unavailable commits remain visible rather than silently disappearing.

**Why this priority**: Silent target loss changes the sampling frame and can bias repository-level
and cross-repository findings.

**Independent Test**: Verify a manifest containing an available target, an inaccessible repository,
an authentication failure, and an unavailable commit; confirm one status row exists for every target
and that the original manifest remains unchanged.

**Acceptance Scenarios**:

1. **Given** a target whose repository and exact commit are accessible, **When** verification runs,
   **Then** it is reported as available with the resolved commit equal to the requested commit.
2. **Given** an inaccessible repository, unavailable commit, or authentication failure, **When**
   verification runs, **Then** the target receives a distinct explicit status and is not removed.
3. **Given** a target whose repository identity disagrees with its URL, **When** verification runs,
   **Then** it is reported invalid before materialization is attempted.

---

### User Story 3 - Materialize the Exact Repository Revision (Priority: P1)

As a researcher, I can run TestMap against the exact commit declared in the target manifest, whether
the repository must be acquired or an existing local copy can be reused.

**Why this priority**: Recording whatever revision happens to be present is not reproducibility;
the requested revision must govern extraction, baseline construction, and evaluation.

**Independent Test**: Start with no local repository, then with a matching existing repository, a
repository at the wrong commit, and a repository with the wrong remote. Confirm that only the exact
declared revision is accepted and that failures stop that target's pipeline.

**Acceptance Scenarios**:

1. **Given** no local repository and an accessible target, **When** its pipeline starts, **Then** the
   repository is materialized at the exact requested commit in a clean state.
2. **Given** a valid existing local repository at another revision, **When** the target is
   materialized, **Then** the requested commit is resolved and becomes the verified base revision.
3. **Given** an existing repository whose configured origin does not match the target, **When** the
   target is materialized, **Then** the pipeline fails visibly without analyzing the unrelated copy.
4. **Given** a requested commit that cannot be obtained, **When** materialization runs, **Then** the
   target is reported as commit-unavailable and no later extraction or evaluation step runs.

---

### User Story 4 - Enforce Workspace Integrity Throughout Evaluation (Priority: P1)

As a researcher, I can trust that every lane and attempt starts from the same pinned source revision
and that an agent cannot silently move the repository's revision during evaluation.

**Why this priority**: A verified initial checkout is insufficient if later rollback follows a moved
`HEAD` or if measurements are taken from a different source revision.

**Independent Test**: Run attempts that only modify tests, that move `HEAD`, that create commits,
and that fail mid-run. Confirm valid working-tree edits can be measured, revision movement is marked
as an integrity failure, and every subsequent attempt begins at the pinned commit.

**Acceptance Scenarios**:

1. **Given** a clean pinned workspace, **When** an attempt modifies test files without changing the
   repository revision, **Then** the change can proceed to post-attempt analysis and measurement.
2. **Given** an attempt that moves `HEAD` or changes the checked-out revision, **When** TestMap checks
   the workspace, **Then** the attempt is classified as a workspace-integrity failure and its metric
   result is not accepted as comparable evidence.
3. **Given** any completed, failed, timed-out, or crashed attempt, **When** cleanup finishes, **Then**
   the working tree is restored to the declared base commit before the next attempt.
4. **Given** an integrity check that cannot establish the current revision, **When** a protected
   evaluation boundary is reached, **Then** processing stops rather than assuming integrity.

---

### User Story 5 - Evaluate Multiple Revisions Without Collision (Priority: P2)

As a researcher, I can evaluate multiple commits of one repository without one revision reusing or
overwriting another revision's source graph, database, artifacts, cohorts, or results.

**Why this priority**: Repository-name-only storage can mix observations from different source
states while still producing plausible outputs.

**Independent Test**: Evaluate two commits of one repository in either order and verify that each
revision has distinct persisted analysis and artifacts, while results retain a common repository
identity and distinct revision identity.

**Acceptance Scenarios**:

1. **Given** two targets with the same repository and different commits, **When** both are processed,
   **Then** each receives an isolated workspace, persistence scope, and artifact scope.
2. **Given** a cohort created for one commit, **When** reuse is attempted for another commit, **Then**
   reuse is rejected even when repository, objective, and selection settings otherwise match.
3. **Given** result exports from multiple revisions, **When** they are combined, **Then** repository
   identity and exact revision identity remain independently available for grouping and joins.

### Edge Cases

- The manifest is empty, unreadable, uses an unsupported schema version, or contains no valid rows.
- A commit value is abbreviated, malformed, all zeroes, or valid in shape but does not identify a
  commit object.
- A repository is renamed, transferred, deleted, archived, made private, or redirects to another URL.
- Equivalent repository URLs differ by case, trailing suffix, or transport form.
- Credentials allow repository discovery but not retrieval of the requested commit.
- The requested commit exists locally but is no longer advertised by the remote.
- A local target directory exists but is not a repository, is corrupt, has the wrong origin, or has
  uncommitted user changes.
- A network interruption occurs after partial acquisition or verification.
- Two input rows normalize to the same repository and commit and therefore contribute multiple
  logical source-record numbers to one target.
- Two target-processing workers request the same repository revision concurrently.
- An attempt changes the current branch, creates a commit, rewrites repository metadata, deletes the
  repository metadata directory, or leaves locked files during rollback.
- Test/build tooling produces ignored files while the pinned source revision remains unchanged.
- A result write is attempted after workspace integrity has failed.
- A legacy URL-only target list is supplied to measured experiment mode.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: TestMap MUST accept a versioned target manifest whose entries include repository
  identity, repository URL, and an exact full commit identifier.
- **FR-002**: TestMap MUST treat normalized repository identity plus exact commit as the immutable
  identity of an evaluation target.
- **FR-003**: TestMap MUST reject measured experiment targets that omit an exact commit; it MUST NOT
  resolve an omitted commit implicitly when an experiment starts.
- **FR-004**: Researchers MUST be able to create a target manifest from a delimited repository file
  containing a `name` column in `owner/repository` form and a `lastCommitSHA` column containing the
  exact full commit identifier. Manifest creation MUST derive the canonical repository URL from
  `name`; an explicit URL column MUST NOT be required for this import contract.
- **FR-005**: Manifest creation MUST automatically detect comma- or tab-delimited input from file
  content rather than filename extension. Researchers MUST be able to override the detected
  delimiter explicitly. Ambiguous or unsupported delimiter detection MUST fail visibly before any
  target records are emitted.
- **FR-006**: Manifest creation MUST validate required values, normalize identities and URLs,
  deduplicate targets by repository revision, and produce deterministic target ordering.
- **FR-007**: Manifest creation MUST preserve source-file identity and source row numbers so emitted,
  deduplicated, and rejected targets can be traced to their input records. Other source columns,
  including repository ID, branch, license, size, activity, popularity, and archival fields, MUST NOT
  be copied into the target manifest. A source row number is the one-based logical data-record number
  excluding the header; an embedded line break in a quoted field does not create another record.
- **FR-008**: Invalid or incomplete input rows MUST be written to a rejection report with a stable
  reason category and human-readable explanation.
- **FR-009**: The manifest MUST record its schema version, creation time, source description, and a
  fingerprint of the source data used to create it.
- **FR-010**: Researchers MUST be able to verify a manifest independently of running extraction or
  evaluation.
- **FR-011**: Verification MUST produce exactly one status record per manifest target and MUST retain
  unavailable targets in the report.
- **FR-012**: Verification MUST distinguish at least valid, invalid target, repository unavailable,
  authentication failed, and commit unavailable outcomes.
- **FR-013**: A target may be reported available only when the resolved commit exactly equals the
  requested commit and the repository identity agrees with the repository URL.
- **FR-014**: Target verification MUST NOT modify the target manifest.
- **FR-015**: Before source extraction, TestMap MUST materialize and verify the exact requested commit
  in a clean workspace.
- **FR-016**: TestMap MAY reuse an existing repository only after confirming repository validity,
  origin identity, requested commit availability, and absence of unapproved user changes.
- **FR-017**: TestMap MUST fail the target pipeline visibly when repository acquisition,
  materialization, or commit verification fails; it MUST NOT continue against a missing, stale,
  default-branch, or unrelated workspace.
- **FR-018**: A successful materialization MUST record requested commit, resolved commit, repository
  identity, target identity, manifest fingerprint, materialization time, and integrity status.
- **FR-019**: TestMap MUST isolate workspaces, persisted analysis, and generated artifacts by
  repository revision so two commits of one repository cannot collide.
- **FR-020**: The verified base commit MUST remain immutable in run state throughout extraction,
  baseline collection, cohort selection, generation, post-attempt analysis, measurement, and export.
- **FR-021**: TestMap MUST verify repository revision integrity before source extraction, before
  baseline collection, at experiment startup, before every LLM or agentic attempt, after an attempt
  returns and before post-attempt analysis, after rollback, and before final result publication.
- **FR-022**: Integrity verification MUST permit expected working-tree changes while requiring the
  repository's current revision to remain the pinned base commit.
- **FR-023**: Moving the repository revision, creating or checking out another commit, or making the
  current revision unverifiable MUST produce an explicit workspace-integrity outcome.
- **FR-024**: Evidence measured after a workspace-integrity failure MUST NOT be classified as a
  comparable success or positive-impact result.
- **FR-025**: Cleanup after every attempt MUST restore tracked content to the pinned base commit and
  remove attempt-created untracked and generated build artifacts before the next attempt.
- **FR-026**: Cleanup MUST verify the restored revision and fail visibly if the pinned state cannot be
  re-established.
- **FR-027**: Cohort creation and reuse, resume identities, attempt identities, canonical result rows,
  and analysis repository keys MUST use the verified resolved commit from the same run provenance.
- **FR-028**: Cohorts MUST NOT be reused across different commits of the same repository.
- **FR-029**: Result exports MUST include target identity, repository identity, requested commit,
  resolved commit, manifest fingerprint, and workspace-integrity status.
- **FR-030**: Required provenance fields MUST be unavailable only with an explicit failure outcome;
  successful evaluated attempts MUST NOT contain an empty or inferred commit.
- **FR-031**: TestMap MUST record unavailable or invalid targets as sampling-frame outcomes rather
  than silently excluding them from evaluation scope counts.
- **FR-032**: Every measured run MUST publish exactly one terminal target-execution status for every
  manifest target, including failures that occur before repository materialization or database
  initialization.
- **FR-033**: Logs and status reports MUST not expose repository credentials or other secrets.
- **FR-034**: URL-only target lists MUST be rejected by measured experiment mode because they do not
  provide immutable revision identity. Continued support for URL-only lists in discovery-only
  workflows is outside this feature's experiment contract.
- **FR-035**: Existing result or database artifacts whose revision provenance cannot be verified MUST
  not be relabeled as conforming pinned-target data.
- **FR-036**: The target manifest, rejection report, verification report, target-execution report,
  run provenance, and result
  data MUST carry schema or policy versions sufficient to distinguish incompatible future semantics.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Make the evaluated object a verifiable repository snapshot and prevent
  revision drift, stale clone reuse, and cross-revision persistence from changing experimental
  conclusions.
- **Unit of analysis**: A target is one repository revision. Integrity observations occur at run and
  attempt boundaries; outcomes remain attached to their existing attempt, candidate, and repository
  grains.
- **Sampling and eligibility**: The manifest is the declared sampling frame. A target is execution-
  eligible only when its repository identity and exact commit are valid and available. Ineligible
  targets remain counted with explicit reasons. Candidate eligibility and randomization occur only
  after the target revision is materialized and verified.
- **Comparison contract**: All LLM and agentic lanes for a target use the same resolved commit,
  baseline data, and isolated revision namespace. Expected working-tree test edits are allowed;
  revision movement is not. Every attempt is restored to the pinned base before the next begins.
- **Measurement contract**: Baseline and post-attempt evidence is valid only when both are associated
  with the same verified target revision. Integrity failure makes impact unevaluable regardless of
  whether tests happened to compile or metrics happened to change.
- **Missingness and failures**: Repository unavailable, authentication failed, commit unavailable,
  invalid target, materialization failed, and workspace integrity failed are explicit outcomes.
  None are represented as success, zero metric change, or silent exclusion.
- **Provenance contract**: Target identity, normalized repository identity, requested and resolved
  commits, manifest fingerprint, materialization time, integrity status, configuration and policy
  versions, and existing run/attempt identifiers are retained in canonical evidence.
- **Compatibility**: Pinned-target results introduce a new provenance contract. Historical artifacts
  lacking verifiable target and resolved-commit evidence remain usable only as legacy data and MUST
  NOT be silently mixed into analyses requiring pinned-target compliance.
- **Audit criteria**: Requested/resolved commit mismatch, empty commit on successful rows, manifest
  fingerprint disagreement, cohort revision mismatch, duplicate target identity, workspace drift,
  cross-revision path collision, and missing target-status records are blocking audit failures.

### Key Entities *(include if feature involves data)*

- **Target Manifest**: Versioned declaration of the experimental sampling frame, its source
  fingerprint, creation metadata, and ordered repository-revision targets.
- **Repository Target**: Immutable repository-plus-commit target with normalized identity, derived
  canonical URL, requested commit, input filename, and source row number.
- **Target Verification Record**: One target's availability and identity-check outcome, resolved
  commit when available, timestamp, and failure reason when unavailable.
- **Target Rejection Record**: Invalid source row, source location, stable rejection category, and
  explanation produced during manifest creation.
- **Materialized Revision**: Verified local realization of a target, including requested and resolved
  commits, isolated locations, materialization time, and current integrity status.
- **Workspace Integrity Observation**: Boundary-specific evidence that the workspace revision equals
  the pinned base commit, with status and failure details.
- **Run Provenance**: Immutable target and manifest fields shared by cohorts, attempts, result rows,
  audits, and downstream analysis.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Repeated manifest creation from identical input and options produces identical ordered
  target content in 100% of validation runs.
- **SC-002**: Every input CSV row is accounted for as emitted, deduplicated, or rejected, with no
  unclassified rows in fixture and pilot audits.
- **SC-003**: The example tab-delimited repository file and an equivalent comma-delimited fixture are
  both parsed correctly without an override, while every ambiguous-delimiter fixture fails before
  emitting a manifest.
- **SC-004**: Verification produces exactly one status record for 100% of manifest targets, including
  unavailable and invalid targets.
- **SC-005**: In tests covering fresh acquisition, existing clones, wrong commits, and wrong origins,
  100% of successful materializations end with the resolved commit equal to the requested commit and
  100% of mismatches stop before source extraction.
- **SC-006**: Across all integrity-boundary tests, no LLM or agentic attempt begins or publishes
  results from a revision other than its pinned base commit.
- **SC-007**: Attempts that move the repository revision are detected before post-attempt evidence is
  accepted in 100% of test and pilot cases.
- **SC-008**: After every successful rollback test, tracked content and revision identity match the
  pinned target and attempt-created untracked artifacts are absent.
- **SC-009**: Two revisions of the same repository can be processed in either order without sharing
  or overwriting source analysis, databases, cohorts, or attempt artifacts.
- **SC-010**: Canonical results and run provenance contain non-empty, matching target, requested-
  commit, resolved-commit, and manifest-fingerprint fields for 100% of successful evaluated attempts.
- **SC-011**: Integrity audits detect every seeded revision mismatch, duplicate target, missing
  status, cross-revision collision, and empty successful-commit defect in the checked-in fixture.
- **SC-012**: A researcher can create and verify a pilot manifest and identify every unavailable
  target and reason without inspecting application logs or local repositories.
- **SC-013**: Every measured-run fixture publishes exactly one terminal target-execution row per
  manifest target, including targets that fail before database initialization.

## Assumptions

- Target manifests use YAML and contain a declared schema version.
- The initial import contract is based on `licenseFilteredRepoList.csv`: `name` and `lastCommitSHA`
  are the only required source columns, even when the input contains additional repository metadata.
- Measured experiment mode requires full commit identifiers; abbreviated identifiers are rejected.
- Resolving branch tips or missing commits is outside this feature. Input records without a full
  commit SHA are rejected rather than resolved implicitly.
- Repository access uses credentials already available to the researcher and existing TestMap
  environment; credential management beyond redaction and failure classification is out of scope.
- An existing local repository with unapproved user changes is not modified automatically.
- Expected generated-test edits occur in the working tree without changing the pinned revision.
- Each repository revision receives an isolated workspace, persistence scope, and artifact scope in
  the first version; storing multiple revisions in one shared source-graph database is out of scope.
- Existing URL-only target lists may remain available to discovery-only workflows, but they are not
  valid inputs for measured experiments under this feature.
- Historical artifacts are not migrated or retroactively certified when their exact revision cannot
  be independently verified.
