# Data Model: Single Repository Target

**Date**: 2026-07-16  
**Feature**: [Single Repository Target](spec.md)

## Grain And Identity

The feature has two distinct grains:

1. **One repository-resolution observation per URL creation invocation.**
2. **Zero or one pinned repository target produced by that observation.**

The resolution observation is acquisition provenance, not an experiment result. The successful
target retains the existing immutable identity:

```text
target_id = SHA-256(normalized_repository + "|" + resolved_commit)
```

## Single Repository Request

Represents the locally validated user input before any remote call.

| Field | Type | Rules |
|---|---|---|
| `requested_url` | string | Exact accepted input string; no credentials, query, or fragment |
| `requested_url_sha256` | SHA-256 | Hash of exact UTF-8 `requested_url` bytes |
| `normalized_repository` | string | Lower-case `owner/repository` |
| `canonical_url` | URI | `https://github.com/{normalized_repository}.git` |
| `provider` | string | `github.com` |
| `policy_name` | string | `github-default-branch-head` |
| `policy_version` | string | `1` |
| `authentication_mode` | enum | `authenticated` or `anonymous` |

### URL Validation

- Scheme is exactly HTTPS, case-insensitive.
- Host is exactly `github.com`, case-insensitive.
- Port is absent/default.
- User information, query, and fragment are empty.
- Path has exactly two decoded non-empty components after optional `.git` and trailing slash removal.
- Owner and repository match the existing repository-identity character policy.
- Dot segments and percent-encoded path separators/dot segments are rejected.

Invalid URL requests do not become remote resolution observations because no trustworthy repository
identity exists. The CLI returns `InvalidUrl` locally and publishes no artifact.

## Provider Resolution Pass

An internal transient entity used to establish one compatible observation.

| Field | Type | Rules |
|---|---|---|
| `provider_repository` | string | Must equal request normalized repository |
| `default_branch` | string | Non-empty branch name from repository metadata |
| `branch_head_commit` | Git SHA | Full nonzero SHA from named branch resource |
| `commit_object_sha` | Git SHA | Full nonzero SHA from Git commit object; equals branch head |
| `confirmation_repository` | string | Equals first metadata identity |
| `confirmation_default_branch` | string | Equals first metadata default branch |

A pass is compatible only when all identity, branch, and SHA equalities hold. An incompatible first
pass is discarded before a second full pass begins.

## Repository Resolution Observation

The canonical YAML resolution record contains one terminal status.

| Field | Type | Availability / rules |
|---|---|---|
| `resolution_schema_version` | integer | Exactly `1` |
| `requested_at_utc` | timestamp | UTC start time |
| `completed_at_utc` | timestamp | UTC terminal time, not earlier than request |
| `requested_url` | string | Sanitized accepted URL |
| `requested_url_sha256` | SHA-256 | Must hash exact requested URL |
| `repository` | string | Normalized requested repository |
| `canonical_url` | URI | Canonical URL for repository |
| `provider` | string | `github.com` |
| `policy_name` | string | `github-default-branch-head` |
| `policy_version` | string | `1` |
| `authentication_mode` | enum | `authenticated` or `anonymous` |
| `status` | status enum | Required terminal status |
| `resolved_repository` | string or null | Required on success; exact repository identity |
| `default_branch` | string or null | Required on success |
| `resolved_commit` | Git SHA or null | Required on success; full nonzero SHA |
| `resolution_passes` | integer | `1` or `2` |
| `reason_kind` | stable string or null | Required on failure, null on success |
| `retry_after_utc` | timestamp or null | Optional for rate limits |
| `summary` | string | Required sanitized bounded explanation |

### Resolution Status Vocabulary

| Status | Manifest eligible | Meaning |
|---|---:|---|
| `Resolved` | yes | Identity, default branch, and exact commit established consistently |
| `RepositoryUnavailable` | no | Repository not found or unavailable |
| `RepositoryIdentityMismatch` | no | Provider canonical identity differs from request |
| `AuthenticationFailed` | no | Missing/rejected credentials prevent resolution |
| `AuthorizationFailed` | no | Credentials lack access |
| `RateLimited` | no | Provider quota prevents completion |
| `EmptyRepository` | no | Repository has no resolvable default branch/commit |
| `DefaultBranchUnavailable` | no | Metadata does not identify a usable default branch |
| `CommitUnavailable` | no | Named branch or commit object cannot be resolved |
| `CommitMismatch` | no | Branch head and commit object SHA differ |
| `InconsistentResolution` | no | Identity/default branch changed across both complete passes |
| `ServiceUnavailable` | no | Transient provider failure |
| `ResolutionFailed` | no | Unexpected sanitized failure |

### Observation Invariants

1. `Resolved` requires `resolved_repository == repository`, a non-empty default branch, and a full
   `resolved_commit`; it forbids `reason_kind` and `retry_after_utc`.
2. Every failure forbids a manifest-eligible target and requires `reason_kind`.
3. `RateLimited` alone may carry `retry_after_utc`.
4. `resolution_passes` is 2 only when the first complete pass was discarded for inconsistency.
5. No field contains a credential, authorization header, raw provider body, or stack trace.

## Target Manifest Schema 3

Schema 3 represents an original source target created from one URL resolution.

| Field | Type | Rules |
|---|---|---|
| `schema_version` | integer | Exactly `3` |
| `generated_at_utc` | timestamp | UTC, at or after resolution completion |
| `source.kind` | string | `github_repository_url` |
| `source.url` | string | Exact accepted requested URL |
| `source.sha256` | SHA-256 | Hash of exact URL bytes |
| `resolution.file` | basename | Content-addressed resolution YAML |
| `resolution.sha256` | SHA-256 | Must match referenced bytes |
| `targets` | target list | Exactly one target |

Schema 3 forbids `rejections` and `derivation`. Schema 1 and schema 2 retain their existing required
and forbidden fields.

### Schema-3 Target Entry

The existing target entry is unchanged:

| Field | Type | Rules |
|---|---|---|
| `target_id` | SHA-256 | Derived from repository and commit |
| `repository` | string | Equals resolution repository |
| `url` | URI | Equals resolution canonical URL |
| `commit` | Git SHA | Equals resolution commit |
| `source_rows` | integer list | Exactly `[1]`, the one logical URL request record |

## Single Repository Creation Result

Returned by successful service execution and used for CLI summary/testing.

| Field | Type | Rules |
|---|---|---|
| `manifest` | target manifest | Valid schema 3 |
| `manifest_path` | absolute path | Published last |
| `resolution` | observation | Status `Resolved` |
| `resolution_path` | absolute path | Content-addressed and hash-verified |
| `authenticated` | boolean | Derived from authentication mode, never credential content |

On a validly parsed but failed resolution, the service raises a categorized creation failure carrying
status and resolution-record path. On an invalid URL, no resolution record exists.

## Relationships

```text
Single Repository Request (1)
    |
    | provider observation
    v
Repository Resolution Observation (1) -- on Resolved --> Pinned Repository Target (1)
    |                                                       |
    | content hash                                          | contained by
    +------------------------------------------------------> Target Manifest Schema 3 (1)
```

## State Transitions

### Resolution

```text
Raw URL
  -> Locally Validated Request
  -> Resolution Pass 1
       -> Consistent -> Resolved
       -> Inconsistent -> Resolution Pass 2
                             -> Consistent -> Resolved
                             -> Inconsistent -> InconsistentResolution
       -> Explicit provider failure status

Raw URL -> InvalidUrl (no remote call, no artifact)
```

### Publication

```text
Terminal Resolution Observation
  -> Sanitize And Validate
  -> Serialize And Hash Resolution Record
  -> Publish And Verify Content-Addressed Record
  -> if Resolved: Build Schema-3 Manifest
  -> Validate Cross-Artifact Provenance
  -> Atomically Publish Manifest Last
```

A failed terminal observation stops after resolution-record publication. The requested manifest path
is never created or replaced. If an older complete manifest exists there, it remains unchanged.

## Downstream Validation

When a target reader opens schema 3, it must:

1. Verify the resolution file is a basename in the manifest directory.
2. Verify file existence and SHA-256.
3. Strictly deserialize resolution schema 1.
4. Require `Resolved` status.
5. Recompute the URL source SHA.
6. Require source URL, repository, canonical URL, commit, policy, and target identity to agree.
7. Return the pinned target without any network/provider request.
