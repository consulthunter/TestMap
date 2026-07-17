# Research: Pinned Repository Targets

## Structured Delimited Input

**Decision**: Use CsvHelper 33.1.0 with invariant culture, quote-aware parsing, delimiter candidates
restricted to comma and tab, and a TestMap validation layer that requires one unambiguous parse with
`name` and `lastCommitSHA` headers.

**Rationale**: The example file is tab-delimited despite its `.csv` suffix and contains large JSON
and semicolon-rich ignored columns. A mature parser handles quoting, newlines, and record boundaries;
the application layer can enforce the narrower scientific contract and fail ambiguous detection.
CsvHelper supports configurable delimiter detection and is compatible with the current runtime.

**Alternatives considered**:

- Manual splitting: rejected because quoted delimiters and embedded data would corrupt row identity.
- Extension-based delimiter choice: rejected because the supplied `.csv` is tab-delimited.
- Tab-only support: rejected because the clarified contract also requires comma input.
- Require an override every time: rejected as unnecessary friction; an override remains available.

Reference: <https://joshclose.github.io/CsvHelper/getting-started/>

## YAML Manifest Serialization

**Decision**: Use YamlDotNet 18.1.0 with typed records, explicit snake-case names, strict validation,
and deterministic ordering performed before serialization.

**Rationale**: TestMap has no YAML support today. YamlDotNet provides a maintained object serializer
and parser rather than requiring ad hoc text generation. Determinism comes from TestMap's canonical
ordering and normalized values, not from round-tripping arbitrary user formatting.

**Alternatives considered**:

- JSON manifest: rejected because the feature contract explicitly adopts researcher-readable YAML.
- Handwritten YAML: rejected because escaping, scalar typing, and schema evolution are error-prone.
- Preserve arbitrary YAML comments/layout: deferred; the manifest is generated scientific input,
  not a general-purpose editor round trip.

Reference: <https://www.nuget.org/packages/YamlDotNet/>

## Repository Revision Materialization

**Decision**: Continue using LibGit2Sharp, but replace clone-only behavior with an exact-revision
materialization service. Successful workspaces use detached `HEAD` at the requested commit. Existing
repositories must match origin and be clean before TestMap performs destructive operations.

**Rationale**: LibGit2Sharp is already a core dependency and supports clone, fetch, commit lookup,
checkout, reset, status, and remote inspection. Detached `HEAD` represents an exact commit without a
mutable branch reference. The current service only records incidental `HEAD` and swallows failures,
which is incompatible with scientific provenance.

**Alternatives considered**:

- Shell out to the Git CLI: rejected for the first version because it duplicates an existing
  dependency and complicates cross-platform error classification.
- Create a local branch per target: rejected because branch tips are mutable and agents could commit
  onto them without immediately violating revision identity.
- Shallow clone: rejected as a default because historical requested commits may be omitted.
- Accept any locally present commit: rejected for verification when remote availability cannot be
  established; local-only objects are not independently reproducible.

References: <https://git-scm.com/docs/git-checkout>,
<https://libgit2.org/docs/reference/main/checkout/git_checkout_strategy_t.html>

## Revision Isolation Strategy

**Decision**: Use one workspace, SQLite database, and artifact root per full repository commit.
Do not add revision foreign keys to every existing source graph table in this feature.

**Rationale**: The current source model is repository-scoped and many entities lack revision keys.
Retrofitting revision identity through every table would be broad and high risk. Filesystem and
database isolation makes accidental cross-revision joins structurally difficult while retaining the
current repository model inside each database.

**Alternatives considered**:

- Shared database with `repository_revision_id` on all evidence tables: rejected for the initial
  feature due to migration breadth and high risk of one omitted filter invalidating results.
- Reuse one repository-name workspace sequentially: rejected because crashes and concurrent runs can
  expose stale files and because artifacts collide.
- Short commit path segment: rejected because full SHA avoids prefix collisions and is the declared
  target identity.

## Manifest and Target Fingerprints

**Decision**: Hash source input bytes for `source.sha256`, hash final manifest bytes for run
`target_manifest_sha256`, and compute target IDs from normalized repository identity plus commit.

**Rationale**: These hashes answer different provenance questions: what source dataset was imported,
which exact manifest governed a run, and which immutable target a row describes. Keeping them
separate prevents a regenerated manifest timestamp from being confused with target identity.

**Alternatives considered**:

- One hash for everything: rejected because source, manifest, and target have different lifecycles.
- Hash YAML object graphs: rejected because serializer changes could create unclear equivalence.
- Use repository name as target ID: rejected because multiple commits would collide.

## Target Verification Semantics

**Decision**: Verification emits one ordered status row per manifest target and uses bounded
concurrency. Target-level failures do not abort the complete report. Manifest/schema failure or an
incomplete report write returns a command failure.

**Rationale**: Unavailable targets are sampling-frame outcomes. Aborting on the first unavailable
repository would hide later outcomes; treating an invalid manifest as a target-level failure would
produce unreliable partial reports.

**Alternatives considered**:

- Drop unavailable targets: rejected as sampling bias.
- Fail on first target error: rejected because it prevents a complete availability census.
- Unlimited concurrency: rejected due to API/network pressure and rate-limit distortion.

## Workspace Integrity Evidence

**Decision**: Centralize checks in one service and persist checkpoint observations keyed by run,
lane, work item, and attempt number. Restore explicitly targets the immutable base commit.

**Rationale**: Checks spread through orchestration code would drift between lanes. Persisted
observations make failures auditable and allow result publication to prove the workspace state that
was measured. Resetting to `HEAD` is unsafe because an agent can move `HEAD`.

**Alternatives considered**:

- Log checks only: rejected because logs are difficult to join and audit systematically.
- One check at experiment start: rejected because agents operate after that boundary.
- Reject every dirty tree: rejected because generated tests are expected working-tree changes during
  post-attempt analysis.

## Result and Compatibility Policy

**Decision**: Introduce provenance policy `pinned-target-v1` and canonical result schema `3.0`.
Do not migrate historical result rows into compliance.

**Rationale**: Required target and integrity fields change the evidentiary meaning of a successful
row. A major schema change prevents downstream notebooks from assuming fields that were never
observed historically.

**Alternatives considered**:

- Keep schema `2.0` with nullable columns: rejected because empty fields would blur legacy and valid
  missingness.
- Infer commits from cached project metadata: rejected because the feature exists to replace that
  unverified behavior.
- Disallow all legacy analysis: rejected; legacy artifacts remain usable for explicitly labeled
  historical or sensitivity analyses.
