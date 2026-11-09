# Filesystem Contract: Readable Project Logs

## Applicability

This contract applies to legacy and pinned project runs. Pinned workspace, database, artifact, and
revision provenance remain repository-and-commit scoped, but pinned logs use this readable layout.

## Path Grammar

```text
relative-log-path = date "/" project-directory "/" log-file
date              = 4DIGIT "-" 2DIGIT "-" 2DIGIT
project-directory = time "_" owner "-" repository [ ordinal ]
time              = 2DIGIT "-" 2DIGIT "-" 2DIGIT
ordinal           = "-" 2*DIGIT
log-file          = project-id ".log"
```

Semantic formatting:

- `date` is UTC `yyyy-MM-dd`.
- `time` is UTC 24-hour `HH-mm-ss`.
- Date and time come from one captured instant.
- The base candidate has no ordinal.
- Collision ordinals start at `-02` and increase monotonically.
- `owner` and `repository` use the existing normalized project labels.

Example:

```text
Logs/2026-07-16/14-05-09_powershell-platyps/482731_powershell-platyps.log
Logs/2026-07-16/14-05-09_powershell-platyps-02/901245_powershell-platyps.log
```

The numbers in the example filenames are existing project identifiers. They are not used in the
directory name.

## Reservation Contract

Each selected directory contains a fixed `.testmap-log-reservation` marker. A run owns a candidate
only when it created that marker with exclusive create semantics after creating the directory.

Allocation rules:

1. If the candidate directory already exists, do not modify it; try the next ordinal.
2. If the candidate did not exist, create it and exclusively create the fixed marker.
3. If exclusive marker creation fails because another contender won, try the next ordinal.
4. Do not delete a successful marker or a partial directory automatically.
5. Never open an existing run log as the log for a new run.

## Idempotence Contract

Once one `ProjectModel` has selected a path, repeated initialization returns the same
`LogsFilePath`, directory, and logger association. It does not probe or reserve another candidate.

## Compatibility Contract

- `ProjectId` generation and persistence are unchanged.
- Existing `<ProjectId>.log` filenames are unchanged.
- Existing directories are not renamed or migrated.
- Pinned target logs use `run.log` in the readable directory; revision-scoped non-log paths and
  provenance are unchanged.
- Database, CSV, target manifest, experiment result, and notebook contracts are unchanged.
