# Quickstart Validation: Pinned Repository Targets

This guide validates the feature end to end after implementation. It uses a small input first, then
the supplied replication dataset. See [CLI contract](./contracts/cli-contract.md),
[data model](./data-model.md), and [result provenance](./contracts/result-provenance-v3.md).

## Prerequisites

- .NET 10 SDK
- Git access to the selected repositories
- `GITHUB_TOKEN` when verification would otherwise be rate-limited or targets are private
- A clean TestMap checkout

## 1. Run Automated Verification

```powershell
dotnet test .\TestMap.slnx
```

Expected: unit, integration, and end-to-end projects pass, including local Git fixtures that do not
require network access.

## 2. Create a Manifest from the Replication Dataset

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets create `
  --input .\Replication\LicenseFilteredRepo\licenseFilteredRepoList.csv `
  --output .\TestMap\Data\license-filtered-targets.yaml
```

Expected:

- Delimiter is detected as tab despite the `.csv` extension.
- Exactly 1,525 input rows are accounted for as emitted, deduplicated, or rejected.
- Only `name` and `lastCommitSHA` influence target content.
- Manifest targets are deterministically ordered and include source row provenance.
- A versioned rejection CSV is written beside the manifest.

Run the command again to a second path and compare target content. Generation timestamp may differ;
the ordered target records must be identical.

## 3. Verify Target Availability

For routine validation, first create a two- or three-target manifest fixture to avoid checking the
entire replication dataset.

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- targets verify `
  --file .\TestMap\Data\pinned-targets-smoke.yaml `
  --status-output .\TestMap\Output\pinned-targets-smoke-status.csv `
  --max-concurrency 2
```

Expected: the status CSV has exactly one row per manifest target. Unavailable repositories or commits
remain present with explicit statuses; they do not make the report incomplete.

## 4. Run a Pinned Collection

Point `RuntimeConfig.FilePaths.TargetFilePath` in a smoke configuration to the small manifest, then:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- collect-tests `
  --config .\TestMap\Config\pinned-target-experiment.example.json
```

Expected for each available target:

- Workspace path includes owner, repository, and full commit.
- `HEAD` is detached at the requested commit.
- SQLite database and artifacts are beneath that revision's output root.
- Persisted project provenance has equal requested and resolved commits.
- A materialization or integrity failure stops extraction for that target.

## 5. Run a Tiny Two-Lane Experiment

Use one mapped candidate and one attempt per enabled lane:

```powershell
dotnet run --project .\TestMap\TestMap.csproj -- experiment `
  --config .\TestMap\Config\pinned-target-experiment.example.json
```

Expected:

- Both lanes use the same target ID, resolved commit, baseline, and candidate cohort.
- Each attempt starts at the pinned commit and rollback restores that commit.
- Result rows use schema `3.0` and policy `pinned-target-v1`.
- Required provenance columns are non-empty and internally consistent.

## 6. Exercise Integrity Failure

Run the end-to-end fixture whose simulated agent moves `HEAD` before returning.

Expected:

- Post-attempt integrity check records `RevisionMismatch`.
- The attempt is not classified as validated or positive impact.
- No post-attempt metric evidence is accepted.
- Rollback restores the pinned commit and the following attempt starts clean.

## 7. Verify Revision Isolation

Create a manifest containing two commits of the same local fixture repository and process both.

Expected:

- Distinct workspace, database, and artifact paths.
- Cohort reuse across commits is rejected.
- Combined results share a repository family key but have distinct repository revision keys.

## 8. Run Analysis Audits

Build the normalized analysis dataset and run the evaluation audit against the smoke results.

Expected: all target/manifest/commit/integrity checks pass. Alter one requested commit in a copied CSV
and confirm the audit blocks the modified dataset.
