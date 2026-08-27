# Coverage Integrity Contract

## Scope

This contract covers the runner-to-collector status sidecar, corrected database semantics, and the
existing project-validation CSV fields. It introduces no public command and no new export file.

## 1. Runner Status Sidecar

### Path

```text
coverage/collection_<run-id>.json
```

The sidecar belongs to exactly one runner invocation and is written even when no coverage XML is
produced. Paths inside the document are relative to the mounted project root when possible.

### Schema

```json
{
  "schemaVersion": "coverage-collection-v1",
  "runId": "baseline_example",
  "status": "Merged",
  "reason": "",
  "testReturnCode": 0,
  "successfulCollector": "Code Coverage;Format=Cobertura",
  "providerAttempts": [
    {
      "order": 1,
      "collector": "XPlat Code Coverage",
      "returnCode": 1,
      "trxFiles": [],
      "coverageArtifacts": []
    },
    {
      "order": 2,
      "collector": "Code Coverage;Format=Cobertura",
      "returnCode": 0,
      "trxFiles": ["coverage/example.trx"],
      "coverageArtifacts": [
        {
          "path": "coverage/abc/coverage.cobertura.xml",
          "sha256": "64-lowercase-hex",
          "target": "tests/Example.Tests/Example.Tests.csproj",
          "framework": "net10.0"
        }
      ]
    }
  ],
  "merge": {
    "status": "Merged",
    "inputArtifacts": ["coverage/abc/coverage.cobertura.xml"],
    "duplicateArtifacts": [],
    "rawOutput": "coverage/merged_baseline_example_raw.cobertura.xml",
    "normalizedOutput": "coverage/merged_baseline_example.cobertura.xml"
  }
}
```

### Rules

- `schemaVersion` and `runId` are required.
- Provider attempts are ordered and include every attempted collector.
- A nonzero provider return code does not stop fallback when that attempt produced no current-run
  coverage artifact.
- Artifact identity is `(sha256, normalized target, framework)`. Filename is not identity.
- Merge inputs come only from `coverageArtifacts` recorded by the current invocation.
- An artifact listed as duplicate must reference an earlier artifact with the same identity.
- The runner updates the sidecar atomically at terminal boundaries so a reader sees either the prior
  valid document or the complete new document.
- If the runner terminates before a valid sidecar can be written, the collector synthesizes a
  `CollectionFailed` metadata document with the known run ID and missing-sidecar reason.

## 2. Collection Status Vocabulary

| Status | Meaning | `HasUsableCoverage` |
|---|---|---|
| `ProviderUnavailable` | No attempted provider could start successfully | false |
| `CollectionFailed` | Provider execution failed without a usable artifact and no fallback succeeded | false |
| `NoArtifact` | Provider execution completed but produced no current-run coverage artifact | false |
| `MergeFailed` | Current-run artifacts existed but could not be merged | false |
| `ParseFailed` | Selected raw/normalized output could not be parsed | false |
| `ParsedNoData` | Parsed report contained no class/member observations | false |
| `PendingAttribution` | Raw observations are durable but attribution did not reach a terminal state | false |
| `ParsedNoUsableCoverage` | All observations reached terminal non-mapped outcomes | false |
| `PartiallyMapped` | At least one usable in-project observation mapped and at least one did not | true |
| `Mapped` | Usable in-project coverage mapped without unexplained loss | true |
| `LegacyNotMeasured` | Historical row predates `coverage-integrity-v1` | false for corrected analysis |

`HasCoverage` in project validation equals `HasUsableCoverage` from the latest corrected report for
the relevant project/run. Report existence is never sufficient.

## 3. Attribution Vocabulary

### Object observations

`Pending`, `Mapped`, `Unmatched`, `Ambiguous`, `OutOfProject`, `Unsupported`.

### Member observations

`Pending`, `Mapped`, `ParentUnmatched`, `Unmatched`, `Ambiguous`, `Unsupported`.

Terminal non-mapped statuses require a reason. `Mapped` requires a non-null source ID. Every raw row
must have exactly one terminal status before a report can be `Mapped`, `PartiallyMapped`, or
`ParsedNoUsableCoverage`.

## 4. Reconciliation Contract

For each corrected report:

```text
RawObjectCount  = count(object_coverages for report)
MappedObjectCount = count(object_coverages where ObjectId is not null and status = Mapped)
RawMemberCount  = count(member_coverages for report)
MappedMemberCount = count(member_coverages where MemberId is not null and status = Mapped)

RawObjectCount = MappedObjectCount + terminal non-mapped object count
RawMemberCount = MappedMemberCount + terminal non-mapped member count
```

Pending rows are allowed only while report status is `PendingAttribution`. A publication-ready audit
fails if summary and child counts disagree, a terminal row lacks a reason, a mapped row lacks an ID,
or a null-ID row reaches a mapped consumer.

## 5. Project-Validation CSV

The existing CSV appends these columns:

```text
CoverageStatus
CoverageReason
CoveragePolicyVersion
RawCoverageObjectCount
MappedCoverageObjectCount
RawCoverageMemberCount
MappedCoverageMemberCount
```

Existing columns retain their order and meaning except that `HasCoverage` now uses the corrected
definition above. CSV empty values mean unavailable text; integer counts are emitted only for a
corrected parsed report. Legacy reports emit `LegacyNotMeasured`, an empty policy, and empty
reconciliation counts rather than zeros.

## 6. Mapped Consumer Contract

Candidate selection, coverage-gap evidence, risk scoring, generation evidence, and before/after
comparison may consume a coverage row only when:

```text
report.MeasurementPolicyVersion = coverage-integrity-v1
report.HasUsableCoverage = true
memberCoverage.MemberId is not null
memberCoverage.AttributionStatus = Mapped
```

Object-only summaries apply the equivalent object rule. The existing `member.Kind == "method"`
candidate rule remains unchanged, so constructor coverage is retained but does not enter the
method-only candidate pool.

## 7. Compatibility

- Historical tables, rates, and source IDs remain readable.
- Historical zero counters are unavailable unless independently supported by retained source
  artifacts; migration does not claim they were observed.
- Corrected and historical policies must not be combined without an explicit legacy analysis path.
- Corrected validation requires recollection at the pinned repository commit.
- Mutation evidence and static-analysis evidence are not rewritten by this contract.
