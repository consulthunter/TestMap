# Quickstart: Validate Coverage Data Integrity

## Prerequisites

- .NET 10 SDK
- Python 3.12 or newer
- Docker and the existing TestMap validation image for repository canaries
- Access to the pinned validation manifest and a fresh output/database root

Run commands from the repository root. Do not point validation at the historical
`Replication/FilteredTestRepoCollectTests` databases or overwrite `Replication/MSRValidation`.

## 1. Validate Runner Fallback and Merge Isolation

Make the runner package importable and execute its current unit suite:

```powershell
$env:PYTHONPATH = (Resolve-Path .\TestMap\Docker\validation\runner\src).Path
python -m unittest discover `
  -s .\TestMap\Docker\validation\runner\test `
  -p "test_*.py"
```

Expected coverage-specific cases:

- an explicit preferred collector is followed by the remaining default collector when it produces
  no artifact, including on nonzero return;
- fallback stops after a current-run artifact is found;
- two distinct same-name reports remain merge inputs;
- a same-scope content duplicate is identified once;
- stale and prior merged outputs are not inputs;
- the `coverage-collection-v1` sidecar reconciles provider attempts and merge inputs.

## 2. Validate Parsing, Counters, and Truthful Status

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~CollectCoverageResultsService|FullyQualifiedName~CoverageCounter|FullyQualifiedName~CoverageReportRepository"
```

Expected:

- missing artifacts and parse failures return persistable non-success reports, not successful empty
  models;
- a parsed empty/all-unmatched report has `HasUsableCoverage = false`;
- line and branch covered/valid counts are exact when details are available;
- unavailable and legacy counters are not interpreted as zero;
- report identity uses project plus run ID rather than timestamp alone.

## 3. Validate Raw-First Attribution and Constructors

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~MapCoverageService"
```

Expected:

- matched, unmatched, ambiguous, out-of-project, and unsupported object/member fixtures all persist;
- raw rows exist with `Pending` status before matching is applied, and interrupted attribution leaves
  those rows auditable;
- raw/mapped report counts reconcile exactly;
- `.ctor` and `.cctor` fixtures map by kind, line, and signature when unique;
- overloaded constructor ambiguity remains explicit;
- coverage gaps are created only for mapped members.

## 4. Validate Migration and Mapped Consumers

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~CoverageIntegrityMigration|FullyQualifiedName~CandidateMethodSelector|FullyQualifiedName~RiskFactorProvider|FullyQualifiedName~GenerationEvidence|FullyQualifiedName~AttemptMetricComparison"
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter `
  "FullyQualifiedName~MigrationSchema|FullyQualifiedName~CoverageIntegrity"
```

Expected:

- migration preserves historical rows and source IDs but marks policy/counters legacy not measured;
- nullable unmatched rows never enter mapped candidate, risk, evidence, gap, or comparison queries;
- only `coverage-integrity-v1` reports satisfy corrected coverage eligibility;
- constructors remain outside the existing method-only candidate pool.

## 5. Validate Project-Validation Output

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter `
  "FullyQualifiedName~CollectTestsResultWriter"
```

Expected:

- `HasCoverage` is false for a report-only failure, parsed-empty report, and all-unmatched report;
- `HasCoverage` is true for a corrected usable mapped report;
- appended status, reason, policy, and raw/mapped counts match the report and child rows;
- legacy reconciliation values are empty rather than zero.

## 6. Run Full Regression

Use a disposable artifacts directory so a running TestMap instance is not disturbed:

```powershell
$coverageArtifacts = Join-Path ([System.IO.Path]::GetTempPath()) `
  ("testmap-coverage-integrity-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $coverageArtifacts | Out-Null

dotnet test .\TestMap.slnx --artifacts-path $coverageArtifacts
```

Expected: all unit, integration, and end-to-end projects pass; no non-coverage outcome or mutation
semantics change.

## 7. Run the Two Pinned Canaries

Create a fresh two-target manifest using the existing target-manifest tooling and these exact targets:

```text
microsoft/artifacts-credprovider@df94ab890995c300ca293d3e8f06a3dad77fdff2
nbuilder/nbuilder@2769d20e112b56201873ddd3397e7dd8dd3e93a6
```

Run the normal pinned `collect-tests` workflow with a new output root and corrected schema. Inspect the
database, sidecars, logs, and project-validation CSV together.

Expected:

- `microsoft/artifacts-credprovider` attempts the default fallback after the preferred provider
  yields no usable artifact; its final coverage status matches what the fallback actually produced;
- `nbuilder/nbuilder` retains usable mapped coverage, nonzero exact counters, constructor observations,
  and reconciled raw/mapped totals;
- neither target reports coverage solely because a report row exists.

## 8. Recollect the Selected Cohort

After both canaries pass, run the normal pinned `collect-tests` workflow against the complete
344-target validation manifest into a new output/database root. Do not merge the new results into the
historical database in place.

The cohort passes when:

- all 344 targets have an explicit terminal collection status;
- every `HasCoverage = true` row references a `coverage-integrity-v1` report with usable mapped
  coverage;
- all raw/mapped counts reconcile and no terminal attribution remains unexplained;
- all available exact counters pass covered-not-greater-than-valid and rate checks;
- provider attempts and merge inputs reconcile to each runner sidecar;
- corrected coverage-dependent frames are regenerated before comparative analysis.

See [coverage-integrity-contract.md](contracts/coverage-integrity-contract.md) for exact status,
sidecar, reconciliation, and consumer rules.
