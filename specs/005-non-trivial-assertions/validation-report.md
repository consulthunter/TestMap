# Validation Report: Non-Trivial Assertion Detection

**Date**: 2026-07-25  
**Policy**: `assertion-lineage-v1`  
**Catalog**: `assertion-catalog-v1`  
**Results schema**: 4.0  
**Assertion sidecar schema**: 1.0

## Isolation and Active-Process Safety

TestMap was already running from the primary workspace during implementation. Builds, migration
generation, and .NET tests were therefore performed in the disposable source copy:

```text
C:\Users\milit\AppData\Local\Temp\testmap-assertion-copy-f78aef985df44b66935fca3a5d6d857e
```

The copy excluded `.git`, `bin`, live `Temp`, `Output`, `Logs`, virtual environments, databases,
and runtime logs. Existing restored dependency assets were copied so validation could run with
`--no-restore`. Migration generation and schema scripting occurred only in this disposable copy;
only the generated migration, designer, snapshot, and `schema.sql` were brought back.

No command stopped the active process or targeted its loaded binaries, logs, output directories, or
database. The final safety check confirmed PID 9736 was still running from
`D:\Projects\TestMap\TestMap\bin\Debug\net10.0\TestMap.exe` with its original
2026-07-25 09:01:34 start time. All four feature-specific disposable directories were resolved
beneath the operating-system temporary root and removed after validation.

## Managed-Code Validation

From the disposable copy:

```powershell
dotnet build .\TestMap.slnx --no-restore
dotnet test .\TestMap.UnitTests\TestMap.UnitTests.csproj --no-build --no-restore
dotnet test .\TestMap.IntegrationTests\TestMap.IntegrationTests.csproj --no-build --no-restore
dotnet test .\TestMap.EndToEndTests\TestMap.EndToEndTests.csproj --no-build --no-restore
```

Results:

| Project/check | Passed | Failed | Notes |
|---|---:|---:|---|
| Solution build | — | — | 0 errors |
| Unit tests | 1,071 | 0 | Includes 1,000-assertion sentinel and seven focused lane tests |
| Integration tests | 37 | 0 | Includes migration schema validation |
| End-to-end tests | 16 | 0 | Existing deterministic regression suite |
| Focused assertion lineage | 25 | 0 | Catalog, operands, definitions, slices, service, performance |
| Focused persistence/reporting | 22 | 0 | Repository, aggregation, audit, results and sidecar writers |
| Focused migration checks | 8 | 0 | Additive schema and expected tables |
| Focused orchestration regression | 33 | 0 | Generated execution, tool links, orchestration behavior |
| Focused TestMap/tool/lane parity | 7 | 0 | Transient/persisted evidence, missingness, aggregation, non-regression |

The required consolidated command was also invoked:

```powershell
dotnet test .\TestMap.slnx --no-build --no-restore --logger "console;verbosity=minimal"
```

Its outer command reached the 60-second limit after all three test projects had reported passing
(1,117 tests at that checkpoint). Seven focused lane tests were added afterward and the full unit
project was rerun successfully, producing a current managed-code total of 1,124 passing tests. The
separate project commands exited with code zero. The only warnings were the pre-existing `NU1903`
advisory for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.

## Python Analysis Validation

From `Analysis/`:

```powershell
uv run pytest tests/test_assertion_lineage.py tests/test_build_evaluation_dataset.py `
  tests/test_analysis_fixes.py tests/test_pinned_target_audits.py -q
uv run pytest -q
```

The focused suite passed 45 tests and the full suite passed 166 tests. A later attempt to repeat the
full command was denied because environment-managing `uv run` could fetch packages; the successful
run above had already completed against the existing project environment, so no network workaround
was attempted.

## Evidence and Contract Checks

- Classification is based on assertion-operand data dependence. Production-only control dependence
  receives no traced credit.
- The default maximum depth is four. Exactly-four-hop paths are supported; fifth-hop paths,
  cycles, ambiguous definitions/dispatch, dynamic/reflection paths, and unsupported flow are
  unresolved with stable reasons.
- Test helpers are traversed while uniquely resolved production members are terminals.
- Alternative reaching definitions use conservative reduction; disagreement is unresolved.
- Attempt, generated-test, observation, and ordered-step grains reconcile exactly.
- Unavailable counts are null. `NotApplicable` and derived historical `NotMeasured` remain distinct
  from observed zero.
- Results schema 4.0 contains attempt and generated-test summaries. The schema-1 sidecar retains
  every recognized assertion and complete parent provenance; traced-only output is derived.
- Legacy schema-3 reading is explicit and cannot promote regex/invocation inventory counts into
  semantic categories.
- Comparable lanes snapshot the same policy, catalog, depth, and path cap.

## Constitution-Focused Compatibility Review

| Concern | Review result |
|---|---|
| Row grain | Existing attempt/generated-test/test-result grains retained; assertion observation is a separate child grain |
| Target identity | Candidate and intended source member are explicit; agent attempts do not infer the target from a highest-confidence mapping |
| Missingness | `Unavailable`, `NotApplicable`, and `NotMeasured` are distinct; unavailable counts remain null |
| Policy provenance | Policy, catalog, maximum depth, and path cap are snapshotted on the experiment and measurement/export evidence |
| Multi-test attribution | Agent attempts retain child summaries and an explicit attempt aggregate; dynamic impact stays attempt-scoped |
| Acceptance non-regression | Assertion analysis is observational and does not alter validation, retry, repair, coverage, mutation, or acceptance decisions |
| Schema compatibility | Schema 4.0 is explicit; schema 3.0 remains an intentional legacy-only reader path |
| Scientific non-claim | `Traced` means production data lineage, not oracle strength, fault sensitivity, or proof of assertion-caused mutant killing |

## Outstanding Experimental Validation

A dedicated assertion-lineage source-to-export-to-Python-audit end-to-end fixture has not been added.
The existing end-to-end suite passes, and focused tests cover both lane persistence/mapping seams,
but the monolithic orchestration constructor does not currently expose a robust direct assertion for
post-refresh/pre-rollback call ordering. Those portions of T044, T045, T047, T055, and T061 remain
unchecked rather than being inferred from indirect coverage.

A paired feature-off/feature-on multi-repository pilot was not run. It requires a frozen candidate
cohort plus external model/tool execution, budgets, and environment controls beyond local
implementation validation. Consequently, the specification's median end-to-end overhead target is
not claimed here. The raw analyzer timing field and deterministic 1,000-assertion sentinel are in
place so that pilot can be conducted without changing the measurement contract.
