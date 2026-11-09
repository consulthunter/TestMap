# Data Model: Check Project Manifests

**Date**: 2026-07-16  
**Feature**: [Check Project Manifests](spec.md)

## Grain And Identity

The canonical grain is one **project-check observation per immutable repository target**. A target is
identified by:

```text
target_id = SHA-256(normalized repository identity + exact commit)
repository_revision = normalized_repository + "@" + full_commit_sha
```

Different commits of one repository are distinct observations. They are never deduplicated or
aggregated into one repository result.

## Target Manifest

The shared in-memory target manifest supports two strict schema variants.

### Source Target Manifest, Schema 1

Represents targets created from the original delimited repository dataset.

| Field | Type | Rules |
|---|---|---|
| `schema_version` | integer | Exactly `1` |
| `generated_at_utc` | timestamp | UTC |
| `source.file` | basename | Original input filename |
| `source.sha256` | SHA-256 | Hash of original input bytes |
| `rejections.file` | basename | Content-addressed rejection report |
| `rejections.sha256` | SHA-256 | Must match report bytes |
| `targets` | target list | At least one, unique and deterministically ordered |

### Derived Target Manifest, Schema 2

Represents one categorized subset produced by `check-projects`.

| Field | Type | Rules |
|---|---|---|
| `schema_version` | integer | Exactly `2` |
| `generated_at_utc` | timestamp | UTC; excluded from deterministic-content comparisons |
| `source.file` | basename | Parent target manifest filename |
| `source.sha256` | SHA-256 | Exact parent manifest hash |
| `derivation.kind` | string | `project_test_presence` |
| `derivation.category` | enum | `tests_detected` or `no_tests_detected` |
| `derivation.policy_name` | string | `project-test-presence` |
| `derivation.policy_version` | string | `1` for this feature |
| `derivation.report.file` | basename | Content-addressed check report |
| `derivation.report.sha256` | SHA-256 | Must match report bytes |
| `targets` | target list | May be empty; otherwise unique and deterministically ordered |

Schema 1 requires `rejections` and forbids `derivation`. Schema 2 requires `derivation` and forbids
`rejections`. This prevents ambiguous provenance.

## Repository Target

An existing target entry is preserved byte-for-field when copied to a derived manifest.

| Field | Type | Rules |
|---|---|---|
| `target_id` | SHA-256 | Must equal identity derived from repository and commit |
| `repository` | string | Lower-case normalized `owner/repository` |
| `url` | URI | Canonical GitHub clone URL for repository |
| `commit` | Git SHA | Full 40-character lower-case nonzero SHA |
| `source_rows` | integer list | Positive, sorted, distinct logical source records |

`check-projects` does not create target IDs, normalize changed values, or replace source rows. A
field mismatch between input and derived output is a blocking contract failure.

## Project Check Policy

| Field | Type | Rules |
|---|---|---|
| `name` | string | `project-test-presence` |
| `version` | string | `1` |
| `description` | string | Human-readable scope; no claim of semantic test discovery |
| `evidence_categories` | string list | Stable vocabulary defined below |

### Evidence Categories

- `TestDirectory`: an exact path segment `test` or `tests`.
- `TestProjectFile`: a supported project filename with a bounded `test` or `tests` token.
- `TestSourceFile`: a supported source filename with a bounded `Test` or `Tests` suffix.

The policy returns the first match after paths are normalized and sorted ordinally. This makes
evidence stable even if the provider returns a different tree order.

## Project Check Observation

Exactly one terminal observation is required for each unique input target.

| Field | Type | Availability / rules |
|---|---|---|
| `target_id` | SHA-256 | Required; equals input target |
| `repository` | string | Required; equals input target |
| `requested_commit` | Git SHA | Required; equals input target |
| `observed_commit` | Git SHA or null | Present only when the provider resolves the requested commit |
| `status` | status enum | Required terminal status |
| `policy_name` | string | Required |
| `policy_version` | string | Required |
| `evidence_category` | enum or null | Required only for `TestsDetected` |
| `evidence_path` | path or null | Required only for `TestsDetected`; repository-relative, `/` separators |
| `tree_complete` | boolean or null | `false` for truncated responses; null when no tree was obtained |
| `reason_kind` | stable string | Required for indeterminate statuses; null for determinate statuses |
| `summary` | string | Required, sanitized, bounded human-readable explanation |
| `checked_at_utc` | timestamp | Required UTC timestamp |

### Status Vocabulary

| Status | Class | Meaning |
|---|---|---|
| `TestsDetected` | determinate positive | Policy evidence observed at the exact commit |
| `NoTestsDetected` | determinate negative | Complete exact-commit tree contained no policy evidence |
| `InvalidTarget` | indeterminate | Target failed validation |
| `RepositoryIdentityMismatch` | indeterminate | Provider repository identity differs from target |
| `RepositoryUnavailable` | indeterminate | Repository could not be found or accessed as a repository |
| `AuthenticationFailed` | indeterminate | Credentials were absent or rejected |
| `AuthorizationFailed` | indeterminate | Credentials did not permit the operation |
| `RateLimited` | indeterminate | Provider quota prevented completion |
| `CommitUnavailable` | indeterminate | Exact requested commit was not available |
| `CommitMismatch` | indeterminate | Provider returned a different commit identity |
| `TreeUnavailable` | indeterminate | Commit resolved but its tree could not be read |
| `TreeTruncated` | indeterminate | Partial tree had no positive evidence, so absence is unknown |
| `ServiceUnavailable` | indeterminate | Transient provider failure prevented completion |
| `CheckFailed` | indeterminate | Unexpected sanitized target-level failure |

### Observation Invariants

1. `observed_commit`, when present, must equal `requested_commit`; otherwise status is
   `CommitMismatch` and the observation cannot be categorized.
2. `TestsDetected` requires evidence category and path; `NoTestsDetected` forbids both.
3. `NoTestsDetected` requires `tree_complete=true`.
4. `TreeTruncated` requires `tree_complete=false` and no evidence.
5. Every indeterminate status requires a stable `reason_kind`.
6. Provider exception text, request headers, and credentials are never persisted verbatim.

## Project Check Report

The report is the canonical screening census.

| Field | Type | Rules |
|---|---|---|
| `report_schema_version` | integer | Exactly `1` |
| `generated_at_utc` | timestamp | UTC |
| `input.file` | basename | Input target manifest |
| `input.sha256` | SHA-256 | Exact input manifest bytes |
| `input.schema_version` | integer | Supported target schema |
| `policy.name` | string | Required |
| `policy.version` | string | Required |
| `summary.input_targets` | integer | Count of unique input targets |
| `summary.tests_detected` | integer | Positive determinate count |
| `summary.no_tests_detected` | integer | Negative determinate count |
| `summary.indeterminate` | integer | All other statuses |
| `summary.by_status` | map | Contains every status with a nonnegative count |
| `observations` | observation list | Exactly one per input target, target-order deterministic |

Count invariant:

```text
input_targets = tests_detected + no_tests_detected + indeterminate
             = sum(summary.by_status)
```

## Project Check Bundle

The stable bundle pointer is the completion marker for one publication.

| Field | Type | Rules |
|---|---|---|
| `bundle_schema_version` | integer | Exactly `1` |
| `generated_at_utc` | timestamp | UTC |
| `input.file` | basename | Input manifest filename |
| `input.sha256` | SHA-256 | Input manifest hash |
| `policy.name` / `version` | string | Must match report and derived manifests |
| `complete` | boolean | Always `true` in a published pointer |
| `classification_complete` | boolean | True only when indeterminate count is zero |
| `artifacts.report` | file/hash/count | Required immutable report reference |
| `artifacts.tests_detected` | file/hash/count | Required derived manifest reference |
| `artifacts.no_tests_detected` | file/hash/count | Required derived manifest reference |

All artifact paths are basenames in the same directory as the pointer. All member hashes are
verified before the pointer is published.

## Relationships

```text
Source Target Manifest (1)
    |
    | input SHA-256
    v
Project Check Report (1) ---- contains ----> Project Check Observation (N, exactly targets N)
    |
    | report SHA-256 + category
    +----> Tests-Detected Target Manifest (1)
    +----> No-Tests-Detected Target Manifest (1)
    |
    +---- all three referenced by ----> Project Check Bundle Pointer (1)
```

## State Transitions

### Target Check

```text
Pending
  -> RepositoryResolved
  -> CommitResolved
  -> TreeRead
  -> TestsDetected | NoTestsDetected | TreeTruncated

Pending / RepositoryResolved / CommitResolved / TreeRead
  -> one explicit indeterminate terminal status
```

No terminal observation is reclassified in place. A rerun creates a new report and bundle with its
own check time and content hashes.

### Bundle Publication

```text
ValidatedInput
  -> CompleteObservations
  -> ReportSerializedAndHashed
  -> DerivedManifestsSerializedAndHashed
  -> ImmutableMembersPublishedAndVerified
  -> BundlePointerPublished
```

Failure before the final transition leaves the previous bundle pointer unchanged. Unreferenced
content-addressed members are harmless and may be cleaned separately.
