# Feature Specification: Readable Log Directories

**Feature Branch**: `004-readable-log-directories`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "For the logs can we use the hour minute and second instead of a random number for the dir?"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identify A Log Run By Time (Priority: P1)

As a TestMap operator, I can identify when a repository run started by reading its log-directory
name instead of interpreting an unrelated random number.

**Why this priority**: Log directories are a primary debugging and research-audit surface. A visible
time makes it faster to correlate a run with console output, experiment records, and observed
failures.

**Independent Test**: Start a legacy repository workflow at a controlled UTC time and verify that
its log path uses the existing `YYYY-MM-DD` date directory followed by a project directory beginning with that
time in `HH-mm-ss` form and the normalized repository label, with no random component.

**Acceptance Scenarios**:

1. **Given** a legacy repository run initialized at 14:05:09 UTC on 16 July 2026, **When** its
   project log is created, **Then** its directory is
   `2026-07-16/14-05-09_<owner>-<repository>` beneath the configured log root.
2. **Given** the same project asks to initialize logging more than once, **When** the later request
   occurs, **Then** it reuses the original directory and log file for that project run.
3. **Given** an operator sorts the directories created on one date by name, **When** no collision
   suffix is needed, **Then** the directories appear in start-time order.

---

### User Story 2 - Preserve Distinct Runs Without Random Names (Priority: P2)

As a TestMap operator, I can run the same repository more than once within the same second without
one run overwriting or sharing another run's logs.

**Why this priority**: Replacing random values with second-level time introduces possible naming
collisions. Collision handling must preserve every log as independent audit evidence.

**Independent Test**: Initialize two distinct runs for the same repository with the same controlled
UTC second and verify that both receive stable, distinct directories and that neither name contains
a random suffix.

**Acceptance Scenarios**:

1. **Given** two runs of the same repository start within the same UTC second, **When** both create
   log directories, **Then** the first uses the base time-and-repository name and the next uses the
   first available deterministic ordinal suffix.
2. **Given** a matching directory already exists from a previous process, **When** a new run creates
   its logs, **Then** it selects a new collision-safe directory and does not append to or overwrite
   the previous run's log.
3. **Given** different repositories start in the same second, **When** their logs are created,
   **Then** their repository labels keep their directories distinct without an ordinal suffix.

---

### User Story 3 - Keep Revision And Persisted Identity Stable (Priority: P3)

As a researcher, I can adopt readable legacy log paths without changing pinned-revision paths,
database identities, result joins, or the meaning of existing experiment data.

**Why this priority**: The requested improvement is a filesystem naming change. Letting it alter
scientific identifiers or immutable target paths would create unnecessary compatibility risk.

**Independent Test**: Compare a pinned-target run and persisted run records before and after the
change, verifying that project logs use the readable hierarchy while revision provenance and
persisted identities remain unchanged.

**Acceptance Scenarios**:

1. **Given** a project is bound to a pinned repository target, **When** logging is initialized,
   **Then** it uses the same readable date/time/repository directory with `run.log` while its
   workspace, database, artifacts, and revision provenance remain commit-scoped.
2. **Given** a run identity is persisted or exported, **When** readable log directories are used,
   **Then** the run identity remains independent from the human-readable directory label.
3. **Given** existing log directories created with random prefixes, **When** TestMap is upgraded,
   **Then** those directories remain readable and are neither renamed nor migrated.

### Edge Cases

- The UTC time contains single-digit hours, minutes, or seconds; each component remains zero-padded
  to two digits.
- Repository owner or name casing differs between inputs; the displayed repository label follows
  the existing normalized project naming policy.
- Multiple processes create the same base directory concurrently; each completed run receives a
  distinct directory without overwriting another process's log.
- Logging is initialized shortly after midnight; the date parent and time leaf come from one
  captured UTC instant and cannot disagree across dates.
- Logging initialization fails after reserving a directory; the failure remains visible and a later
  distinct run does not silently reuse partial evidence.
- The configured log root is missing or empty; existing configuration and error behavior remain
  unchanged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Legacy project logs MUST remain grouped beneath the existing UTC calendar-date
  directory formatted as `YYYY-MM-DD`.
- **FR-002**: Legacy per-project log directory names MUST replace the random numeric prefix with a
  24-hour UTC time formatted as `HH-mm-ss`.
- **FR-003**: The complete relative directory path MUST follow
  `YYYY-MM-DD/HH-mm-ss_<owner>-<repository>` when no collision exists.
- **FR-004**: The date and time used in the directory path MUST be captured once for the project run and MUST
  remain stable across repeated logging initialization calls.
- **FR-005**: The date parent directory and the time component MUST derive from the same UTC instant.
- **FR-006**: The system MUST keep distinct runs of the same repository isolated when their base
  names collide.
- **FR-007**: Collision handling MUST use a deterministic, monotonically increasing ordinal suffix
  such as `-02`, `-03`, and MUST NOT introduce a random value or overwrite an existing directory.
- **FR-008**: Collision detection and reservation MUST remain safe when independent processes try to
  create the same directory concurrently.
- **FR-009**: Repeated log initialization for one project run MUST reuse that run's already selected
  directory and log file rather than allocate another ordinal.
- **FR-010**: The log filename MUST remain uniquely associated with the selected project run and
  MUST be recorded through the existing log-path evidence surfaces.
- **FR-011**: Existing project identifiers, persisted run identifiers, database keys, result keys,
  and output-directory naming MUST NOT change as a consequence of this feature.
- **FR-012**: Pinned-target project logs MUST use the readable date/time/repository hierarchy and
  `run.log`; their workspace, database, artifact, and exact-revision provenance MUST remain unchanged.
- **FR-013**: Existing random-prefixed log directories MUST remain untouched; no migration or rename
  is required.
- **FR-014**: Documentation that describes log locations MUST show the readable date-and-time naming
  convention and its UTC basis.
- **FR-015**: Automated validation MUST cover formatting, stable reuse, same-second collisions,
  concurrent collision safety, midnight consistency, pinned-path non-regression, and preservation
  of persisted run identity.

### Research Validity and Data Contract *(mandatory for experiment-affecting features)*

- **Research purpose**: Improve the auditability of retained execution evidence by making the run
  time visible in legacy log paths, without changing experimental meaning.
- **Unit of analysis**: One project run and its associated log directory. This does not change the
  attempt, candidate, generated-test, repository, or experiment grains.
- **Sampling and eligibility**: No sampling frame, eligibility rule, exclusion, or randomization is
  changed. Every run that currently receives a log continues to receive one.
- **Comparison contract**: Lane assignment, budgets, retries, workspace isolation, and shared or
  lane-specific evidence remain unchanged. The naming rule applies wherever the affected legacy
  project log path is used.
- **Measurement contract**: No metric, baseline/post pairing, threshold, attribution rule, duration,
  or outcome classification changes. The displayed time is path metadata, not a measured duration.
- **Missingness and failures**: Existing logging failures remain failures. A collision or partial
  directory MUST NOT cause logs to be overwritten, merged, or silently reported as available.
- **Provenance contract**: Existing repository, commit, configuration, experiment, attempt, and run
  identifiers remain authoritative. The readable path is additional human-facing provenance and is
  not a replacement identifier.
- **Compatibility**: No database, CSV, cohort, target manifest, result schema, notebook, or existing
  log directory changes. New pinned log locations change prospectively, while pinned workspaces,
  databases, artifacts, provenance, and prior research claims remain compatible.
- **Audit criteria**: Validation fails if a new legacy directory contains a random prefix, if two
  runs share a directory, if date and time derive from different UTC instants, if a pinned path
  changes, or if any persisted identity changes with the path label.

### Key Entities *(include if feature involves data)*

- **Log Run Label**: A human-readable, per-project-run directory label containing the captured UTC
  time, normalized repository label, and an optional deterministic collision ordinal.
- **Project Run Log**: The retained log file associated with exactly one project run and one selected
  directory; its path may be persisted as evidence but does not define the run's identity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly created legacy project log paths match the documented
  `YYYY-MM-DD/HH-mm-ss_<owner>-<repository>` convention and contain no random component.
- **SC-002**: In a test of at least 100 same-second runs for one repository, every run receives a
  distinct directory and no existing log content is overwritten or merged.
- **SC-003**: Repeated initialization of the same project run reuses one directory and one log file
  in 100% of test cases.
- **SC-004**: Existing pinned-target path, persisted identity, CSV, database, and experiment
  regression tests show zero naming-related behavioral changes.
- **SC-005**: An operator can identify the repository and UTC start second directly from every new
  legacy log directory name without consulting another artifact.

## Assumptions

- UTC is used because research artifacts may be compared across machines and time zones.
- The date directory preserves the existing zero-padded four-digit year, month, and day format in
  `YYYY-MM-DD` order.
- Hyphens separate hour, minute, and second because colons are not portable in directory names.
- The requested hierarchy applies to both legacy and pinned project logs. Pinned workspace,
  database, artifact, and provenance paths remain revision-scoped.
- A deterministic ordinal is acceptable only for collision resolution; it is not a random run ID.
- Existing project and persisted run identifiers may continue to use their current identity policy
  because the request concerns the log directory name, not identity semantics.
