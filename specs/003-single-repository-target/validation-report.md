# Validation Report: Single Repository Target

**Date**: 2026-07-16  
**Result**: Passed for pilot use, subject to the existing SQLite native-package advisory below.

## Automated Regression Gate

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `TestMap.UnitTests` | 998 | 0 | 0 |
| `TestMap.IntegrationTests` | 34 | 0 | 0 |
| `TestMap.EndToEndTests` | 16 | 0 | 0 |

Focused coverage includes strict URL grammar, zero-I/O invalid input, exact named-branch and Git-commit
resolution, whole-pass consistency retry, complete failure-status mapping, sanitization, deterministic
YAML, schema-3 cross-artifact validation, publication order, previous-manifest preservation, DI
registration, offline consumption, and moving-branch immutability.

## Live Public Repository Smoke

- Requested URL: `https://github.com/powershell/platyps`
- Normalized repository: `powershell/platyps`
- Default branch: `main`
- Exact commit: `063264b97f6596469650ef6497fe83b9e51464c6`
- Authentication mode: `anonymous`
- Resolution passes: `1`
- Creation elapsed time: `967.7 ms`
- Manifest: `TestMap/Output/platyps-url-targets-final-validation.yaml`
- Resolution record: `TestMap/Output/platyps-url-targets-final-validation-resolution-ad2e1d5b04fd.yaml`

The configured environment also resolved anonymously, so authenticated-mode live validation was not
available. Automated tests cover authenticated-mode recording and prove that token content is not
part of the request, record, manifest, or success summary.

`targets verify` reported one available target and zero unavailable targets. `check-projects`
accepted the same schema-3 manifest, inspected the exact requested commit, and reported one
tests-detected observation with zero indeterminate observations. Offline configuration/target-source
integration tests loaded the same schema through `TargetSourceReader` without a provider call.

## Hash And Identity Audit

| Value | Recomputed value |
|---|---|
| Exact requested URL SHA-256 | `e204bb9d9e0e1011e176ea8b0df7cb9b6073b8f304890c1df57cf495f8a858d1` |
| Resolution record SHA-256 | `ad2e1d5b04fd57e561f96bef49d9895bb61b0e57a89fb106d19e47b8160c1739` |
| Target ID | `151ba3e0342c65b392d57d8accaffc7aed7b527d0040832cf2bbb752b95814dc` |
| Manifest SHA-256 | `735039d616139d0e7a8f654437e463cb2fc058d40e86158dbb971c290df67691` |

The URL and resolution hashes equal the values recorded in the manifest. The target ID equals the
shared identity hash for normalized repository plus exact commit. The resolution filename carries
the first 12 characters of its full hash. The manifest timestamp equals the completed resolution
timestamp, and tests record resolution publication before manifest publication.

## Compatibility And Security

- CSV creation processed 2 records, emitted 2 targets, rejected 0, and produced schema 1 with its
  content-addressed rejection CSV. Schema-3-only keys are omitted from schema-1 YAML.
- Hash-valid but semantically mismatched resolution records are rejected offline before repository
  verification or experiment work begins.
- Missing resolution records, URL-hash mismatches, target/commit mismatches, failed observations,
  malformed status values, and non-UTC retry times are rejected.
- A credential-pattern scan found no credential-shaped values in live or checked example artifacts.
  Negative-test fixtures contain only the documented `SYNTHETIC_NOT_A_SECRET` sentinels.
- `git diff --check` passed. Its only output was an existing line-ending warning for
  `TestMap/Migrations/TestMapDbContextModelSnapshot.cs`.
- No unresolved `TODO`, `TBD`, placeholder, old schema-1/schema-2-only guard, or
  `NotImplementedException` remains in the feature surface. The intentional schema switch branches
  in the shared target reader and serializer remain.

## Known Advisory And Deferral

Every .NET build reports `NU1903` for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11,
`GHSA-2m69-gcr7-jv3q`. This dependency warning predates and is independent of URL target resolution,
but it should be remediated before distributing the research environment broadly.

Explicit branch selection and caller-supplied commit overrides remain deferred. Version 1 always
observes the provider-reported default branch and pins its exact head after a consistency-checked
pass. This keeps the first contract narrow and avoids silently introducing a second resolution
policy.
