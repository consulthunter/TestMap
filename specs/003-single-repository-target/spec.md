# Feature Specification: Single Repository Target

**Feature Branch**: `not created - no before-specify branch hook configured`

**Created**: 2026-07-16

**Status**: Draft

**Input**: Allow the target creation command to accept one GitHub repository URL and build a pinned
target manifest for that repository.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create A Target From One Repository URL (Priority: P1)

As a researcher, I can provide a single GitHub repository URL to target creation and receive a
normal pinned target manifest without first constructing a CSV file.

**Why this priority**: Small experiments, debugging, demonstrations, and pilot runs commonly begin
with one repository. Requiring a tabular import file adds ceremony without adding information.

**Independent Test**: Provide an accessible GitHub repository URL, create a manifest, and verify that
it contains exactly one normalized target with the repository's exact resolved commit.

**Acceptance Scenarios**:

1. **Given** an accessible public GitHub repository URL, **When** the researcher creates a target,
   **Then** a valid pinned target manifest containing exactly one repository revision is produced.
2. **Given** equivalent supported URL forms for the same repository, **When** each is used to create
   a target, **Then** they resolve to the same normalized repository identity and canonical URL.
3. **Given** both a single repository URL and a tabular input file, **When** target creation is
   requested, **Then** the command fails before reading or resolving either input because creation
   modes are mutually exclusive.
4. **Given** no URL and no tabular input file, **When** target creation is requested, **Then** the
   command reports the missing input without producing a manifest.

---

### User Story 2 - Pin The Resolved Revision Reproducibly (Priority: P1)

As a researcher, I can trust that URL-based target creation converts the moving repository URL into
an exact, auditable revision before the manifest is published.

**Why this priority**: A repository URL alone identifies a moving project, not a reproducible source
state. The convenience input must not weaken the experiment's immutable provenance.

**Independent Test**: Resolve a repository whose default branch moves between two creation runs.
Verify that each manifest records the exact head observed during its own resolution and that an
already created manifest remains unchanged and continues to identify its original commit.

**Acceptance Scenarios**:

1. **Given** an accessible repository URL, **When** target creation resolves it, **Then** the current
   default branch and its full exact head commit are observed from one compatible repository state.
2. **Given** a successful resolution, **When** the manifest is inspected later, **Then** it records
   the original requested URL, normalized repository identity, canonical URL, resolved default
   branch, exact commit, resolution time, and resolution policy.
3. **Given** the repository default branch advances after manifest creation, **When** the existing
   manifest is used, **Then** its target commit does not change or resolve again.
4. **Given** a provider response whose observed commit is abbreviated, malformed, all zeroes, or
   inconsistent with the resolved branch state, **When** creation runs, **Then** no target manifest
   is published.

---

### User Story 3 - Diagnose Unresolvable Single Targets (Priority: P2)

As a researcher, I receive a clear and auditable failure when a URL cannot be normalized or its
repository revision cannot be resolved, without receiving a plausible but unpinned target.

**Why this priority**: Authentication, authorization, rate limits, unavailable repositories, empty
repositories, and service failures are unavailable observations, not permission to use a different
revision.

**Independent Test**: Exercise malformed URLs, non-GitHub URLs, missing repositories, private
repositories without access, empty repositories, rate limits, and provider failures. Verify stable
failure categories, no manifest publication, and no credential leakage.

**Acceptance Scenarios**:

1. **Given** a malformed, unsupported, or non-GitHub URL, **When** creation is attempted, **Then** it
   fails locally with an invalid-URL reason and performs no remote resolution.
2. **Given** an inaccessible, empty, or unavailable repository, **When** resolution runs, **Then**
   the failure remains distinguishable and no target manifest is published.
3. **Given** missing credentials, rejected credentials, forbidden access, rate limiting, or a
   transient provider failure, **When** resolution runs, **Then** a sanitized stable reason is
   reported without exposing tokens or raw authorization data.
4. **Given** a failed URL-based creation attempt, **When** an existing valid output manifest is
   present, **Then** that completed manifest is not overwritten by partial or failed output.

### Edge Cases

- The URL includes or omits `.git`, has mixed-case owner/repository text, or has a trailing slash.
- The URL uses an unsupported scheme, includes credentials, query parameters, fragments, extra path
  segments, a commit page, an issue page, or a non-GitHub host.
- The repository has been renamed or transferred and the provider reports a different canonical
  identity from the requested URL.
- The repository has no default branch, no commits, is disabled, deleted, private, or inaccessible.
- The default branch changes name or advances while resolution is in progress.
- The provider returns an annotated tag, abbreviated identifier, non-commit object, or inconsistent
  repository metadata.
- Anonymous access works for public metadata but fails while resolving the commit.
- The output path collides with an existing source, report, or manifest artifact.
- The process stops after remote resolution but before all output artifacts are safely published.
- A URL-based creation is repeated after the default branch advances.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Target creation MUST accept one GitHub repository URL as an alternative to a delimited
  input file.
- **FR-002**: URL input and delimited-file input MUST be mutually exclusive. Supplying both or
  neither MUST fail before any input processing or remote request.
- **FR-003**: The URL input MUST identify exactly one repository and MUST NOT accept repository lists
  or multiple URL values in one invocation.
- **FR-004**: The command MUST accept supported GitHub repository URL forms that can be normalized
  unambiguously and MUST reject credentials, query strings, fragments, commit pages, branch pages,
  issue pages, and other non-repository paths.
- **FR-005**: URL parsing and normalization MUST happen before remote access. Invalid or non-GitHub
  URLs MUST cause no remote request.
- **FR-006**: Successful creation MUST normalize the repository to `owner/repository` form and emit
  the same canonical repository URL used by other target manifests.
- **FR-007**: URL-only creation MUST resolve the repository's current default branch and the full
  exact commit at that branch head before constructing the target.
- **FR-008**: The resolved branch and commit MUST come from a compatible provider observation. If
  the repository state changes during resolution and consistency cannot be established, creation
  MUST fail or retry the complete resolution; it MUST NOT combine metadata from incompatible states.
- **FR-009**: A successful resolved commit MUST be a full normalized nonzero commit identifier and
  MUST identify a commit belonging to the resolved repository.
- **FR-010**: The target identifier MUST be calculated from normalized repository identity plus the
  exact resolved commit using the same identity policy as imported targets.
- **FR-011**: The published target manifest MUST be directly consumable by verification,
  `check-projects`, collection, and experiment workflows without conversion.
- **FR-012**: The creation provenance MUST distinguish URL resolution from tabular import and record
  the original requested URL, normalized repository identity, canonical URL, resolved default branch,
  resolved commit, resolution time, provider identity, and named resolution policy version.
- **FR-013**: The manifest MUST retain a fingerprint or content-addressed reference for the URL
  resolution record so later consumers can detect changed or missing provenance.
- **FR-014**: After publication, downstream consumers MUST use the exact manifest commit and MUST NOT
  re-resolve the URL or default branch.
- **FR-015**: Repository rename or transfer behavior MUST be explicit. A provider identity that does
  not match the normalized requested repository MUST fail rather than silently target another
  repository.
- **FR-016**: URL-based creation MUST distinguish at least invalid URL, repository unavailable,
  repository identity mismatch, authentication failed, authorization failed, rate limited, empty
  repository, default branch unavailable, commit unavailable, inconsistent resolution, service
  unavailable, and unexpected resolution failure.
- **FR-017**: Failed URL resolution MUST NOT produce a usable target manifest or modify a previously
  completed output manifest.
- **FR-018**: Failure messages and persisted resolution records MUST be sanitized and MUST NOT contain
  credentials, authorization headers, tokens, or raw sensitive provider response content.
- **FR-019**: Successful publication MUST follow the existing complete-bundle rule: provenance
  artifacts are written and verified before the target manifest is atomically published last.
- **FR-020**: Repeating URL-based creation MUST create a target for the revision observed during each
  invocation. The command MUST NOT claim deterministic output across invocations when the remote
  default branch has changed.
- **FR-021**: The command summary MUST state the normalized repository, resolved branch, exact commit,
  output path, and whether resolution used authenticated or anonymous access, without exposing the
  credential itself.
- **FR-022**: Existing delimited-file target creation semantics MUST remain available and unchanged.
- **FR-023**: Documentation MUST explain that URL-only creation is a convenience for resolving and
  pinning the current default-branch head, not a moving target used directly by experiments.
- **FR-024**: An explicit user-selected commit or branch override is outside this feature's first
  version; existing file-based creation remains the path for supplying a predetermined exact commit.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Reduce setup friction for one-repository pilots without weakening immutable
  repository provenance or allowing a moving default branch into evaluation.
- **Unit of analysis**: One repository target identified by normalized repository identity plus the
  exact commit resolved during target creation. The resolution record is one creation observation,
  not an experiment attempt or metric.
- **Sampling and eligibility**: The researcher explicitly selects one repository. URL resolution
  determines its immutable revision but does not establish project eligibility, test presence, build
  success, candidate availability, or inclusion in a comparison cohort.
- **Comparison contract**: Every later lane receives the same generated target manifest and exact
  commit. No lane may independently resolve the URL or select a newer revision.
- **Measurement contract**: This feature performs identity and revision resolution only. It produces
  no coverage, mutation, test-smell, quality, cost, or outcome measurement.
- **Missingness and failures**: Any inability to establish repository identity, default branch, and
  exact commit is a failed resolution with an explicit reason. Missing values are never converted to
  a default branch name, zero, empty commit, or latest locally available revision.
- **Provenance contract**: The requested URL, canonical repository, canonical URL, provider, resolved
  branch, resolved commit, resolution time, policy version, authentication mode, manifest identity,
  and resolution-record fingerprint remain auditable.
- **Compatibility**: Existing CSV/tab target creation remains unchanged. The emitted target must be
  accepted by all current target consumers. If the existing manifest schema cannot honestly encode
  URL-resolution provenance, it must receive an explicit schema version rather than overloading
  tabular-import fields.
- **Audit criteria**: Target creation or later analysis is blocked when the resolution record is
  missing or changed, repository identity differs, commit is not full and exact, target ID differs
  from repository-plus-commit identity, provenance indicates inconsistent state, or a downstream
  result uses a different commit.

### Key Entities *(include if feature involves data)*

- **Single Repository Request**: The original GitHub repository URL and output intent supplied by the
  researcher.
- **Repository Resolution Observation**: The provider, normalized identity, canonical URL, default
  branch, exact head commit, resolution time, authentication mode, policy version, status, and
  sanitized failure reason.
- **Pinned Repository Target**: The existing repository-plus-commit target produced only after a
  successful resolution.
- **Target Manifest Bundle**: The resolution provenance and one-target manifest published as a
  complete, hash-linked unit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A researcher can create a valid one-target manifest from one accessible GitHub
  repository URL with a single command and no intermediate CSV file.
- **SC-002**: In all revision-movement fixtures, 100% of published targets contain the exact full
  commit observed during their own creation invocation and never change afterward.
- **SC-003**: Across supported equivalent URL forms, 100% resolve to the same normalized repository
  identity and canonical URL for the same repository.
- **SC-004**: Across invalid URL, unavailable repository, authentication, authorization, rate-limit,
  empty-repository, inconsistent-state, and provider-failure fixtures, 0 failed resolutions publish
  a usable target manifest.
- **SC-005**: Every successful URL-created manifest is accepted without conversion by target
  verification, project checking, collection, and experiment target readers.
- **SC-006**: Every published one-target manifest can be traced to a hash-verified resolution record
  containing the requested URL, resolved default branch, exact commit, policy, and resolution time.
- **SC-007**: Credential-pattern scans find zero secrets or raw authorization values in manifests,
  provenance records, summaries, logs, and checked fixtures.
- **SC-008**: Existing file-based target creation tests and behavior continue to pass unchanged.
- **SC-009**: For a normally responsive public repository, users receive either a completed manifest
  or a categorized failure within 30 seconds under the reference validation environment.

## Assumptions

- The first version supports GitHub-hosted repositories only.
- URL-only creation intentionally pins the default-branch head observed at creation time.
- Researchers who already know the desired exact commit continue to use the existing tabular import
  contract in the first version.
- Public repositories may be resolved anonymously within provider limits; private repositories
  require an appropriately scoped credential.
- Repository rename or transfer is treated as an identity mismatch instead of automatic migration.
- The output remains a target manifest, not a configuration file and not an instruction to clone a
  moving branch later.
