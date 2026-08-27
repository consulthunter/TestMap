# Data Model: Coverage Data Integrity

## Minimal-Change Overview

No new database tables are introduced. Existing rows take on explicit raw-observation and collection
semantics.

| Existing entity | Grain after change | Purpose |
|---|---|---|
| Coverage Report | One project coverage collection for one run | Stores collection/merge outcome, policy, usable status, root counters, reconciliation, and runner metadata |
| Object Coverage | One parsed Cobertura class occurrence in one report | Retains raw class identity and measurements whether or not a source object is found |
| Member Coverage | One parsed Cobertura method occurrence within one raw class | Retains raw member identity and measurements whether or not a source member is found |
| Coverage Gap | One mapped uncovered/partial-branch line | Remains mapped-only and unchanged; created only when `MemberId` is known |

The runner sidecar is an attempt-scoped file contract, not a database entity. Its original JSON is
stored on the owning coverage report.

## Coverage Report

### Existing fields retained

`Id`, `ProjectId`, `TestRunId`, line/branch rates, complexity, Cobertura version/timestamp, root
covered/valid counters, and `CreatedAt` retain their existing roles.

### Added fields

| Field | Type | Rules |
|---|---|---|
| RunId | string | Required for corrected rows; identifies the host/runner collection and replaces timestamp-only lookup |
| CollectionStatus | string | Required stable vocabulary from the state model below |
| CollectionReason | string | Required when status is not a usable terminal status; sanitized and concise |
| SuccessfulCollector | string | Collector that produced the retained artifact; empty when none succeeded |
| CollectionMetadataJson | string | Verbatim `coverage-collection-v1` sidecar or a synthesized failure document when the sidecar is unavailable |
| HasUsableCoverage | boolean | True only after at least one in-project object or member observation is mapped under the corrected policy |
| LineCountsAvailable | boolean | Root line counters are observed, not inferred from legacy defaults |
| BranchCountsAvailable | boolean | Root branch counters are observed, not inferred from legacy defaults |
| MeasurementPolicyVersion | string | `coverage-integrity-v1` for corrected rows; empty for legacy rows |
| RawObjectCount | integer | Number of parsed object observations |
| MappedObjectCount | integer | Raw object observations with non-null `ObjectId` and `Mapped` status |
| RawMemberCount | integer | Number of parsed member observations |
| MappedMemberCount | integer | Raw member observations with non-null `MemberId` and `Mapped` status |

### Validation

- Corrected report identity is unique by `(ProjectId, RunId)`.
- All summary counts are non-negative.
- `MappedObjectCount <= RawObjectCount` and `MappedMemberCount <= RawMemberCount`.
- `HasUsableCoverage` is true exactly when the corrected report has at least one mapped in-project
  object or member observation with valid parsed measurement data.
- A non-usable status requires `HasUsableCoverage = false`.
- Counter integers are interpreted only when the corresponding availability flag is true.
- The child rows, not summary fields, are authoritative; summaries must reconcile before completion.

## Object Coverage

### Existing fields retained

`Id`, `CoverageReportId`, rates, covered/valid counters, and complexity remain on the same row.

### Changed and added fields

| Field | Type | Rules |
|---|---|---|
| ObjectId | integer, nullable | Source attribution; null for pending, unmatched, ambiguous, or out-of-project observations |
| SourceOrdinal | integer | Stable zero-based parsed class occurrence order within the report |
| PackageName | string | Raw Cobertura package name, including empty when supplied empty |
| Name | string | Raw class name exactly as parsed |
| Filename | string | Raw source filename exactly as parsed |
| AttributionStatus | string | `Pending`, `Mapped`, `Unmatched`, `Ambiguous`, `OutOfProject`, or `Unsupported` |
| AttributionReason | string | Required unless status is `Mapped` or `Pending` |
| LineCountsAvailable | boolean | Existing line counter values are observed from line details |
| BranchCountsAvailable | boolean | Existing branch counter values are observed from condition details or an observed no-branch set |

### Identity and validation

- `(CoverageReportId, SourceOrdinal)` is unique.
- `Mapped` requires non-null `ObjectId`; every other terminal status requires null `ObjectId`.
- Raw identity is retained for every parsed class, including external/generated classes.
- Counter availability controls whether zero means observed zero or unavailable.

## Member Coverage

### Existing fields retained

`Id`, `CoverageReportId`, rates, covered/valid counters, and complexity remain on the same row.

### Changed and added fields

| Field | Type | Rules |
|---|---|---|
| MemberId | integer, nullable | Source attribution; null for pending or any non-match outcome |
| ObjectCoverageId | integer? | Nullable for migrated legacy rows; required for corrected raw member observations |
| SourceOrdinal | integer | Stable zero-based parsed member occurrence order within its raw object |
| Name | string | Raw member name exactly as parsed, including `.ctor` and `.cctor` |
| Signature | string | Raw Cobertura signature exactly as parsed |
| AttributionStatus | string | `Pending`, `Mapped`, `ParentUnmatched`, `Unmatched`, `Ambiguous`, or `Unsupported` |
| AttributionReason | string | Required unless status is `Mapped` or `Pending` |
| LineCountsAvailable | boolean | Existing line counter values are observed from line details |
| BranchCountsAvailable | boolean | Existing branch counter values are observed from condition details or an observed no-branch set |

### Identity and validation

- `(ObjectCoverageId, SourceOrdinal)` is unique.
- `Mapped` requires non-null `MemberId`; every other terminal status requires null `MemberId`.
- A member under an unmapped object is retained as `ParentUnmatched`, not discarded.
- `.ctor` maps only to source kind `constructor`; `.cctor` maps only to
  `static_constructor`. Line overlap is preferred, then parameter count; unresolved ties are
  `Ambiguous`.
- Coverage gaps may reference only a `Mapped` member row.

## Counter Calculation

For both class and member observations:

- `LinesValid` is the number of distinct observed line numbers.
- `LinesCovered` is the number of those lines with at least one positive hit.
- `BranchesValid` and `BranchesCovered` use the numerator/denominator in each observed Cobertura
  `condition-coverage` value. Child condition percentages are the fallback when the fraction is
  absent.
- Repeated representations of the same line/condition within one observation are not double-counted.
- `0/0` is an observed zero-branch measurement when the line inventory is available.
- Malformed or absent detail sets the relevant availability flag false; stored integer defaults are
  not interpreted as measurements.
- Covered counts must not exceed valid counts. Source rates remain available but must agree with
  exact counts within the documented rounding tolerance when both are available.

## Collection State Model

```text
Collecting
  -> ProviderUnavailable
  -> CollectionFailed
  -> NoArtifact
  -> MergeFailed
  -> ParseFailed
  -> ParsedNoData
  -> PendingAttribution
       -> ParsedNoUsableCoverage
       -> PartiallyMapped
       -> Mapped

Historical row without coverage-integrity-v1
  -> LegacyNotMeasured
```

- `PartiallyMapped` means at least one usable mapping and at least one terminal non-match.
- `Mapped` means all in-scope parsed observations reached terminal mapped outcomes; explicitly
  out-of-project/unsupported observations may remain but must be counted and disclosed.
- An interruption after raw persistence leaves `PendingAttribution` and durable raw child rows.
- A retry for the same `(ProjectId, RunId)` resumes or deterministically replaces rows by source
  ordinal; it must not append duplicates.

## Relationships

```text
Project 1 ── * Coverage Report
Test Run 0..1 ── * Coverage Report
Coverage Report 1 ── * Object Coverage
Object Coverage 1 ── * Member Coverage
Source Object 0..1 ── * Object Coverage
Source Member 0..1 ── * Member Coverage
Mapped Member Coverage 1 ── * Coverage Gap (via report + source member)
```

## Historical Compatibility

The migration:

- makes source IDs nullable without changing existing non-null values;
- marks existing reports `LegacyNotMeasured`, policy empty, `HasUsableCoverage = false`;
- marks line/branch count availability false on historical object/member rows;
- retains historical rates and integer columns for inspection but prevents corrected consumers from
  interpreting zero counters as observed;
- leaves historical mappings queryable for explicit legacy analysis;
- requires recollection for a report to receive `coverage-integrity-v1` and participate in corrected
  coverage-dependent analysis.

No attempt is made to synthesize dropped raw rows, constructor rows, or exact counters from legacy
rates.
