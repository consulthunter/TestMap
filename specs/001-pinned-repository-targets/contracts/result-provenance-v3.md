# Canonical Result Provenance Contract v3

`results_schema_version=3.0` adds required pinned-target provenance to every canonical row kind.

## Required Columns

```text
target_id
repository_identity
requested_commit
resolved_commit
target_manifest_sha256
target_source_sha256
provenance_policy_version
workspace_integrity_status
```

Existing `repo_owner`, `repo_name`, `repo_url`, and `commit_hash` remain for analysis compatibility.
For schema `3.0`, `commit_hash` is an alias of `resolved_commit`; disagreement is a blocking audit
failure.

## Invariants

1. Every row in an experiment run has the same target and manifest provenance.
2. Successful evaluated attempts have non-empty commits and `requested_commit == resolved_commit`.
3. Validated success or positive impact requires a verified workspace-integrity status.
4. Generated-test and test-result rows repeat the parent attempt provenance.
5. Cohort and resume commit identity equals `resolved_commit`.
6. Schema `2.0` rows are legacy and do not satisfy `pinned-target-v1`.

## Blocking Audit Failures

- Missing target or commit provenance on a successful evaluated row.
- Requested/resolved/legacy commit disagreement.
- Multiple manifest hashes within one run.
- Target ID not recomputable from repository identity and requested commit.
- Blocking or missing final integrity status on a validated result.
- Cohort revision mismatch.
- Revision-specific paths shared by distinct commits.
