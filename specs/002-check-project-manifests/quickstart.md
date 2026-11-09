# Quickstart: Validate Check Project Manifests

This guide validates the feature after implementation. Run commands from the repository root.

## Prerequisites

- .NET 10 SDK
- A valid pinned target manifest created by `targets create`
- `GITHUB_TOKEN` with repository Contents read access for private targets; public targets may be
  checked anonymously within provider limits
- No production evaluation artifacts in the output directory used for validation

The checked-in smoke manifest is `TestMap/Data/pinned-targets-smoke.yaml`. Its referenced rejection
report must remain beside it and match the recorded hash.

## 1. Run Focused Automated Tests

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~ProjectCheck|FullyQualifiedName~TargetManifest"
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter "FullyQualifiedName~ProjectCheck"
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj --filter "FullyQualifiedName~PinnedProjectCheck"
```

Expected: all focused tests pass. The end-to-end fixture demonstrates that moving a repository's
default branch does not change the classification of an older pinned target.

## 2. Check A Pinned Manifest

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- check-projects `
  --file .\TestMap\Data\pinned-targets-smoke.yaml `
  --output .\TestMap\Data\pinned-targets-smoke-project-check.yaml `
  --max-concurrency 4
```

Expected:

- Exit code `0` when all targets are classified, or `2` when a valid bundle contains indeterminate
  targets.
- The console prints counts for checked, tests detected, no tests detected, and indeterminate.
- The stable pointer `pinned-targets-smoke-project-check.yaml` is written last.
- It references three content-addressed YAML members in the same directory.
- Neither legacy text output is created or modified.

## 3. Validate Bundle Integrity

Inspect the stable pointer and verify:

1. `complete` is `true`.
2. Its input SHA-256 equals the source manifest hash.
3. Every referenced member exists and matches its SHA-256.
4. Report observations equal the input target count.
5. `input_targets = tests_detected + no_tests_detected + indeterminate`.
6. Every determinate target appears in exactly one derived manifest.
7. Every indeterminate target appears in neither derived manifest.

The exact field contracts are in the report, derived-manifest, and bundle examples under
`specs/002-check-project-manifests/contracts`.

## 4. Reuse Each Categorized Manifest

Read the `tests_detected.file` and `no_tests_detected.file` paths from the bundle. Use each as the
configured target path for the normal target-source reader or a collection dry run.

Expected:

- Both schema 2 manifests pass strict target validation, including an empty category.
- The referenced check report is required and hash-verified.
- Target IDs, repositories, URLs, commits, and source rows match the input manifest exactly.
- Downstream workspaces and outputs continue to use repository-plus-commit identity.

## 5. Prove Exact-Revision Classification

Use the deterministic end-to-end fixture with two commits: commit A has no recognized test paths,
commit B adds a recognized test project, the default branch points at B, and the manifest includes
both A and B.

Expected:

- A is `NoTestsDetected`.
- B is `TestsDetected` with evidence from B.
- Both observations record their own requested and observed commit.
- No default-branch value appears in the report contract.

## 6. Prove Honest Missingness

Run fixture cases for unavailable repository, unavailable commit, rejected credentials, forbidden
access, rate limiting, truncated tree, and transient service failure.

Expected:

- Each target has one explicit indeterminate observation.
- None appears in the no-tests-detected manifest.
- The bundle has `classification_complete: false`.
- The command exits `2`, while all safely determined targets remain available.
- Reports contain sanitized reasons and no token or authorization value.

## 7. Prove Publication Safety

Use the bundle publisher's injected-failure tests to fail after each member write and before pointer
publication.

Expected:

- A previous valid bundle pointer remains unchanged.
- With no previous pointer, no completed-looking pointer exists.
- A successful retry publishes a pointer whose three member hashes validate.
- Repeating the run never appends or duplicates targets.

## 8. Run Scale Validation

Run the deterministic 10,000-target publication benchmark with remote observations preconstructed.

Expected:

- Partition validation, YAML serialization, hashing, member publication, and pointer publication
  complete within 30 seconds in the recorded reference environment.
- The benchmark reports elapsed time, target count, output sizes, runtime version, OS, and storage
  location; it does not silently enforce results from an unrecorded environment.

## 9. Run The Full Regression Matrix

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj
```

Before a pilot, retain the smoke bundle and test output with the evaluation setup evidence. Resolve
or explicitly document dependency security advisories visible during restore.
