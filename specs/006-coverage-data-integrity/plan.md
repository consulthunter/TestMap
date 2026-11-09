# Implementation Plan: Coverage Data Integrity

**Branch**: `006-coverage-data-integrity` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-coverage-data-integrity/spec.md`

## Summary

Correct coverage collection and persistence with the smallest coherent change to the existing
pipeline. The validation runner will try the existing collectors in fallback order, carry the exact
current-run artifact list into merge, deduplicate only equal content within the same scope, and write
one small status sidecar. The existing coverage report, object coverage, and member coverage tables
will be evolved in place: the report stores final status and sidecar JSON, source IDs become nullable,
raw names and mapping outcomes are added to the existing child rows, and the already-present counter
columns are populated. `MapCoverageService` will persist raw rows before assigning source IDs, retain
constructors, and update the report's usable-coverage flag and reconciliation counts. Existing mapped
consumers continue using the same tables and require a non-null source ID plus the corrected policy.

No new coverage subsystem, service layer, repository family, database table, package dependency, or
standalone export pipeline is introduced.

## Technical Context

**Language/Version**: C# on .NET 10; Python 3.12 for the container validation runner

**Primary Dependencies**: Existing `System.Xml.Serialization`, Entity Framework Core 10.0.9,
SQLite, Python standard library, existing Docker validation runner

**Storage**: Existing SQLite `coverage_reports`, `object_coverages`, and `member_coverages` tables;
one `coverage/collection_<run-id>.json` runner sidecar; existing project-validation CSV

**Testing**: xUnit unit and migration/integration tests; Python `unittest` runner tests; two pinned
repository canaries followed by the 344-repository validation cohort

**Target Platform**: Windows host orchestration and Linux/Windows validation containers

**Project Type**: .NET command-line research tool with a Python container runner

**Performance Goals**: Coverage persistence and mapping remain linear in parsed class/member count;
hashing each current-run artifact once adds no material time relative to test execution and report
merge

**Constraints**: Reuse existing models, entities, repositories, mapper, collector, runner, and CSV
writer; no new tables or injected services; no new package; raw rows must be committed before source
attribution; missingness must not become zero; mapped-only consumers must not ingest null source IDs

**Scale/Scope**: 344 selected validation repositories; 245,806 previously persisted class/member
coverage rows; multi-project and multi-target runs with repeated Cobertura filenames

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **Scientific purpose**: The change protects coverage-availability, coverage-count, candidate,
  risk-scoring, and generated-test impact claims from false success, lost reports, silent mapping
  drops, absent counters, and constructor exclusion.
- **Units and scope**: A provider attempt and artifact belong to one collection run. A coverage report
  belongs to one project/run. Object and member rows are raw parsed observations within that report;
  nullable source IDs add attribution without changing observation grain. Repository CSV values are
  explicit aggregations of the latest corrected report.
- **Eligibility and pairing**: Repository selection and source revisions do not change. Candidate,
  gap, risk, and before/after consumers use only corrected reports with usable mapped member rows.
  Constructors are retained as coverage but remain outside the existing method-only candidate rule.
- **Provenance**: The report retains run ID, project/repository/commit through its existing owner,
  collector attempt order, return codes, artifact paths and hashes, merge decisions, collection
  status, and `coverage-integrity-v1` policy. Existing test-run links preserve baseline/post pairing.
- **Lane fairness and isolation**: Both baseline and targeted test commands use the same fallback,
  merge, and status contract. Test outcome remains separate from coverage outcome. Existing retries,
  budgets, workspace cleanup, and lane ordering do not change.
- **Missingness and failures**: Missing artifacts, parse failures, no usable mappings, absent counters,
  ambiguous matches, and legacy rows are explicit. Availability flags distinguish observed zero from
  unavailable counters. No empty placeholder makes `HasCoverage` true.
- **Verification**: Focused runner, parser, mapping, repository, writer, consumer, and migration tests
  cover each defect. Integration tests prove raw-first persistence and schema compatibility. Pinned
  failure/success canaries precede the complete 344-repository recollection and audit.
- **Compatibility**: Historical rows remain queryable but are marked legacy/not measured for the new
  policy and do not satisfy corrected coverage eligibility. The project-validation CSV gains explicit
  coverage status and reconciliation columns. Corrected coverage-dependent analysis requires rerun;
  unaffected static-analysis evidence may be reused at the same pinned commit.

### Post-Design Re-check

- The design adds no new database evidence table or independently injected subsystem: it enriches
  the three existing coverage grains and uses one runner-to-collector sidecar for data already
  produced by the runner.
- The only schema expansion is required to represent nullable attribution, raw identity, counter
  availability, final status, policy, and reconciliation. Removing any of these fields would restore
  one of the identified silent-loss or missing-as-zero defects.
- Existing inner-join consumers remain mapped-only by requiring non-null source IDs and a corrected,
  usable report. No unmatched observation can enter candidate selection or metric comparison.
- The gate remains passed; no constitution exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/006-coverage-data-integrity/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── coverage-integrity-contract.md
└── tasks.md                         # created by speckit-tasks, not this command
```

### Source Code (repository root)

```text
TestMap/
├── Docker/validation/runner/
│   ├── src/testmap_runner/cli.py
│   └── test/test_cli.py
├── Models/Coverage/
│   ├── CoverageReportModel.cs
│   ├── ObjectCoverageModel.cs
│   └── MemberCoverageModel.cs
├── Services/TestExecution/
│   ├── BuildTestResultCollector.cs
│   ├── Collection/
│   │   ├── CollectCoverageResultsService.cs
│   │   └── CollectTestsResultWriter.cs
│   └── Mapping/MapCoverageService.cs
├── Persistence/Ef/
│   ├── Entities/Coverage/
│   ├── Configuration/Entities/Coverage/
│   ├── Mapping/Coverage/
│   ├── Repositories/Coverage/
│   └── TestMapDbContext.cs
└── Migrations/
    └── TestMapDbContextModelSnapshot.cs

TestMap.UnitTests/
├── TestExecution/
│   ├── Collection/CollectCoverageResultsServiceTests.cs
│   ├── CollectTestsResultWriterTests.cs
│   └── MapCoverageServiceTests.cs
├── Persistence/CoverageReportRepositoryTests.cs
├── RiskScoring/
└── TestGeneration/
```

**Structure Decision**: Modify the current coverage pipeline at its existing boundaries. A small
pure counter helper may be placed beside the existing coverage mapping extensions if sharing the
line/branch calculation avoids duplication; it must remain a non-injected implementation helper,
not a service or subsystem.

## Complexity Tracking

No constitution violations or additional architectural layers are required.
