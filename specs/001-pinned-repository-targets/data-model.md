# Data Model: Pinned Repository Targets

## Identity Rules

- `repository_identity`: lower-case `owner/repository` for comparisons and keys; retain canonical
  display casing only as presentation metadata if desired.
- `requested_commit`: exactly 40 hexadecimal characters, normalized to lower case.
- `target_id`: lower-case SHA-256 of `repository_identity + "|" + requested_commit`.
- `resolved_commit`: commit observed after materialization; successful status requires exact equality
  with `requested_commit`.
- `source_sha256`: SHA-256 of exact source import bytes.
- `target_manifest_sha256`: SHA-256 of exact manifest bytes consumed by a run.
- `repository_key`: `repository_identity + "@" + resolved_commit` for analysis.
- `repository_family_key`: `repository_identity` for grouping multiple revisions.

## TargetManifest

Generated YAML document defining one sampling frame.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `schema_version` | integer | yes | Must equal `1` |
| `generated_at_utc` | UTC timestamp | yes | ISO 8601 |
| `source.file` | string | yes | Input basename, no credentials or absolute path required |
| `source.sha256` | string | yes | 64 lower-case hex characters |
| `rejections.file` | string | yes | Content-addressed rejection report basename |
| `rejections.sha256` | string | yes | SHA-256 of exact rejection report bytes |
| `targets` | ordered list | yes | At least one valid unique target |

Relationships: contains one or more `RepositoryTarget` records. The manifest itself is immutable once
used by an experiment; modifying it produces a new manifest byte hash.

## RepositoryTarget

| Field | Type | Required | Rules |
|---|---|---:|---|
| `target_id` | string | yes | 64 lower-case hex characters; recomputable |
| `repository` | string | yes | Normalized `owner/repository` |
| `url` | string | yes | Derived canonical HTTPS GitHub URL ending in `.git` |
| `commit` | string | yes | Full normalized commit SHA |
| `source_rows` | integer list | yes | Sorted, distinct, one-based logical records excluding header |

Uniqueness: `target_id`, and separately `(repository, commit)`, are unique within a manifest.

## TargetRejectionRecord

Versioned CSV row created from an invalid import record.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `report_schema_version` | string | yes | `1.0` |
| `source_file` | string | yes | Input basename |
| `source_row` | integer | yes | One-based logical record number excluding header |
| `raw_name` | string | no | Sanitized source value |
| `raw_commit` | string | no | Sanitized source value |
| `rejection_kind` | enum | yes | Stable category |
| `summary` | string | yes | Human-readable, no secrets |

`RejectionKind`: `MissingName`, `InvalidRepositoryName`, `MissingCommit`, `InvalidCommit`,
`MalformedRecord`.

## TargetExecutionRecord

Versioned CSV row initialized before project execution and published terminally even when a target
fails before database creation.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `report_schema_version` | string | yes | `1.0` |
| `manifest_sha256` | string | yes | Exact consumed manifest hash |
| `target_id` | string | yes | One row per manifest target |
| `repository` | string | yes | Normalized identity |
| `requested_commit` | string | yes | Manifest commit |
| `resolved_commit` | string | no | Present after successful materialization |
| `status` | enum | yes | One terminal status at run completion |
| `failure_stage` | string | no | Stable pipeline stage |
| `failure_kind` | string | no | Stable failure category |
| `summary` | string | yes | Sanitized explanation |
| `started_at_utc` | UTC timestamp | yes | ISO 8601 |
| `completed_at_utc` | UTC timestamp | yes for terminal | ISO 8601 |

`TargetExecutionStatus`: `Pending`, `Materialized`, `Completed`, `InvalidTarget`,
`RepositoryUnavailable`, `AuthenticationFailed`, `RateLimited`, `CommitUnavailable`,
`WorkspaceBusy`, `MaterializationFailed`, `PipelineFailed`, `IntegrityFailed`.

## TargetVerificationRecord

Versioned CSV row; exactly one per manifest target.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `report_schema_version` | string | yes | `1.0` |
| `manifest_sha256` | string | yes | Hash of verified manifest bytes |
| `target_id` | string | yes | Matches manifest |
| `repository` | string | yes | Matches manifest |
| `requested_commit` | string | yes | Matches manifest |
| `resolved_commit` | string | no | Required only for `Available` |
| `status` | enum | yes | One terminal status |
| `verified_at_utc` | UTC timestamp | yes | ISO 8601 |
| `summary` | string | yes | Sanitized explanation |

`TargetVerificationStatus`: `Available`, `InvalidTarget`, `RepositoryUnavailable`,
`AuthenticationFailed`, `RateLimited`, `CommitUnavailable`, `VerificationFailed`.

State transition: `Pending -> terminal status`. Reports never omit a target because of its status.

## MaterializedRevision

Runtime state established before extraction and copied into persisted provenance.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `target_id` | string | yes | Manifest target ID |
| `repository_identity` | string | yes | Normalized identity |
| `requested_commit` | string | yes | Manifest commit |
| `resolved_commit` | string | yes on success | Must equal requested commit |
| `origin_url` | string | yes on success | Normalized and credential-redacted |
| `workspace_path` | string | yes | Revision-isolated path |
| `database_path` | string | yes | Revision-isolated path |
| `artifact_path` | string | yes | Revision-isolated path |
| `manifest_sha256` | string | yes | Exact consumed manifest hash |
| `source_sha256` | string | yes | Manifest source hash |
| `materialized_at_utc` | UTC timestamp | yes on success | ISO 8601 |
| `status` | enum | yes | Materialization terminal state |
| `policy_version` | string | yes | `pinned-target-v1` |

`MaterializationStatus`: `Pending`, `Available`, `InvalidWorkspace`, `WrongOrigin`, `WorkspaceDirty`,
`WorkspaceBusy`, `AuthenticationFailed`, `RepositoryUnavailable`, `CommitUnavailable`, `Failed`.

State transition: `Pending -> Available | failure`. Only `Available` permits extraction.

## ExperimentRun Provenance Extension

Persist the following required fields for pinned experiments:

- `target_id`
- `repository_identity`
- `requested_commit`
- `resolved_commit`
- `target_manifest_sha256`
- `target_source_sha256`
- `materialized_at_utc`
- `workspace_integrity_status`
- `provenance_policy_version`

Successful run creation requires a materialized revision. The final integrity status is updated before
publication. Existing cohort relation remains; cohort commit must equal `resolved_commit`.

## WorkspaceIntegrityObservation

Append-only SQLite audit entity.

| Field | Type | Required | Rules |
|---|---|---:|---|
| `id` | integer | yes | Database identity |
| `project_id` | integer | yes | Current revision database project |
| `experiment_run_id` | integer | no | Null for pre-experiment pipeline checks |
| `target_id` | string | yes | 64-char hash |
| `producer_lane` | string | no | `testmap` or `agent-tool` when attempt-scoped |
| `work_item_stable_key` | string | no | Existing stable key |
| `attempt_number` | integer | no | Positive when attempt-scoped |
| `checkpoint` | enum string | yes | Protected boundary |
| `expected_commit` | string | yes | Pinned base commit |
| `actual_commit` | string | no | Null when Git state cannot be read |
| `origin_matches` | boolean | yes | False when unknown or mismatched |
| `working_tree_dirty` | boolean | no | Null when status unavailable |
| `status` | enum string | yes | Observation result |
| `details` | string | yes | Sanitized structured summary |
| `observed_at_utc` | UTC timestamp | yes | ISO 8601 |

`IntegrityCheckpoint`: `PreExtraction`, `PreBaseline`, `ExperimentStart`, `PreAttempt`,
`PostAttemptPreAnalysis`, `PostRollback`, `PreResultsPublication`.

`WorkspaceIntegrityStatus`: `VerifiedClean`, `VerifiedExpectedChanges`, `RevisionMismatch`,
`OriginMismatch`, `UnexpectedChanges`, `RepositoryInvalid`, `StatusUnavailable`, `RestoreFailed`.

Indexes:

- `(experiment_run_id, checkpoint, status)`
- `(target_id, observed_at_utc)`
- `(work_item_stable_key, attempt_number, checkpoint)`

## Attempt Provenance Extensions

Both built-in and tool attempts carry:

- `base_commit`: required resolved commit
- `workspace_integrity_status`: final attempt integrity status

Tool attempts retain their existing base commit field. Built-in generation attempts gain an
equivalent field. Attempt impact is not evaluable when final integrity is blocking.

## Canonical Result Schema 3.0 Extensions

All row kinds carry the parent attempt's target provenance:

- `target_id`
- `repository_identity`
- `requested_commit`
- `resolved_commit`
- `target_manifest_sha256`
- `target_source_sha256`
- `provenance_policy_version`
- `workspace_integrity_status`

Validation rules:

- `results_schema_version == 3.0` implies all fields above are present.
- Successful evaluated rows require `requested_commit == resolved_commit`.
- `workspace_integrity_status` must be a verified status for validated success or positive impact.
- Child generated-test/test-result rows repeat attempt provenance and do not create new integrity
  observations or change metric attribution.
