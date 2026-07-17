# Validation Report: Readable Log Directories

**Date**: 2026-07-16  
**Result**: Passed

**Amendment**: After inspecting a real pinned `collect-tests` run, the readable hierarchy was
extended to pinned project logs. Earlier validation totals remain historical; the amended contract
is revalidated by the current test run recorded below.

## Implemented Contract

New legacy project logs use:

```text
<log-root>/YYYY-MM-DD/HH-mm-ss_<owner>-<repository>[-NN]/<ProjectId>.log
```

The date and time derive from one captured UTC `RunStartedAtUtc`. The unsuffixed directory is used
first; collisions receive `-02`, `-03`, and later deterministic ordinals. Every selected directory
retains `.testmap-log-reservation`. `ProjectId` remains unchanged as the log filename and persisted
run identity, but no longer determines the directory leaf.

Pinned target logs now use the same hierarchy:

```text
<log-root>/YYYY-MM-DD/HH-mm-ss_<owner>-<repository>/run.log
```

## Automated Regression Gate

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `TestMap.UnitTests` | 1,027 | 0 | 0 |
| `TestMap.IntegrationTests` | 37 | 0 | 0 |
| `TestMap.EndToEndTests` | 16 | 0 | 0 |

Repository-data integration tests now reference the current canonical data and example bundle. The
collection end-to-end test uses a copied, test-owned offline Git fixture rather than a mutable
workspace under `Temp`.

Focused coverage validates exact UTC formatting, zero-padding, custom `RunDateFormat` isolation,
one-instant midnight behavior, idempotent initialization, existing-directory preservation, ordinal
formatting through `-100`, partial reservation retention, persistence identity, pinned paths, and
real collection log creation.

A real pinned `collect-tests` run against a reused workspace with preserved runtime artifacts
completed in 130.4 seconds and wrote:

```text
Logs/2026-07-16/19-19-06_consulthunter-testmap-example/run.log
Logs/2026-07-16/19-19-06_consulthunter-testmap-example/run-docker-baseline_<run-id>.log
```

The target execution report recorded the requested and resolved commit as
`867ab17d3141bc0e6d696486bd8edb67546c00d1` and terminal status `Completed`.

## Concurrency And Performance

The isolated 100-way same-repository, same-second allocation test passed in 254 ms. It produced:

- 100 distinct directory paths;
- ordinals 1 through 100 exactly once;
- one retained reservation marker per selected directory;
- no random or GUID directory component;
- no shared or overwritten log path.

A separate contention test uses independent allocator instances and verifies the number of exclusive
reservation markers equals the number of successful allocations. Ownership depends on exclusive
filesystem creation, so it is not limited to an in-process lock.

## Failure And Compatibility Audit

- Existing directories, including historical random-prefixed directories, are skipped without
  modification.
- A reserved directory without a log remains visible as partial evidence; the next run advances to
  the next ordinal.
- Non-collision I/O errors propagate rather than being converted into an endless collision loop.
- Repeated initialization of one project model reuses its original logger and path.
- `RunDateFormat` remains available for display and persistence; it cannot alter the fixed
  `yyyy-MM-dd` log parent.
- `ProjectId`, database IDs, test-run IDs, output paths, result contracts, CSVs, manifests, cohorts,
  and notebooks are unchanged.
- No migration, database entity, schema snapshot, or `schema.sql` change was introduced by this
  feature. Existing unrelated persistence changes in the dirty worktree were left untouched.
- Pinned logs use the allocator and update `MaterializedRevision.Paths.LogPath` to the selected path;
  pinned workspace, database, artifact, and revision provenance remain commit-scoped.
- Repository-local Git exclusions identify untracked TestMap-owned `coverage`, `mutation`, and
  `TestResults` artifacts, so preserved runtime evidence does not make a reused pinned workspace
  appear source-dirty. Tracked changes under identically named paths remain visible and still cause
  strict materialization failure.
- A target-level materialization or pipeline failure is still recorded per target, but now also
  causes the command to return a nonzero result after all configured targets have been processed.
- `project-validation.csv` remains a run-wide target-list artifact at the configured output root;
  pinned repository paths affect `analysis.db` and artifacts only. The aggregate is reset at the
  start of each `collect-tests` invocation, and concurrent row writes are serialized.
- A successful `collect-tests` project now invokes repository cleanup from the run coordinator.
  With `KeepProjectFiles=false`, deletion is constrained to descendants of `TempDirPath`, removes
  the pinned revision workspace, and prunes empty repository/owner parents without affecting
  siblings. Failures after successful workspace preparation are cleaned; pre-materialization
  failures cannot delete an unverified or busy workspace. Cleanup failures are surfaced.
- Path validation rejects non-UTC timestamps, empty roots, unsafe identity segments, and root escape.

## Static Audit

- The old `Path.Combine(logRoot, runDate, ProjectId)` construction is absent.
- Remaining random-number references describe the intentionally unchanged `ProjectId` policy or
  historical compatibility requirements, not directory allocation.
- `git diff --check` passed; its only output was a pre-existing line-ending warning for
  `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`.
- Documentation now applies the readable log hierarchy to both legacy and pinned targets while
  preserving commit-scoped non-log paths and provenance.

## Known Advisory

All .NET builds continue to report the pre-existing `NU1903` advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (`GHSA-2m69-gcr7-jv3q`). It is unrelated to this filesystem-only
feature.
