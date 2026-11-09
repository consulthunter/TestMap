# File Contracts: Target Reports v1

## Rejection Report

CSV header, in canonical order:

```text
report_schema_version,source_file,source_row,raw_name,raw_commit,rejection_kind,summary
```

One row exists for each rejected logical data record, numbered from one after the header. Deduplicated valid rows are represented in the
manifest target's `source_rows` and are not rejection rows unless their metadata conflicts.

## Target Execution Report

CSV header, in canonical order:

```text
report_schema_version,manifest_sha256,target_id,repository,requested_commit,resolved_commit,status,failure_stage,failure_kind,summary,started_at_utc,completed_at_utc
```

The report contains exactly one row per manifest target. It is initialized before project execution
and every row is terminal when the measured run completes, including failures before SQLite exists.

## Verification Report

CSV header, in canonical order:

```text
report_schema_version,manifest_sha256,target_id,repository,requested_commit,resolved_commit,status,verified_at_utc,summary
```

One row exists for every manifest target, in manifest order. `resolved_commit` is populated only when
the verifier established the requested commit. Unavailable targets remain in the report.

## Atomicity and Encoding

- UTF-8 without BOM.
- RFC-compatible quoting for commas, quotes, and line breaks.
- Stable invariant formatting and ISO 8601 UTC timestamps.
- Write reports to sibling temporary files and flush them. Rejection reports use immutable,
  content-addressed names; publish the manifest last after verifying the referenced report hash.
- Replace mutable verification and execution reports atomically after every complete snapshot.
- Failure before replacement leaves the prior complete report untouched.
