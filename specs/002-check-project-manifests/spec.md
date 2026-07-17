# Feature Specification: Check Project Manifests

**Feature Branch**: `not created - no before-specify branch hook configured`

**Created**: 2026-07-16

**Status**: Draft

**Input**: Update `check-projects` to consume a pinned repository target YAML manifest and publish
target-compatible YAML manifests for repositories with and without detected tests.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Check Pinned Repository Revisions (Priority: P1)

As a researcher, I can check every repository revision in a target manifest for evidence of tests so
project eligibility is determined from the same immutable source state that will be evaluated.

**Why this priority**: Checking a repository's current default branch can disagree with the pinned
revision and change the sampling frame after it has been declared.

**Independent Test**: Supply a manifest containing two revisions of one repository where only one
revision contains test indicators. Verify that each revision is classified from its own declared
commit and receives the expected result.

**Acceptance Scenarios**:

1. **Given** a valid target whose pinned revision contains test indicators, **When** the researcher
   runs `check-projects`, **Then** the target is classified as having detected tests at that exact
   revision.
2. **Given** a valid target whose default branch contains tests but whose pinned revision does not,
   **When** the check runs, **Then** the result reflects the pinned revision rather than the default
   branch.
3. **Given** two targets for different commits of one repository, **When** the check runs, **Then**
   they remain separate targets and may receive different classifications.

---

### User Story 2 - Produce Reusable YAML Target Lists (Priority: P1)

As a researcher, I receive separate YAML manifests for targets with detected tests and targets with
no detected tests, and I can use either output directly as input to later TestMap workflows.

**Why this priority**: Requiring researchers to reconstruct pinned target identity from bare URLs
would discard commit and source provenance and invite accidental revision drift.

**Independent Test**: Check a mixed input manifest, load both output manifests using the normal
target-manifest reader, and verify that every determined target appears exactly once with unchanged
identity, URL, commit, and source provenance.

**Acceptance Scenarios**:

1. **Given** a manifest with targets both with and without detected tests, **When** checking
   completes, **Then** two valid target-compatible YAML manifests are published for those categories.
2. **Given** an input target assigned to a category, **When** its output row is inspected, **Then**
   its target identifier, repository identity, URL, exact commit, and source-row provenance are
   unchanged.
3. **Given** the same input manifest and the same observed repository contents, **When** checking is
   repeated, **Then** the categorized target content and ordering are identical.
4. **Given** a category with no targets, **When** checking completes, **Then** a valid empty manifest
   is still published so downstream automation does not depend on file absence.

---

### User Story 3 - Audit Every Classification (Priority: P2)

As a researcher, I can audit how every input target was handled, including targets that could not be
checked, without those failures being reported as repositories without tests.

**Why this priority**: Authentication, rate-limit, repository, and commit failures are missing
observations, not evidence that a repository lacks tests. Misclassifying them would bias eligibility.

**Independent Test**: Check a manifest containing an available revision, an unavailable commit, an
inaccessible repository, and an authentication failure. Verify one report record exists per target,
only determined targets enter a categorized manifest, and the command reports incomplete checking.

**Acceptance Scenarios**:

1. **Given** an unavailable repository or commit, **When** checking runs, **Then** the target receives
   an explicit unavailable status and appears in neither categorized target manifest.
2. **Given** an authentication, authorization, rate-limit, or transient service failure, **When**
   checking runs, **Then** the failure remains distinguishable and is not classified as no tests
   detected.
3. **Given** any input manifest, **When** checking finishes, **Then** a report contains exactly one
   terminal record for every unique input target and identifies the revision that was checked.
4. **Given** one or more indeterminate targets, **When** outputs are published, **Then** the command
   visibly reports that the classification set is incomplete.

### Edge Cases

- The input file is missing, unreadable, empty, malformed, or uses an unsupported manifest schema.
- A target has a malformed identity, URL, commit, or target identifier despite appearing in YAML.
- The input manifest contains duplicate target identifiers or duplicate repository revisions.
- The repository exists but the pinned commit is not advertised or cannot be retrieved.
- The repository was renamed, transferred, archived, deleted, or made private after manifest creation.
- Credentials are missing, invalid, expired, insufficiently scoped, or exhausted by a rate limit.
- A repository tree is empty, exceptionally large, truncated by the provider, or contains paths whose
  names incidentally include the word `test`.
- A project uses unconventional test project or directory names that the classification policy does
  not recognize.
- Two check processes attempt to publish the same output bundle concurrently.
- Processing stops after some remote checks complete but before all output artifacts are published.
- An output path would overwrite the input manifest or another member of the output bundle.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `check-projects` MUST accept the current versioned pinned-target YAML manifest as its
  repository input.
- **FR-002**: The command MUST reject legacy URL-only or plain-text target lists rather than silently
  converting or resolving them.
- **FR-003**: The command MUST validate the complete input manifest before checking any target and
  MUST fail visibly when its schema or required provenance is invalid.
- **FR-004**: Each project check MUST use the target's exact declared commit. It MUST NOT substitute
  the repository's default branch, latest revision, or a locally cached revision.
- **FR-005**: The test-presence classification policy MUST be named and versioned in the check report
  and output provenance.
- **FR-006**: A target MUST be classified as having detected tests only when the pinned revision
  contains evidence recognized by the declared policy.
- **FR-007**: The absence category MUST be labelled as no tests detected, rather than asserting that
  the repository has no tests.
- **FR-008**: Repository unavailability, commit unavailability, authentication failure,
  authorization failure, rate limiting, transient service failure, invalid target data, and other
  check failures MUST remain distinct from no tests detected.
- **FR-009**: The command MUST publish one YAML target manifest containing all targets classified as
  having detected tests and one containing all targets classified as no tests detected.
- **FR-010**: Each categorized output MUST satisfy the target-manifest contract used by downstream
  collection and experiment workflows without requiring conversion.
- **FR-011**: Categorized output entries MUST preserve the input target identifier, normalized
  repository identity, repository URL, exact commit, and source-record provenance without
  recomputation or loss.
- **FR-012**: Categorized output manifests MUST record their relationship to the input manifest,
  including the input manifest identity or fingerprint, classification category, policy version,
  and creation time.
- **FR-013**: Targets with a determinate result MUST appear in exactly one categorized manifest.
  Indeterminate targets MUST appear in neither.
- **FR-014**: The command MUST publish a check report containing exactly one terminal record for each
  unique input target, including target identity, requested commit, observed commit when available,
  classification status, policy version, check time, and a stable reason category.
- **FR-015**: The check report MUST retain enough non-secret evidence to explain a positive
  classification, such as the recognized indicator category and matching path, while avoiding
  credentials and sensitive request data.
- **FR-016**: The check report MUST include aggregate counts for input targets, detected-test
  targets, no-tests-detected targets, and every indeterminate status.
- **FR-017**: Output target ordering and report ordering MUST be deterministic for equivalent input
  and observations.
- **FR-018**: The command MUST publish categorized manifests and their report as one consistent
  output bundle so consumers cannot mistake partial output for a completed check.
- **FR-019**: Existing output artifacts MUST NOT be appended to. A repeated completed run MUST
  replace or create a complete deterministic bundle without duplicating targets.
- **FR-020**: Output paths MUST be distinct from the input path and from one another, and publication
  MUST fail before overwriting the input manifest.
- **FR-021**: A completed run with any indeterminate target MUST return a visible incomplete result
  while retaining the complete report and all safely classified targets.
- **FR-022**: The command MUST leave the input manifest unchanged.
- **FR-023**: Legacy `repos_with_tests.txt` and `repos_without_tests.txt` outputs are outside the new
  contract and MUST NOT be required or treated as canonical artifacts.
- **FR-024**: Documentation MUST show the YAML input and all YAML output artifacts and explain that
  classification applies to pinned revisions and represents detected evidence, not proof of absence.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Preserve the declared sampling frame while screening pinned repository
  revisions for likely test presence, preventing default-branch drift and API failures from changing
  cohort eligibility.
- **Unit of analysis**: One immutable repository target, identified by normalized repository identity
  plus exact commit. Report summaries aggregate target-level classifications without collapsing
  revisions of the same repository.
- **Sampling and eligibility**: The input manifest is the complete sampling frame. Targets with
  detected test evidence are eligible for later collection; no-tests-detected targets are explicit
  screening exclusions; indeterminate targets remain unresolved and MUST NOT be treated as excluded
  for lacking tests.
- **Comparison contract**: This feature occurs before lane assignment and therefore MUST produce one
  shared, revision-pinned eligibility set for every later evaluation lane.
- **Measurement contract**: Test presence is a versioned heuristic classification of repository-tree
  evidence at an exact commit. It is neither a test execution result nor a metric and MUST not be
  interpreted as proof that tests compile, pass, map to candidates, or are absent.
- **Missingness and failures**: Remote access and validation failures are explicit indeterminate
  statuses. They are never converted to false, zero, or no tests detected.
- **Provenance contract**: The input manifest fingerprint, target identity, repository identity,
  requested and observed commits, policy version, classification time, and output category remain
  auditable in the output bundle.
- **Compatibility**: The feature adopts the current pinned-target YAML contract and intentionally
  does not preserve legacy text-list input or output. Categorized manifests remain directly usable by
  downstream target consumers.
- **Audit criteria**: Analysis or cohort creation MUST be blocked when target identities collide,
  commits differ from the input, a target appears in multiple categories, an indeterminate target is
  categorized, a report target is missing, or output provenance does not match the input manifest.

### Key Entities *(include if feature involves data)*

- **Project Check Input**: The immutable pinned-target manifest that defines every repository
  revision to classify.
- **Project Check Observation**: One terminal target-level record describing the checked revision,
  classification, policy, evidence, availability, and failure reason where applicable.
- **Categorized Target Manifest**: A target-compatible YAML manifest containing an unchanged subset
  of input targets assigned to one determinate classification.
- **Project Check Bundle**: The two categorized manifests and complete report that share one input
  fingerprint and publication identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In validation fixtures, 100% of classifications are based on the exact commit declared
  by the target, including repositories whose default branch has different test contents.
- **SC-002**: For every completed run, 100% of unique input targets have exactly one terminal report
  record, and 100% of determinate targets appear in exactly one categorized manifest.
- **SC-003**: No authentication, availability, rate-limit, or service failure is classified as no
  tests detected across the automated failure scenarios.
- **SC-004**: Both categorized outputs can be accepted directly by every downstream workflow that
  accepts the current target-manifest contract.
- **SC-005**: Repeated checks with equivalent input and observations produce identical categorized
  target content and ordering, excluding explicitly non-deterministic metadata such as check time.
- **SC-006**: After all remote observations are available, a bundle containing up to 10,000 targets
  is validated and published within 30 seconds under the reference test environment.
- **SC-007**: An interrupted or failed publication leaves zero completed-looking partial bundles.
- **SC-008**: Reviewers can trace every categorized target to its original manifest entry and the
  evidence or reason for its classification using only the published bundle.

## Assumptions

- The current pinned-target manifest contract from feature `001-pinned-repository-targets` is the
  sole supported input contract.
- The existing check remains a conservative repository-tree heuristic; build, test discovery, and
  execution are performed by later workflows.
- Positive evidence may use recognized test-related paths or project artifacts, but the exact
  indicator set is a versioned policy decision to be finalized during planning.
- Remote repository access requires credentials appropriate to the repositories being checked.
- Categorized manifests preserve target entries rather than creating new target identities.
- Output filenames may be configurable, with deterministic names beside the input manifest as the
  default.
- A complete report is retained even when the command returns an incomplete or failed status.
