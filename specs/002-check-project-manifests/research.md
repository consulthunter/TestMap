# Research: Check Project Manifests

**Date**: 2026-07-16  
**Feature**: [Check Project Manifests](spec.md)

## Decision 1: Coordinate At Manifest Grain

**Decision**: Replace the current per-project pipeline run with one manifest-level coordinator. It
validates the full input first, schedules bounded target probes, waits for one terminal observation
per target, validates the partition, and publishes one bundle.

**Rationale**: The unit of analysis is a target and the required artifact is a complete sampling-frame
census. The current pipeline creates a service per project and serializes concurrent append calls to
two files. It has no point at which it can prove complete target accounting, deterministic ordering,
or cross-file consistency.

**Alternatives considered**:

- Keep the pipeline and append YAML fragments: rejected because YAML documents cannot be safely
  assembled through concurrent append and partial runs look complete.
- Write one temporary result per project and merge later: workable, but introduces unnecessary
  intermediate state for a lightweight remote check and still needs a manifest-level finalizer.

## Decision 2: Resolve Commit Then Tree

**Decision**: For each target, query repository metadata to verify identity, retrieve the Git commit
identified by the full requested SHA, require the returned SHA to match, then request the recursive
tree using the commit's tree SHA.

**Rationale**: Repository contents and default-branch APIs inspect a moving branch. A commit object
provides the exact tree identity needed for revision-pinned classification. GitHub's REST contracts
expose the tree SHA on the Git commit and accept a tree SHA for recursive tree retrieval:
[Git commits](https://docs.github.com/en/rest/git/commits) and
[Git trees](https://docs.github.com/en/rest/git/trees).

**Alternatives considered**:

- Continue reading top-level contents and the default branch: rejected because it violates pinned
  provenance and can classify the wrong source state.
- Clone every repository before checking: exact but substantially more bandwidth and storage than a
  tree-only screening command; later collection already performs exact materialization.
- Pass the commit SHA directly to the tree endpoint: rejected because the endpoint contract asks for
  a tree SHA or ref; resolving the commit first is explicit and auditable.

## Decision 3: Treat Truncation As Missingness

**Decision**: A truncated recursive tree may support `TestsDetected` when recognized evidence is
present in the returned entries. It may not support `NoTestsDetected`; when no evidence is present,
the terminal status is `TreeTruncated`.

**Rationale**: GitHub documents a recursive-tree limit of 100,000 entries or 7 MB and returns a
`truncated` flag. Absence from a partial tree is not negative evidence. A future policy can add
non-recursive traversal without changing the meaning of v1 observations.

**Alternatives considered**:

- Classify every truncated tree as indeterminate even with a positive match: overly conservative;
  observed positive evidence remains valid despite unseen entries.
- Recursively fetch every subtree after truncation: potentially expensive and rate-limit intensive;
  defer to a later policy version if pilot data shows material missingness.
- Treat a truncated tree with no match as no tests detected: rejected as invalid missing-to-negative
  conversion.

## Decision 4: Version A Conservative Path Policy

**Decision**: Define `project-test-presence/v1` as a case-insensitive, path-only policy with three
positive evidence categories:

1. A path segment exactly equal to `test` or `tests`.
2. A `.csproj`, `.fsproj`, or `.vbproj` filename whose stem contains a bounded `test` or `tests`
   token, such as `Widget.Tests.csproj`.
3. A C#, F#, or Visual Basic source filename ending in a bounded `Test` or `Tests` suffix, such as
   `ParserTests.cs`.

Simple substrings such as `contest`, workflow names, documentation prose, and commit messages do not
count. The first deterministic matching path and evidence category are retained.

**Rationale**: The command is explicitly a likely-test-presence screen, not semantic test discovery.
Boundary-aware indicators retain the current low-cost tree approach while reducing obvious false
positives from `Contains("test")`. Naming the policy prevents later heuristic improvements from
silently changing eligibility semantics.

**Alternatives considered**:

- Search for `test` anywhere in a path: rejected because unrelated names such as `contest` become
  positives.
- Download and inspect all project files for test SDK/package references: more precise but adds many
  requests and content parsing; this belongs in a future policy comparison.
- Use only directory names: too many conventional `Foo.Tests.csproj` repositories have no exact
  `tests` directory segment.

## Decision 5: Add Derived Target Manifest Schema 2

**Decision**: Preserve source target manifests as schema version 1. Add target-compatible schema
version 2 for derived subsets. Version 2 replaces the source-import rejection reference with a
`derivation` block containing parent manifest identity, category, policy, and project-check report
reference. It allows an empty target list.

**Rationale**: Reusing schema 1's `rejections` field for a classification report would be
semantically false, while emitting a non-target list would force conversion and lose provenance.
Explicit schema evolution keeps existing manifests valid and lets downstream readers distinguish
source and derived provenance.

**Alternatives considered**:

- Emit schema 1 and point `rejections` at the check report: rejected because the field would lie
  about artifact meaning.
- Define an unrelated project-list schema: rejected because downstream workflows would need another
  conversion path.
- Forbid empty outputs: rejected because file absence makes automation and target accounting
  conditional.

## Decision 6: Publish Immutable Members, Pointer Last

**Decision**: Serialize the report first, hash it, and include its content-addressed reference in both
derived manifests. Hash each derived manifest, publish all three immutable members, then atomically
replace a stable bundle-pointer YAML that contains all filenames and hashes. A bundle is complete
only when the pointer exists and validates.

**Rationale**: Atomic replacement is available for one file, not a set of independent paths. The
manifest-last pattern already used by target creation generalizes cleanly: an old pointer continues
to reference a complete old bundle if a new run fails, and a new pointer cannot reference members
that were not published and verified.

**Alternatives considered**:

- Overwrite three stable paths independently: rejected because readers can observe a mixture of old
  and new artifacts.
- Rename an entire directory: replacement semantics vary across platforms and open-handle states.
- Store everything in one YAML file: simpler atomicity but fails the requirement that categorized
  target manifests be directly reusable.

## Decision 7: Preserve CLI Configuration Convenience

**Decision**: Make `--file/-f` the explicit primary input, with `--output`, `--max-concurrency`, and
`--policy` options. Retain `--config/-c` as an optional source of the target path and concurrency when
direct options are absent. Direct options take precedence. Regardless of invocation style, the
resolved input must be YAML and pass strict target-manifest validation.

**Rationale**: This aligns the command with `targets verify` while preserving existing scripted
configuration entry points. Compatibility applies to CLI ergonomics, not to the scientifically unsafe
legacy target data format.

**Alternatives considered**:

- Require only `--config`: hides the central manifest artifact and makes a simple screening command
  depend on unrelated experiment configuration.
- Remove `--config` immediately: unnecessary disruption for existing automation.

## Decision 8: Distinguish Completion From Success

**Decision**: Use exit code `0` when every target is determinate, `2` when a valid bundle is published
but one or more targets are indeterminate, and `1` for invalid input, failed coordination,
publication failure, or an unusable bundle. The console summary reports all category totals and the
bundle pointer path.

**Rationale**: Researchers need both machine-readable automation and retained failure evidence. An
incomplete classification is not equivalent to a fatal command failure, but it must not look like a
fully screened sampling frame.

**Alternatives considered**:

- Return zero whenever files are written: rejected because automation could proceed with unresolved
  targets.
- Return one for indeterminate targets: conflates a valid auditable partial result with invalid input
  or failed publication.

## Decision 9: Keep The Feature File-Based

**Decision**: Do not add database entities or migrations. Check observations live in the immutable
YAML report and are joined to later outputs through target ID, repository revision, and manifest hash.

**Rationale**: Screening precedes repository materialization and database creation. Requiring one
database per target would recreate the per-project coordination problem and make the sampling-frame
artifact harder to inspect independently.

**Alternatives considered**:

- Persist observations in each project database: impossible for unavailable targets and fragmented
  across the sampling frame.
- Add a global screening database: unnecessary operational state when the versioned report already
  provides a complete canonical census.
