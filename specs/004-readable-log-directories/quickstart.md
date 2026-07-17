# Quickstart: Validate Readable Log Directories

Run these scenarios from the repository root after implementation.

## Prerequisites

- .NET 10 SDK
- A temporary writable log root
- No network access is required for focused validation

## 1. Run Focused Tests

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~ProjectLogDirectory|FullyQualifiedName~ProjectModelLog|FullyQualifiedName~ConfigurationService"

dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter `
  "FullyQualifiedName~ReadableLogDirectory"
```

Expected: exact UTC formatting, stable reuse, deterministic collision ordinals, concurrent
reservation, midnight consistency, and configuration propagation all pass.

## 2. Inspect A Controlled Example

Use a test fixture with run start `2026-07-16T14:05:09Z`, owner `powershell`, and repository
`platyps`.

Expected directory:

```text
<log-root>/2026-07-16/14-05-09_powershell-platyps/
```

Expected contents include:

```text
.testmap-log-reservation
<existing-project-id>.log
```

The directory contains no random prefix. The existing project identifier may remain in the log
filename and persisted run records.

## 3. Validate Same-Second Collisions

Initialize at least 100 distinct project-run instances for the same repository, UTC instant, and log
root concurrently.

Expected:

- 100 distinct directories;
- the first unsuffixed, followed by `-02` through `-100`;
- one reservation marker per selected directory;
- no shared or overwritten log file;
- completion within 2 seconds on a local filesystem.

## 4. Validate Repeated Initialization

Call log initialization twice on one project model.

Expected: the same `LogsFilePath` is returned and no `-02` directory is created for that model.

## 5. Validate Midnight Consistency

Use a controlled run start immediately before and immediately after UTC midnight in separate tests.

Expected: each date parent and time leaf come from its one controlled instant; no path combines the
previous date with the next day's time.

## 6. Validate Pinned Target Logging And Revision Non-Regression

Run the existing pinned target path and materialization tests.

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~TargetPathResolver"

dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter `
  "FullyQualifiedName~RepositoryMaterialization|FullyQualifiedName~Pinned"
```

Expected: pinned logs use `<log-root>/YYYY-MM-DD/HH-mm-ss_<owner>-<repository>/run.log`, while pinned
workspace, database, artifact paths, and exact-revision provenance remain unchanged.

## 7. Run Full Regression Suites

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj
```

Inspect a collection run's `LogsFilePath` and verify that database identities, output paths, result
files, and existing random-prefixed historical directories remain unchanged.
