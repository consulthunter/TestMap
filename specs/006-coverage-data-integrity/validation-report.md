# Coverage Data Integrity Validation Report

This report records implementation checks for the coverage-integrity changes. Every command must
use disposable build artifacts and isolated databases. Historical outputs under
`Replication/FilteredTestRepoCollectTests` and `Replication/MSRValidation` are read-only inputs.

## Checkpoints

| Checkpoint | Status | Evidence |
|---|---|---|
| Disposable .NET build/test artifacts | Passed | Used `%TEMP%\testmap-coverage-integrity-tests-codex-20260827` with `--artifacts-path`. |
| Isolated migration/repository database | Passed | Tests use in-memory SQLite or a GUID-named file under `%TEMP%`. |
| Foundation schema and repository tests | Passed | 10 unit tests and 9 integration tests passed. |
| Raw-first mapping and constructor tests | Passed | 6 focused `MapCoverageService` tests passed. |
| Collection outcome and CSV tests | Passed | 29 combined collector/repository/mapper/linker/CSV tests passed. |
| Provider fallback and merge tests | Passed | 17 Python runner tests passed. |
| Counter and mapped-consumer tests | Passed | 14 counter/persistence tests, 39 candidate/risk/evidence/comparison tests, 32 method/orchestration tests, and 29 legacy/null-ID candidate/risk tests passed. |
| Legacy/corrected analysis compatibility | Passed | 5 focused audit and MSR-frame tests passed. Corrected reports are publication-blocked on false claims or integrity failures; legacy rows retain unavailable counters. |
| Full Python regression | Passed | Runner `17/17`; Analysis `191/191`. Tests used isolated temporary inputs. |
| Quickstart focused commands | Passed | Parsing/counters/repository `19/19`; raw mapping `7/7`; mapped consumers `37/37`; migration integration `9/9`; CSV `8/8`; runner `17/17`. |
| Full regression suite | Passed | Solution: unit `1149/1149`, integration `38/38`, end-to-end `16/16`; total `1203/1203`. |
| `artifacts-credprovider` canary | Pending | Must use a new output/database root. |
| `nbuilder` canary | Pending | Must use a new output/database root. |
| Two-target pinned manifest | Passed | Both requested commits resolved exactly and were `Available`; manifest SHA-256 `9f99f8354ddaaf1fe2f4ffb696768948639518c2a45fee6b53f6d4ad06d42990`. |
| 344-target corrected cohort | Pending | Must run only after both canaries pass. |
| Corrected MSR audit | Pending | Must use corrected cohort output. |

## Reconciliation Examples

The deterministic service fixture produced `RawObjectCount=2`, `MappedObjectCount=1`,
`RawMemberCount=6`, and `MappedMemberCount=4`. The same run ID was mapped twice without duplicate
rows. The unmatched member and the child of the out-of-project class remained durable but created no
coverage gaps. Instance constructor overloads mapped by coverage line, the static constructor mapped
by kind, and an unresolved same-line/same-parameter constructor tie remained `Ambiguous`.

The interruption fixture deliberately faults during gap calculation after the first save; reloading
the database shows the report as `PendingAttribution` and both raw rows as `Pending`.

## Foundation Commands

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj `
  --artifacts-path $coverageTestArtifacts `
  --filter "FullyQualifiedName~CoverageIntegrityMigration|FullyQualifiedName~CoverageReportRepository|FullyQualifiedName~ObjectCoverageRepository|FullyQualifiedName~MemberCoverageRepository"

dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj `
  --artifacts-path $coverageTestArtifacts `
  --filter "FullyQualifiedName~MigrationSchema"
```

Results: unit `10/10` passed; integration `9/9` passed. The only build warning was the existing
`NU1903` advisory for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

## Raw-First Mapping Command

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj `
  --artifacts-path $coverageTestArtifacts --no-restore `
  --filter "FullyQualifiedName~MapCoverageService"
```

Result: `6/6` passed.

## Terminal Status Examples

Focused fixtures now cover missing-sidecar `CollectionFailed`, sidecar `ProviderUnavailable`,
artifact-less `NoArtifact`, malformed XML `ParseFailed`, empty XML `ParsedNoData`, interrupted
`PendingAttribution`, all-unmatched `ParsedNoUsableCoverage`, mixed `PartiallyMapped`, and fully
eligible `Mapped`. Runner classification also distinguishes provider-unavailable, failed, and
successful-but-empty attempts. `MergeFailed` is emitted by the orchestration path when the existing
merge command returns nonzero.

The collector-level fixtures additionally prove that a passing TRX outcome stays `Passed` when its
coverage report is `CollectionFailed`, that the failure report is still persisted, and that a usable
`Mapped` report is linked to the matching persisted test run without changing its run identity.

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj `
  --artifacts-path $coverageTestArtifacts --no-restore `
  --filter "FullyQualifiedName~BuildTestResultCollectorTests|FullyQualifiedName~CollectCoverageResultsServiceTests|FullyQualifiedName~MapCoverageServiceTests|FullyQualifiedName~CoverageReportRepositoryTests|FullyQualifiedName~CollectTestsResultWriterTests"
# 29/29 passed
```

## Runner Evidence

Explicit `XPlat Code Coverage` produces order `[XPlat, Microsoft]`; explicit
`Code Coverage;Format=Cobertura` produces `[Microsoft, XPlat]`. A nonzero first attempt with no
artifact proceeds to the second provider. The atomic sidecar test reconciles return code, SHA-256,
artifact path, target, framework, and merge input. Merge tests retain distinct same-name files and
same-content files from different target scope, report same-scope duplicates, and exclude an
unrecorded stale file.

## Counter and Consumer Evidence

The deterministic counter cases verify distinct lines, positive/zero hits, `1/2` and `3/4` branch
fractions, child-condition fallback, observed zero branches, malformed unavailable branches,
duplicate details, and rejection of `3/2`. Corrected mapped consumers require policy
`coverage-integrity-v1`, `HasUsableCoverage=true`, non-null member IDs, and `Mapped` attribution;
arithmetic requiring exact line counts additionally requires availability. Legacy and unmatched
rows are explicitly excluded by candidate and risk regression cases.

Commands/results:

```powershell
python -m unittest discover -s .\TestMap\Docker\validation\runner\test -p "test_*.py"
# 17/17 passed

dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --artifacts-path $coverageTestArtifacts `
  --filter "FullyQualifiedName~CoverageCounterCalculator|FullyQualifiedName~MapCoverageService|FullyQualifiedName~ObjectCoverageRepository|FullyQualifiedName~MemberCoverageRepository"
# 14/14 passed
```

## Analysis Compatibility Evidence

The coverage frame retains one row per raw member/object observation, including nullable source IDs,
source ordinals, attribution status/reason, exact-counter availability, report policy/status, run ID,
collector provenance, and reconciliation counts. Legacy databases remain readable and expose unknown
counters as unavailable rather than as measured zeros. The audit treats corrected reconciliation,
pending rows, missing terminal reasons, and impossible counters as publication blockers, while legacy
reports with actual child coverage are not mislabeled as false claims.

```powershell
python -m pytest .\Analysis\tests\test_msr_coverage_audit.py `
  .\Analysis\tests\test_build_msr_datasets.py
# 5/5 passed
```

## Full Python Regression

```powershell
python -m unittest discover -s .\TestMap\Docker\validation\runner\test -p "test_*.py"
# 17/17 passed

python -m pytest .\Analysis\tests
# 191/191 passed
```

The Analysis run used a workspace-local pytest base directory and disabled its cache provider to
avoid reading or writing historical validation outputs.

## Full .NET Regression

```powershell
$env:TEMP = (Resolve-Path .\.tmp\coverage-integrity-runtime).Path
$env:TMP = $env:TEMP
dotnet test .\TestMap.slnx `
  --artifacts-path .\.tmp\coverage-integrity-full-solution `
  --no-restore
```

Results: unit `1149/1149`, integration `38/38`, and end-to-end `16/16`, for `1203/1203`
tests across three test projects. The only warning was the existing `NU1903` advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

An earlier attempt using the user temp directory was invalid: the sandbox denied LibGit2 access,
repository-relative fixture tests could not ascend from an external artifact root, and the
end-to-end assets had not been restored there. Relocating both test temp files and artifacts under
the repository removed all of those environmental failures.

## Constitution Review

- **Row grain and missingness:** one durable row represents one raw Cobertura object/member
  observation. Stable source ordinals prevent retry duplication; nullable source IDs and explicit
  status/reason preserve unmatched observations. Counter availability distinguishes unknown from
  measured zero.
- **Identity and provenance:** reports use project plus run ID; runner sidecars retain the requested
  collector, ordered attempts, successful collector, target/framework scope, exact artifact paths,
  hashes, return codes, and merge inputs. Pinned repository/commit behavior is unchanged.
- **Isolation:** merge inputs come only from artifacts recorded during the current run. Validation
  uses disposable output/database roots; historical MSR data is read-only.
- **Consumer validity:** candidate, risk, evidence, comparison, gap, and analysis consumers require
  mapped non-null member attribution from a usable `coverage-integrity-v1` report. The audit blocks
  false usable claims, pending rows, incomplete reasons, reconciliation failures, and impossible
  counters from publication.
- **Historical compatibility:** legacy rows remain queryable with unavailable reconciliation and
  counters. They are not relabeled as corrected measurements.

No constitution exception is required.

## Minimality Review

The implementation extends the existing coverage report/object/member tables, EF repositories,
collector/mapper, validation runner, CSV writer, and MSR export/audit. It adds no coverage table,
injected service, repository family, package dependency, public CLI option, or standalone export
pipeline. The only new production helper is the pure `CoverageCounterCalculator`, which centralizes
exact counter arithmetic without adding runtime wiring or a subsystem. No project or solution file
changed.

## Canary Manifest

The existing `targets create` and `targets verify` commands generated and remotely verified a fresh
two-target manifest. Both resolved commits exactly equal their requested commits and have status
`Available`.

- Manifest: `D:\Projects\TestMap\.tmp\coverage-canary-20260827\targets.yaml`
- Manifest SHA-256: `9f99f8354ddaaf1fe2f4ffb696768948639518c2a45fee6b53f6d4ad06d42990`
- Verification CSV: `D:\Projects\TestMap\.tmp\coverage-canary-20260827\targets-status.csv`
- Verification CSV SHA-256: `c06a7e3aaed9379e7161462a3e556c0f2f81234959f6f4300410421eb9e5c04b`
