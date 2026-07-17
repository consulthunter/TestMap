# Research: Readable Log Directories

## Scope Boundary

**Decision**: Use the readable hierarchy for both legacy and pinned project logs. Pinned runs use
`run.log`; their workspace, database, artifact, and exact-revision provenance remain commit-scoped.

**Rationale**: Operators need one predictable log hierarchy regardless of target-source format.
Revision identity remains independently available in stronger provenance surfaces.

**Alternatives considered**:

- Encode the commit in pinned log directories: rejected because it creates a second incompatible
  hierarchy while the commit is already recorded in the manifest, database, and execution report.
- Replace `ProjectId` globally: rejected because it is used as a run/persistence identity beyond
  directory naming.

## Timestamp Ownership

**Decision**: Capture one `DateTimeOffset` in UTC at run initialization, pass it to project models,
and derive both `yyyy-MM-dd` and `HH-mm-ss` from that same value.

**Rationale**: Independent date and time reads can disagree across midnight. A single value is easy
to test and becomes stable across repeated calls. `DateTimeOffset` makes the UTC requirement
explicit.

**Alternatives considered**:

- Read the current time in `EnsureProjectLogDir`: rejected because delayed project startup and
  repeated calls would create unstable labels.
- Combine the existing `RunDate` string with a later time read: rejected because it can cross a date
  boundary and because the string may use a custom display format.
- Add a broad clock abstraction: rejected for the first version because an explicit optional run
  timestamp gives deterministic tests with less abstraction.

## Directory Grammar

**Decision**: Use
`<log-root>/yyyy-MM-dd/HH-mm-ss_<owner>-<repository>[-NN]/<ProjectId>.log`, where `-NN` begins at
`-02` only for collisions.

**Rationale**: Year-first dates and 24-hour time sort chronologically, hyphens are portable, and the
repository remains visible. Keeping the existing log filename minimizes downstream change.

**Alternatives considered**:

- `MM-dd-yyyy`: rejected because the current default is `yyyy-MM-dd` and year-first sorting is
  chronological.
- Colons in time: rejected because they are not portable on Windows.
- Milliseconds: rejected because the user requested hour, minute, and second; timing precision does
  not eliminate every concurrency collision.

## Collision And Concurrency Policy

**Decision**: Probe deterministic ordinals and reserve a candidate with a fixed
`.testmap-log-reservation` file created exclusively inside the candidate directory. Existing
directories are never entered or reused.

**Rationale**: `Directory.CreateDirectory` succeeds even when another process already created the
same directory, so existence checks alone cannot establish ownership. Exclusive file creation gives
one winner in the race. Deterministic ordinals satisfy readability without random final paths.

**Alternatives considered**:

- In-process lock only: rejected because separate TestMap processes can race.
- Random or GUID suffix: rejected by the feature requirement.
- Timestamp milliseconds or process ID: rejected because they expose incidental identity and still
  require a collision policy.
- Delete the reservation marker after setup: rejected because a crash between deletion and logger
  startup could make a partial directory look reusable.

## Failure And Retention Policy

**Decision**: Leave reservations and partial directories in place after a failure. Later runs choose
the next ordinal; no automatic migration, deletion, or reuse occurs.

**Rationale**: Logs and logging failures are audit evidence. Retaining partial state prevents a later
run from obscuring the fact that an earlier run reserved that name but failed to complete.

**Alternatives considered**:

- Delete empty or partial directories: rejected because crash-time cleanup cannot be guaranteed and
  may destroy useful evidence.
- Reuse directories without a log file: rejected because it can merge distinct runs.

## Compatibility

**Decision**: Do not change database schemas, result contracts, `ProjectId`, run IDs, output paths,
old directories, or pinned target paths.

**Rationale**: The directory is human-facing provenance, not a scientific join key. Keeping identity
contracts stable avoids changing prior observations or analyses.

**Alternatives considered**:

- Persist a new timestamp column: rejected because current log paths already carry the filesystem
  evidence and no analysis requires a new field.
- Rename old directories: rejected because it would mutate historical artifacts.
