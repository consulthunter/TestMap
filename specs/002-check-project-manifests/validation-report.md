# Check Project Manifests Validation Report

**Validation date**: 2026-07-16  
**Feature**: `002-check-project-manifests`

## Result

The feature is implemented and passes its automated, contract, scale, CLI, and live-provider checks.
`check-projects` now consumes pinned target YAML, checks each exact commit, and publishes a complete
YAML census plus directly reusable categorized target manifests. The former default-branch and
append-to-text-file implementation has been removed.

## Automated Test Evidence

| Suite | Result |
|---|---:|
| .NET unit tests | 933 passed |
| .NET integration tests | 32 passed |
| .NET end-to-end tests | 6 passed |
| Project-check scale fixture | 10,000 targets published in approximately 1 second |

Focused tests cover schema-1/schema-2 validation, valid empty subsets, exact commit/tree requests,
evidence-policy boundaries, false positives, deterministic evidence, bounded concurrency, multiple
revisions, truncation, every provider failure class, one observation per target, report counts,
partitioning, downstream report validation, content hashes, pointer-last publication, injected
pointer failure, redaction, legacy rejection, and CLI fatal behavior.

## Live Smoke Evidence

Command:

```powershell
dotnet run --project TestMap/TestMap.csproj --no-build -- check-projects `
  --file TestMap/Data/pinned-targets-smoke.yaml `
  --output TestMap/Output/pinned-targets-smoke-project-check.yaml `
  --max-concurrency 2
```

Result:

```text
Checked: 2; tests detected: 2; no tests detected: 0; indeterminate: 0
Exit code: 0
```

Both observations have `requested_commit == observed_commit`. Evidence was recorded from the exact
commit trees at `Demo/Assets/Tests` and `test`; both trees were complete.

The same two-target check also completed with exit code 0 using
`TestMap/Config/pinned-target-check.example.json`, validating configuration-based input and
concurrency defaults in addition to direct CLI options.

Verified immutable member hashes:

| Artifact | SHA-256 |
|---|---|
| Report | `b7651328aa41b9c83457003072d760a29c396751c3ccf864549e19b34ac382d2` |
| Tests detected | `990d6c6188697bd5ced45d6e001a00889ccb0a18cc9e3bf2b2739da6c23bbd33` |
| No tests detected | `c0e14d1f27901fec2566f4d941cf0c1b109684bfd234a4d17a0b7ce5b9adf167` |

Each hash equals the value in the last-written bundle pointer. The empty no-tests-detected manifest
was successfully serialized as schema 2.

## Failure And Compatibility Checks

- A legacy text target list returns exit code 1 before remote access with a concise pinned-YAML
  requirement message.
- An unknown policy returns exit code 1 before input or provider work.
- A valid bundle with indeterminate observations returns exit code 2 and excludes those targets from
  both categorized manifests.
- Missing or changed schema-2 reports block downstream target loading.
- A failure immediately before pointer publication preserves the previous completed pointer.
- No canonical source or documentation references to `repos_with_tests.txt`,
  `repos_without_tests.txt`, `CheckProjectsRun`, `CheckProjectsStep`, or `RunMode.CheckProjects`
  remain outside historical data files and this explicit compatibility record.
- Legacy text-list input/output compatibility is intentionally not supported.

## Security And Provenance Checks

- A credential-pattern scan of generated smoke YAML, project-check fixtures, and example
  configuration found no authorization headers, bearer values, PATs, or populated API keys.
- Unit tests verify redaction of authorization, bearer, token, PAT, and API-key-shaped summaries.
- Observation target IDs are recomputed from normalized repository identity and requested commit.
- Derived targets are accepted only when the referenced report contains the same target,
  repository, commit, and declared category.
- Report and bundle schemas preserve input manifest hash, exact commits, policy name/version, status,
  tree completeness, evidence, and missingness reason.

## Known Advisory

Restore and test output continues to report the pre-existing high-severity advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (`GHSA-2m69-gcr7-jv3q`). This feature does not add or use that
dependency, but the advisory should be resolved or explicitly accepted before distributing a pilot
release.
