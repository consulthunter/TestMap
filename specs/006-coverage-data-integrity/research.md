# Research: Coverage Data Integrity

## Decision 1: Evolve Existing Coverage Tables In Place

**Decision**: Add the required status, raw identity, availability, and attribution fields to
`coverage_reports`, `object_coverages`, and `member_coverages`. Make `ObjectId` and `MemberId`
nullable. Do not create raw-report, provider-attempt, artifact, or attribution tables.

**Rationale**: These three tables already represent exactly the required report, class observation,
and member observation grains, and already contain the counter columns. Treating each child row as
the parsed observation and its nullable source ID as attribution preserves the current query model
while making unmatched evidence durable.

**Alternatives considered**:

- Add a parallel raw-coverage schema: rejected because it duplicates the existing observation grain,
  requires synchronization and new repositories, and exceeds the user's minimal-change constraint.
- Store unmatched entries only in logs: rejected because logs cannot support reconciliation,
  remapping, or corrected database exports.
- Use source ID `0` for unmatched rows: rejected because it invents a source identity and conflicts
  with the explicit nullable-attribution requirement.

## Decision 2: Use the Existing Report Row as the Collection Outcome

**Decision**: A `CoverageReportModel` is returned and persisted for every collection attempt,
including missing-artifact and parse-failure outcomes. Add run identity, final status/reason,
successful collector, policy version, usable-coverage flag, counter availability, summary counts, and
one JSON metadata field to the existing report model/entity. `HasCoverage` is derived from the
corrected report's usable flag, never report-row existence.

**Rationale**: The existing collector, mapper, test-run linker, repository, and project-validation
writer all already pass a coverage report. Enriching that object keeps the current call graph and
allows failed attempts to remain first-class without introducing a separate result subsystem.

**Alternatives considered**:

- Return `null` for every failure: rejected because the failure would again disappear before
  persistence.
- Introduce a new collection-result service hierarchy: rejected because a few explicit properties on
  the existing result are sufficient.
- Query for any report row when writing `HasCoverage`: rejected because that is the current false
  positive.

## Decision 3: Add One Runner Status Sidecar, Stored Verbatim on the Report

**Decision**: The Python runner writes `coverage/collection_<run-id>.json` for every completed
coverage command. It records provider order, return codes, TRX and current-run coverage artifacts,
artifact hashes/scopes, duplicate decisions, merge outcome, and final reason. The C# collector reads
the sidecar and stores its JSON verbatim on the report while promoting only the final fields needed
for normal queries and CSV output.

**Rationale**: The runner is the only component that knows which providers were attempted and which
files belong to the current command. One sidecar crosses that existing process boundary without new
database tables or host orchestration APIs. Keeping the original JSON retains audit detail while
promoted fields keep routine queries simple.

**Alternatives considered**:

- Parse human-readable Docker logs: rejected because message wording is not a stable data contract.
- Add provider-attempt and artifact tables: rejected as unnecessary for the immediate fixes; the
  sidecar JSON preserves the evidence at attempt scope.
- Infer provider failure from missing XML: rejected because it cannot distinguish unavailable,
  failed, no-artifact, and merge-failure outcomes.

## Decision 4: Carry Current-Run Artifact Paths into Merge

**Decision**: Extend the runner's existing command result with the artifacts observed after each
collector invocation and pass that accumulated list directly to merge. Group duplicates by content
hash and collection scope, not filename. Do not rescan the coverage directory during merge.

**Rationale**: The runner already timestamps and counts recent files. Returning the matching paths is
the smallest change that prevents both stale-file contamination and same-filename loss. Hashing uses
the Python standard library and each file is read once.

**Alternatives considered**:

- Keep recursive directory scanning and filter by modification time later: rejected because
  filesystem timestamp granularity and merged outputs make attempt ownership ambiguous.
- Rename every produced report: rejected because report locations are controlled by test tooling and
  project layout.
- Deduplicate only by content hash: rejected because identical bytes from distinct project/framework
  scopes are separate observations under the specification.

## Decision 5: Continue Fallback After an Unsuccessful Preferred Attempt

**Decision**: Build collector order from the requested preferred collector followed by the remaining
built-in collectors in the existing default order. Continue to the next collector whenever the
current attempt produces no current-run coverage artifact, even if its command return code is
nonzero. Stop once usable artifacts exist. Preserve every attempt and keep test command outcome
separate from final coverage availability.

**Rationale**: The current early return is the direct cause of missing fallback. Reusing the current
loop and collector strings avoids a new policy component or CLI mode.

**Alternatives considered**:

- Fall back only on return code zero: rejected because unavailable collectors commonly return
  nonzero before producing coverage.
- Always run both collectors: rejected because it doubles test execution when the preferred provider
  already succeeds and complicates merge semantics.
- Add a new provider-only option now: deferred because no current workflow requires it and it would
  expand the public CLI without fixing the reported bug.

## Decision 6: Persist Parsed Rows, Then Attribute Them in the Existing Mapper

**Decision**: `MapCoverageService` first persists the report and all object/member rows with raw
identity, source ordinals, counter values, `Pending` attribution, and null source IDs. After that save
succeeds, it runs the current object/member matching logic, updates those same rows with source IDs
and terminal statuses, creates gaps only for mapped members, updates report reconciliation/status,
and saves again.

**Rationale**: This preserves the current mapper and matching helpers but changes their control flow
from filter-before-insert to insert-before-match. A committed first save makes observations available
even if later attribution fails.

**Alternatives considered**:

- Keep inserting only mapped rows: rejected because it is the silent-drop defect.
- Create a separate remapping worker: rejected because attribution is already synchronous and the
  current service can update persisted rows directly.
- Persist the full Cobertura XML as the only raw evidence: rejected because it cannot be queried or
  reconciled at class/member grain without reparsing and still leaves existing rows incomplete.

## Decision 7: Populate Existing Counters with One Shared Pure Calculation

**Decision**: Compute line counts from distinct line numbers and hits. Compute branch counts from
Cobertura condition fractions, falling back to child condition percentages where necessary. Reuse
one small pure helper from the existing mapping extensions for class and member rows. Add line- and
branch-count availability flags; legacy flags default false.

**Rationale**: The database columns already exist, but current mappings omit them. A pure helper is
enough to centralize parsing and is not a subsystem. Availability flags preserve the difference
between observed zero and unavailable historical or malformed detail without making all consumers
nullable-aware.

**Alternatives considered**:

- Derive counts from rates alone: rejected because rounding prevents recovering exact integers.
- Make all eight existing counter columns nullable: rejected because it creates broad arithmetic and
  query churn; paired availability flags preserve semantics with fewer consumer changes.
- Leave zero as the default for missing values: rejected by the constitution and the reported defect.

## Decision 8: Match Constructors Through Existing Member Logic

**Decision**: Remove the unconditional `.ctor`/`.cctor` exclusion. Normalize those names without
renaming them, require compatible persisted kinds (`constructor` or `static_constructor`), and reuse
the existing line-overlap and parameter-count disambiguation. Ambiguous constructors remain
unmatched with a reason.

**Rationale**: Static analysis already stores constructor symbols as `.ctor`/`.cctor` and distinct
member kinds. The current exclusion is the only structural barrier; existing overload safeguards can
handle constructors with a small compatibility extension.

**Alternatives considered**:

- Map every constructor to its containing type: rejected because overload attribution would be lost.
- Rename coverage constructors to the class name: rejected because it would diverge from persisted
  symbol identity.
- Add a constructor-specific mapper: rejected because the existing member matcher already has the
  required file, line, name, and parameter evidence.

## Decision 9: Keep Downstream Consumers Mapped-Only and Policy-Aware

**Decision**: Update current coverage queries to require a non-null source ID and a report marked
usable under `coverage-integrity-v1`. Continue using their existing joins, ordering, and method-only
filters. Extend the existing project-validation CSV with status, reason, policy, and four raw/mapped
reconciliation counts rather than creating a new export.

**Rationale**: Nullable IDs naturally exclude unmatched evidence from mapped inner joins, but
explicit predicates make the rule reviewable and prevent legacy reports from being selected as the
latest corrected evidence. Appending fields to the current validation row keeps the blast radius
small.

**Alternatives considered**:

- Let raw unmatched rows enter risk or candidate scoring: rejected because they do not identify a
  source member.
- Add a new coverage audit CSV: rejected because current repository validation already has the
  correct repository grain and only needs explicit coverage fields.
- Backfill legacy zero counters as observed: rejected because exact values cannot be recovered from
  the stored rates.

## Decision 10: Recollect, Do Not Fabricate, Corrected Historical Coverage

**Decision**: The migration marks historical reports and counters as legacy/not measured for the new
policy. Validate first on pinned `microsoft/artifacts-credprovider` and `nbuilder/nbuilder`, then
recollect all 344 selected repositories at their pinned commits. Reuse static-analysis data only when
its target identity and commit remain exact.

**Rationale**: Existing databases lack dropped observations, constructor rows, and exact entity
counters, so a trustworthy in-place backfill is impossible. A full selected-cohort rerun restores
comparability between the 147 false-success repositories and the 197 partially persisted ones.

**Alternatives considered**:

- Rerun only the 147 repositories without usable rows: rejected because the remaining 197 also lost
  counters, constructors, unmatched entries, or same-name reports.
- Estimate counters from rates: rejected because the denominators are unknown.
- Treat historical rows as corrected after migration: rejected because a schema label cannot create
  missing evidence.
