# Readable Log Directory Fixtures

Tests use fixed UTC timestamps and temporary roots. A directory containing
`.testmap-log-reservation` is owned by one synthetic run. Pre-existing and partial directories are
retained as audit evidence and must never be deleted or reused by a later fixture.
