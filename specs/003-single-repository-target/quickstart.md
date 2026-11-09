# Quickstart: Validate Single Repository Target Creation

Run these scenarios from the repository root after implementation.

## Prerequisites

- .NET 10 SDK
- Network access for the live smoke scenario
- Optional `GITHUB_TOKEN` for authenticated resolution or private repositories
- A validation output directory that does not contain production evaluation artifacts

## 1. Run Focused Automated Tests

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~SingleRepository|FullyQualifiedName~TargetManifest"
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter "FullyQualifiedName~SingleRepository"
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj --filter "FullyQualifiedName~SingleRepository"
```

Expected: all focused tests pass, including URL grammar, provider status mapping, consistency retry,
schema 3, publication order, output preservation, and downstream consumption.

## 2. Create A Target From One Public Repository

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets create `
  --url https://github.com/powershell/platyps `
  --output .\TestMap\Output\platyps-url-targets.yaml
```

Expected:

- Exit code 0.
- The summary prints normalized repository, resolved default branch, full commit, authentication
  mode, manifest path, and content-addressed resolution-record path.
- The manifest has schema version 3 and exactly one target.
- The target commit is a full 40-character nonzero SHA.
- The resolution record exists and its hash matches the manifest.

Run again with `.git` and a trailing slash in separate output directories. When the remote branch has
not moved, normalized repository, canonical URL, and commit should agree. The original requested URL
and its source hash should reflect each exact accepted input.

## 3. Verify Downstream Compatibility

Use the schema-3 output with each target-consuming workflow:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets verify `
  --file .\TestMap\Output\platyps-url-targets.yaml

dotnet run --project .\TestMap\TestMap.csproj -- check-projects `
  --file .\TestMap\Output\platyps-url-targets.yaml
```

Use a small temporary configuration pointing `TargetFilePath` at the same manifest for collection
and experiment configuration loading tests.

Expected:

- Every consumer verifies the linked resolution record.
- No consumer resolves the URL or default branch again.
- Repository, target ID, and exact commit remain unchanged.

## 4. Prove A Moving Branch Does Not Mutate Existing Targets

Use the deterministic provider fixture:

1. First resolution returns default branch `main` at commit A.
2. Publish manifest A.
3. Move fixture branch head to commit B.
4. Confirm manifest A still reads as commit A.
5. Create manifest B in a new path and confirm it records commit B.

Expected: each invocation honestly pins its own observed head; neither existing manifest changes nor
silently follows the branch.

## 5. Prove Consistency Retry

Configure the deterministic provider fixture so the first metadata read reports default branch
`main`, the confirmation read reports `trunk`, and the second complete pass is stable on `trunk`.

Expected:

- The first pass contributes no fields to the final observation.
- Resolution succeeds with `resolution_passes: 2`, branch `trunk`, and the commit from the second pass.

Repeat with both passes inconsistent.

Expected: status `InconsistentResolution`, exit code 1, a failure resolution record is retained, and
no target manifest is created or replaced.

## 6. Validate Input Arbitration And URL Rejection

Run:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets create
dotnet run --project .\TestMap\TestMap.csproj -- targets create --input targets.csv --url https://github.com/owner/repo
dotnet run --project .\TestMap\TestMap.csproj -- targets create --url https://example.com/owner/repo
dotnet run --project .\TestMap\TestMap.csproj -- targets create --url https://token@github.com/owner/repo
dotnet run --project .\TestMap\TestMap.csproj -- targets create --url https://github.com/owner/repo/issues/1
```

Expected: every command exits 1 before remote access and publishes no resolution record or manifest.

## 7. Validate Provider Failures

Exercise deterministic fixtures for repository unavailable, identity mismatch, authentication,
authorization, rate limit, empty repository, default branch unavailable, commit unavailable,
commit mismatch, service unavailable, and unexpected failure.

Expected:

- Each valid URL attempt writes one content-addressed resolution record with the exact stable status.
- Exit code is 1.
- No target manifest is created.
- If a previous valid manifest exists at the output path, its bytes remain unchanged.
- Rate-limit records may include a retry time but the command does not sleep automatically.

## 8. Validate Secrets And Hashes

Scan resolution records, manifests, summaries, logs, and fixtures for credential patterns. Recompute:

- `source.sha256` from exact requested URL UTF-8 bytes;
- `resolution.sha256` from resolution record bytes;
- `target_id` from normalized repository and resolved commit.

Expected: all hashes agree and no token, bearer value, authorization header, password, or raw
provider body is present.

## 9. Run Existing File Creation Regression

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets create `
  --input .\TestMap\Data\pinned-targets-smoke.csv `
  --output .\TestMap\Output\file-targets-regression.yaml
```

Expected: schema-1 output, rejection CSV, accounting summary, and existing deterministic import
behavior remain unchanged.

## 10. Run Full Regression Suites

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj
```

Record exact test counts, live resolution status, elapsed time, artifact hashes, access mode, and any
dependency advisories in the feature validation report before pilot use.
