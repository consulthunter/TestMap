# Research: Single Repository Target

**Date**: 2026-07-16  
**Feature**: [Single Repository Target](spec.md)

## Decision 1: Accept Strict GitHub HTTPS Repository URLs

**Decision**: URL mode accepts only these equivalent shapes, case-insensitively for host and
repository identity:

```text
https://github.com/{owner}/{repository}
https://github.com/{owner}/{repository}.git
```

One trailing slash is allowed. User information, ports, query strings, fragments, extra path
segments, empty components, dot segments, and non-HTTPS or non-GitHub hosts are rejected before any
provider call. The original accepted string is retained in provenance; normalized identity and
canonical clone URL are derived through the existing target identity policy.

**Rationale**: A narrow grammar makes one repository unambiguous and blocks accidental issue,
branch, commit, or credentials-bearing URLs. It matches TestMap's existing canonical URL and avoids
silently treating a web subpage as a repository.

**Alternatives considered**:

- Accept SSH/scp forms such as `git@github.com:owner/repo.git`: useful for cloning but not a single
  portable URL grammar; defer to a future version.
- Accept repository names without a URL: already supported indirectly by the CSV path and does not
  satisfy the explicit URL workflow.
- Accept arbitrary Git hosts: rejected because provider resolution and authentication semantics are
  GitHub-specific in v1.

## Decision 2: Resolve Metadata, Branch, Commit, Then Metadata Again

**Decision**: One resolution pass performs:

1. Get repository metadata and require returned `full_name` to equal the normalized requested
   identity.
2. Require a non-empty `default_branch`.
3. Get that named branch and capture its full head commit SHA.
4. Get the Git commit object by the captured SHA and require exact SHA equality.
5. Get repository metadata again and require the same `full_name` and `default_branch`.

If step 5 disagrees, discard the entire pass and retry once from step 1. A second disagreement
produces `InconsistentResolution`. No fields from different passes are combined.

**Rationale**: GitHub repository metadata exposes `full_name` and `default_branch`; the branch
endpoint exposes its head commit, and the Git commit endpoint establishes that the full SHA denotes a
commit object in that repository. The second metadata read detects repository transfer/default-branch
changes during observation. GitHub documents these repository and branch resources in its
[repository](https://docs.github.com/en/rest/repos/repos) and
[branch](https://docs.github.com/en/rest/branches) REST contracts.

**Alternatives considered**:

- Resolve only the default branch name: not reproducible because the branch moves.
- Query the latest repository commit without naming the default branch: can select a different ref or
  depend on endpoint ordering.
- Clone the repository: exact but much heavier than metadata resolution; exact cloning remains a
  downstream materialization responsibility.
- Require the branch head to remain unchanged through a second branch read: too strict and can fail
  active repositories unnecessarily; the first observed full commit remains a valid immutable target.

## Decision 3: Reject Redirected Or Renamed Identity

**Decision**: If provider metadata returns a `full_name` different from the normalized requested
repository, classify `RepositoryIdentityMismatch` and publish no manifest. Do not silently adopt the
new identity.

**Rationale**: Automatic transfer following would make the user's requested sampling unit differ
from the emitted target. GitHub recommends following redirects for API operation, but scientific
target identity still needs an explicit user decision when canonical identity changes. A later
feature may offer an opt-in migration workflow.

**Alternatives considered**:

- Follow and persist the new identity automatically: convenient but changes the selected repository.
- Keep the old identity with the new repository's commit: invalid target identity.

## Decision 4: Add Target Manifest Schema 3

**Decision**: Preserve schema 1 for delimited imports and schema 2 for project-check subsets. Add
schema 3 for URL-resolved source targets:

- `source.kind` is `github_repository_url`.
- `source.url` is the accepted original URL.
- `source.sha256` hashes the exact UTF-8 URL value.
- `resolution.file` and `resolution.sha256` reference a content-addressed resolution record.
- `rejections` and `derivation` are absent.
- `targets` contains exactly one target.

The target keeps `source_rows: [1]`, meaning logical input record 1 of the one-URL source.

**Rationale**: Reusing schema-1 `rejections` or schema-2 `derivation` would misstate provenance.
Schema 3 preserves one shared target entry shape and gives downstream consumers an explicit rule for
validating URL resolution.

**Alternatives considered**:

- Emit schema 1 with a synthetic input file: rejected because no delimited file or rejection report
  exists.
- Create an unrelated single-target format: rejected because verification, screening, collection,
  and experiment commands would need conversion.
- Remove `source_rows`: creates a second target-entry shape and unnecessary downstream branching.

## Decision 5: Publish A Resolution Record For Every Attempt

**Decision**: Serialize and content-address a resolution record after every validly parsed URL
attempt, whether resolution succeeds or fails. On success, verify the record hash and atomically
publish the schema-3 manifest last. On failure, return the record path and status but leave any
existing manifest at the requested output path unchanged.

**Rationale**: Failures are scientifically useful acquisition evidence, but a failure artifact must
not look like a usable target. Content addressing makes retries append-safe without requiring a
mutable log. Manifest-last publication retains the complete-bundle pattern used elsewhere.

**Alternatives considered**:

- Emit no record on failure: loses the reason a selected repository disappeared from a pilot.
- Overwrite the manifest with a failed state: downstream consumers could mistake it for a target.
- Put failure fields directly in target YAML: violates one-target manifest semantics.

## Decision 6: Use Policy `github-default-branch-head/v1`

**Decision**: The resolution record names provider `github.com` and policy
`github-default-branch-head/v1`. Any later change to URL grammar, consistency checks, retry behavior,
or revision selection receives a new policy version.

**Rationale**: The commit selection rule affects source identity and must not change silently.

**Alternatives considered**:

- Leave the behavior implicit in code: not auditable across tool versions.
- Use manifest schema alone as the policy: schema identifies structure, not revision-selection
  semantics.

## Decision 7: Do Not Sleep Or Automatically Retry Rate Limits

**Decision**: Classify provider rate limiting explicitly and stop the resolution attempt. The only
automatic retry is the immediate complete retry for internally inconsistent repository metadata.
The resolution record may include a sanitized retry-after/reset time when available.

**Rationale**: GitHub advises callers not to retry until `retry-after` or reset conditions permit and
warns that continued requests can result in banning. A CLI invocation should not sleep for an
unbounded provider interval. See GitHub's
[REST API best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api)
and [rate-limit guidance](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api).

**Alternatives considered**:

- Immediate exponential retry: conflicts with provider guidance for primary/secondary limits.
- Wait until reset automatically: can make a one-target command block for an unpredictable duration.

## Decision 8: Record Authentication Mode, Never Credentials

**Decision**: Use `GITHUB_TOKEN` when non-empty; otherwise attempt anonymous resolution. Persist only
`authenticated` or `anonymous`. Reuse the project's credential-pattern sanitizer for summaries, and
map provider exceptions to a closed status/reason vocabulary.

**Rationale**: Authentication mode explains availability and rate-limit context without exposing a
secret. GitHub notes that authenticated requests generally have higher primary limits.

**Alternatives considered**:

- Require a token for all URL resolution: unnecessary for public repositories.
- Persist token type, prefix, scopes, or account identity: not needed for target provenance and
  increases disclosure risk.

## Decision 9: Keep File Creation Behavior Untouched

**Decision**: `--input` continues through the existing delimited import service, rejection CSV,
schema-1 manifest, defaults, and summaries. URL mode uses a separate creation result and service.
Only CLI input arbitration is shared.

**Rationale**: The existing file workflow is validated at scale and has different provenance and
failure accounting. A shared super-service would obscure rather than reduce complexity.

**Alternatives considered**:

- Convert URL input into a temporary one-row CSV: would fabricate file provenance, complicate
  cleanup, and omit branch-resolution evidence.
- Refactor both flows into one generic importer before adding URL mode: unnecessary risk and scope.

## Decision 10: No Persistence Or Analysis Schema Change

**Decision**: Do not add database migrations, result columns, or analysis datasets. Existing project
and experiment provenance receives repository, exact commit, manifest hash, and source hash from the
schema-3 target exactly as it does for other manifests.

**Rationale**: URL resolution precedes project materialization. The file bundle is the canonical
acquisition record; downstream research observations already persist immutable target provenance.
