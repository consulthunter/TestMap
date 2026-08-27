# Quickstart: Validate Non-Trivial Assertion Detection

## Prerequisites

- .NET 10 SDK
- Python environment for `Analysis` managed with `uv`
- A clean checkout with the feature migration applied by the test harness

Run all commands from the repository root unless noted.

## Safe Validation While TestMap Is Running

Do not stop the active TestMap process, clean its output directories, reuse its database, or build
into repository `bin/` and `obj/` paths. Allocate a new operating-system temporary root for each
validation session:

```powershell
$assertionRunRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    "testmap-assertion-lineage-" + [Guid]::NewGuid().ToString("N"))
$assertionArtifacts = Join-Path $assertionRunRoot "artifacts"
$assertionWorkspace = Join-Path $assertionRunRoot "workspace"
$assertionDatabase = Join-Path $assertionRunRoot "assertion-lineage.sqlite"
New-Item -ItemType Directory -Path $assertionArtifacts, $assertionWorkspace | Out-Null
Resolve-Path -LiteralPath $assertionRunRoot
```

Append `--artifacts-path $assertionArtifacts` to every `dotnet build` or `dotnet test` command below.
Configure fixtures to use `$assertionWorkspace` and `$assertionDatabase`; never point them at
`TestMap/Output`, `TestMap/Logs`, `TestMap/Temp`, or the running program's SQLite file.

If migration generation or another SDK tool still writes to locked repository outputs, copy the
current source into the disposable workspace while excluding `.git`, `bin`, `obj`, runtime logs,
databases, and generated outputs. Run the tool in that copy and bring back only the intended
migration, model snapshot, schema, or source files:

```powershell
robocopy . $assertionWorkspace /E /XD .git bin obj Temp Output Logs .venv `
    /XF *.db *.db-shm *.db-wal *.sqlite *.log | Out-Null
```

Before cleanup, resolve the path again and verify it begins with
`[System.IO.Path]::GetTempPath()`. Remove only that exact disposable directory:

```powershell
$resolvedAssertionRunRoot = (Resolve-Path -LiteralPath $assertionRunRoot).Path
$resolvedSystemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $resolvedAssertionRunRoot.StartsWith(
        $resolvedSystemTemp,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove a non-temporary path: $resolvedAssertionRunRoot"
}
Remove-Item -LiteralPath $resolvedAssertionRunRoot -Recurse -Force
```

## 1. Validate Catalog and Backward Slicing

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~AssertionLineage|FullyQualifiedName~AssertionPatternCatalog|FullyQualifiedName~ReachingDefinition"
```

Expected:

- direct calls, local assignments, constructions, properties, awaited values, and in-bound helper
  returns are `Traced`;
- literals and fully analyzed test-local computations are `Trivial`;
- mixed definitions, a fifth hop, helper cycles, ambiguous dispatch, reflection, dynamic calls,
  unsupported fields/aliases, and missing source are `Unresolved`;
- an unrelated arrange/act production call does not make a literal assertion traced;
- a production-derived branch predicate alone does not make its body assertion traced;
- each fluent chain produces one logical assertion.

## 2. Validate Persistence and Historical Compatibility

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~AssertionLineagePersistence|FullyQualifiedName~AssertionLineageMigration"
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter "FullyQualifiedName~MigrationSchema"
```

Expected:

- one attempt measurement owns per-test summaries, observations, and ordered lineage steps;
- complete counts reconcile exactly;
- unavailable counts are null and have stable reasons;
- traced observations contain a production-member terminal;
- migration from the previous schema leaves historical attempts queryable as `NotMeasured`.

## 3. Validate Both Evaluation Lanes

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~AssertionLineageLaneParity|FullyQualifiedName~ExperimentOrchestration|FullyQualifiedName~ToolAttemptGeneratedTest"
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --filter "FullyQualifiedName~AssertionLineagePipeline"
```

Expected:

- equivalent test/candidate fixtures receive identical policy, category, and trace conclusions in
  both lanes;
- multiple agent-created tests retain separate summaries;
- agent coverage and mutation stay attempt-attributed;
- assertion analysis completes before rollback;
- trivial, unresolved, or unavailable lineage does not change validation or acceptance.

## 4. Validate Export, Filtering, and Audit

```powershell
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --filter "FullyQualifiedName~ExperimentResultsWriter|FullyQualifiedName~AssertionObservationWriter"

Push-Location .\Analysis
uv run pytest tests/test_assertion_lineage.py tests/test_build_evaluation_dataset.py tests/test_analysis_fixes.py
Pop-Location
```

Expected:

- results rows use schema 4.0 and assertion observations use schema 1.0;
- raw counts reconcile across attempt, generated-test, and assertion grains;
- traced-only datasets filter semantic `Traced` rows without deleting excluded categories;
- schema-3 and historical rows become `NotMeasured`, not zero or trivial;
- seeded invalid categories, missing reasons, broken trace terminals, reconciliation failures, and
  cross-lane policy mismatches block publication audit;
- regex assertion counts are not accepted as classified publication evidence.

See [assertion-lineage-contract.md](contracts/assertion-lineage-contract.md) for the exact
vocabularies and reconciliation rules.

## 5. Run the Deterministic End-to-End Fixture

```powershell
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj --filter "FullyQualifiedName~AssertionLineageExperiment"
```

Expected:

- the fixture materializes tests for both lanes without a live model call;
- semantic refresh and generated-test attribution occur before analysis;
- persisted and exported policy, candidate, attempt, member, content hash, category, and trace
  evidence agree;
- dataset construction completes and strict assertion audit passes.

## 6. Run Full Regression

```powershell
dotnet test .\TestMap.slnx

Push-Location .\Analysis
uv run pytest
Pop-Location
```

Expected: all existing generation, workspace, persistence, reporting, and analysis tests continue to
pass. Coverage, mutation, candidate selection, retries, and acceptance retain their prior semantics.

## 7. Validate Performance

First run the deterministic 1,000-assertion regression sentinel and record analyzer duration. Then run
paired feature-off and feature-on evaluations on the same frozen multi-repository pilot:

- identical repository identities and commits;
- identical candidate cohort and seed;
- identical model/tool versions, configuration, budgets, retries, and ordering;
- both supported lanes;
- warm-up policy documented consistently.

Compare median end-to-end attempt duration across paired observations. The feature passes when the
feature-on median increases by no more than 10%. Retain raw per-attempt timings, assertion-analysis
duration, policy/catalog/depth, and environment provenance with the validation report.

## Validation Record (2026-07-25)

The implementation was validated from the disposable source copy
`C:\Users\milit\AppData\Local\Temp\testmap-assertion-copy-f78aef985df44b66935fca3a5d6d857e`.
The copy retained restored dependency assets so all .NET commands could use `--no-restore`; no build
or test command targeted the active repository's `bin`, `obj`, logs, output, or database paths.

| Checkpoint | Result |
|---|---|
| Solution build (`dotnet build .\TestMap.slnx --no-restore`) | Passed; 0 errors |
| Assertion-lineage classifier/service/config tests | 25 passed |
| Persistence/reporting focused tests | 22 passed |
| Migration integration tests | 8 passed |
| Orchestration/lineage regression selection | 33 passed |
| Focused TestMap/tool/lane-parity tests | 7 passed |
| Full unit project | 1,071 passed |
| Full integration project | 37 passed |
| Full end-to-end project | 16 passed |
| Focused Python assertion/dataset/audit tests | 45 passed |
| Full Python analysis suite | 166 passed |

The consolidated `dotnet test .\TestMap.slnx --no-build --no-restore` wrapper reached the 60-second
command limit after reporting all three constituent projects passed (1,117 tests at that checkpoint).
The seven focused lane tests were added afterward and the full unit project was rerun successfully,
bringing the current managed-code total to 1,124 tests. Each
constituent project was also run separately and exited successfully. The only build warnings were
the repository's existing `NU1903` advisory for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

The deterministic 1,000-assertion analyzer sentinel is part of the unit suite. A paired
feature-off/feature-on frozen-repository pilot is intentionally not claimed by this local validation;
it requires a separately authorized experiment run with fixed model/tool and repository inputs.
