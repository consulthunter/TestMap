# Tasks: Check Project Manifests

**Input**: Design documents from `specs/002-check-project-manifests/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature changes repository screening and cohort eligibility, so tests must
be written before the implementation they protect and must cover exact revision identity,
missingness, artifact grain, provenance, and publication integrity.

**Organization**: Tasks are grouped by user story so each scientific behavior can be implemented and
validated as an independent increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it affects different files and has no dependency on an
  incomplete task in the same phase.
- **[Story]**: Maps the task to a user story from spec.md.
- Every task includes an exact repository path.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish deterministic fixtures and directories before changing the command.

- [X] T001 Create project-check fixture directories and fixture conventions in `TestMap.UnitTests/Fixtures/ProjectChecks/README.md`
- [X] T002 [P] Add valid source-schema-1, derived-schema-2, empty-subset, malformed, duplicate, and legacy-list fixtures in `TestMap.UnitTests/Fixtures/ProjectChecks/Manifests/`
- [X] T003 [P] Add deterministic exact-commit, moved-default-branch, truncated-tree, and provider-failure response fixtures in `TestMap.UnitTests/Fixtures/ProjectChecks/ProviderResponses/`
- [X] T004 [P] Add a minimal configuration fixture whose target path points to pinned YAML in `TestMap.UnitTests/Fixtures/ProjectChecks/check-projects-config.json`

---

## Phase 2: Foundational (Blocking Contracts And Validation)

**Purpose**: Define the shared schema, identity, validation, and serialization boundaries used by
all three stories.

**CRITICAL**: No user-story implementation begins until this phase passes.

### Foundational Tests

- [X] T005 [P] Add failing tests for source schema 1 and derived schema 2 structural invariants, including valid empty derived subsets, in `TestMap.UnitTests/Targets/TargetManifestSerializerTests.cs`
- [X] T006 [P] Add failing tests for project-check observation status, evidence, commit, truncation, and reason invariants in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckModelTests.cs`
- [X] T007 [P] Add failing tests for report count equations, one-record-per-target identity, and bundle artifact hash/count invariants in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckModelTests.cs`
- [X] T008 [P] Add failing tests for distinct input, pointer, and member output paths and basename-only references in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckOutputPathTests.cs`

### Foundational Implementation

- [X] T009 Extend target-manifest models with mutually exclusive schema-1 rejection provenance and schema-2 derivation provenance in `TestMap/Models/Targets/TargetManifest.cs`
- [X] T010 Implement project-check policy, evidence, status, observation, report, summary, artifact-reference, bundle, and command-result models in `TestMap/Models/Targets/ProjectCheckModels.cs`
- [X] T011 Define probe, policy, coordinator, serializer, validator, and publisher interfaces in `TestMap/Services/ProjectDiscovery/Contracts/ProjectCheckContracts.cs`
- [X] T012 Extend strict target serialization and validation to support schema 1 and schema 2 without weakening existing target identity rules in `TestMap/Services/Targets/TargetManifestSerializer.cs`
- [X] T013 Implement cross-record report, observation, partition, and bundle validation in `TestMap/Services/ProjectDiscovery/ProjectCheckContractValidator.cs`
- [X] T014 Implement deterministic YAML serialization and deserialization for project-check reports and bundle pointers in `TestMap/Services/ProjectDiscovery/ProjectCheckSerializer.cs`
- [X] T015 Implement default content-addressed member names, pointer naming, path collision rejection, and input-overwrite protection in `TestMap/Services/ProjectDiscovery/ProjectCheckOutputPathResolver.cs`
- [X] T016 Run and keep green the foundational target and project-check tests in `TestMap.UnitTests/TestMap.UnitTests.csproj`

**Checkpoint**: Both target-manifest schema variants and all project-check records have strict,
test-backed contracts.

---

## Phase 3: User Story 1 - Check Pinned Repository Revisions (Priority: P1) MVP

**Goal**: Classify test-presence evidence from each exact requested commit without reading or
substituting the repository's default branch.

**Independent Test**: Feed two revisions of one repository to the coordinator, with tests present
only in the newer revision while the default branch points at that newer revision. Confirm the older
revision is negative, the newer revision is positive, and both requested/observed commit pairs match.

### Tests For User Story 1

> Write these tests first and confirm they fail before implementing the story.

- [X] T017 [P] [US1] Add policy tests for exact `test`/`tests` segments, bounded test project names, bounded test source names, deterministic first evidence, and false positives such as `contest` in `TestMap.UnitTests/ProjectDiscovery/ProjectTestPresencePolicyTests.cs`
- [X] T018 [P] [US1] Add exact-commit probe tests that assert repository identity lookup, requested commit lookup, commit SHA equality, commit-tree SHA use, and absence of default-branch/content calls in `TestMap.UnitTests/ProjectDiscovery/GitHubProjectTreeProbeTests.cs`
- [X] T019 [P] [US1] Add coordinator tests for bounded concurrency, input-order output, separate observations for multiple revisions, and positive/negative classifications in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckCoordinatorTests.cs`
- [X] T020 [P] [US1] Add adapter-level tests for exact Git commit and recursive tree request/response mapping in `TestMap.IntegrationTests/ProjectDiscovery/GitHubProjectTreeProbeIntegrationTests.cs`
- [X] T021 [US1] Add a moved-default-branch end-to-end fixture and failing revision-specific classification test in `TestMap.EndToEndTests/ProjectDiscovery/PinnedProjectCheckEndToEndTests.cs`

### Implementation For User Story 1

- [X] T022 [P] [US1] Implement the named `project-test-presence/v1` boundary-aware path policy in `TestMap/Services/ProjectDiscovery/ProjectTestPresencePolicy.cs`
- [X] T023 [P] [US1] Implement provider-neutral repository metadata, commit, tree-entry, and tree-result transport records in `TestMap/Services/ProjectDiscovery/Contracts/ProjectTreeProviderModels.cs`
- [X] T024 [US1] Implement the Octokit adapter that resolves repository identity, exact commit, commit tree SHA, and recursive tree without default-branch fallback in `TestMap/Services/ProjectDiscovery/GitHubProjectTreeClient.cs`
- [X] T025 [US1] Implement exact requested/observed commit validation and tree-to-policy classification in `TestMap/Services/ProjectDiscovery/GitHubProjectTreeProbe.cs`
- [X] T026 [US1] Implement bounded manifest-level target scheduling with deterministic input-order observations in `TestMap/Services/ProjectDiscovery/ProjectCheckCoordinator.cs`
- [X] T027 [US1] Preserve target ID, repository, requested commit, observed commit, policy name/version, evidence category/path, tree completeness, and check time in `TestMap/Services/ProjectDiscovery/ProjectCheckCoordinator.cs`
- [X] T028 [US1] Register the project-check policy, client, probe, coordinator, validators, and serializers in `TestMap/Services/ServiceCollectionExtensions.cs`
- [X] T029 [US1] Complete the exact-commit adapter integration test using deterministic provider responses in `TestMap.IntegrationTests/ProjectDiscovery/GitHubProjectTreeProbeIntegrationTests.cs`
- [X] T030 [US1] Complete the moved-default-branch end-to-end test and assert no default-branch value enters the observation in `TestMap.EndToEndTests/ProjectDiscovery/PinnedProjectCheckEndToEndTests.cs`
- [X] T031 [US1] Run all US1 unit, integration, and end-to-end filters from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`

**Checkpoint**: Exact-commit test-presence classification is functional and independently testable
in memory, including multiple revisions of one repository.

---

## Phase 4: User Story 2 - Produce Reusable YAML Target Lists (Priority: P1)

**Goal**: Publish tests-detected and no-tests-detected target subsets as directly reusable YAML
manifests, together with a content-addressed report and last-written completion pointer.

**Independent Test**: Supply a mixed set of determinate observations, publish a bundle, verify every
hash and partition invariant, then load both derived manifests through the normal target reader,
including a run where one category is empty.

### Tests For User Story 2

> Write these tests first and confirm they fail before implementing the story.

- [X] T032 [P] [US2] Add report YAML contract tests against `specs/002-check-project-manifests/contracts/project-check-report-v1.yaml` in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckSerializationTests.cs`
- [X] T033 [P] [US2] Add derived-manifest YAML contract tests against `specs/002-check-project-manifests/contracts/target-subset-manifest-v2.yaml`, including empty subsets, in `TestMap.UnitTests/Targets/TargetManifestSerializerTests.cs`
- [X] T034 [P] [US2] Add bundle-pointer YAML contract tests against `specs/002-check-project-manifests/contracts/project-check-bundle-v1.yaml` in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckSerializationTests.cs`
- [X] T035 [P] [US2] Add publisher tests for deterministic ordering, unchanged target fields, content-addressed filenames, member hash verification, repeated runs, and pointer-last behavior in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckBundlePublisherTests.cs`
- [X] T036 [P] [US2] Add injected-failure tests after each member publication and before pointer publication in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckBundlePublisherTests.cs`
- [X] T037 [P] [US2] Add downstream-reader tests for valid schema 2, report hash validation, empty subsets, missing reports, and changed reports in `TestMap.UnitTests/Configuration/TargetSourceReaderTests.cs`
- [X] T038 [P] [US2] Add CLI contract tests for direct options, configuration defaults, precedence, default filenames, output collisions, unknown policy, and legacy-list rejection in `TestMap.UnitTests/CLI/CheckProjectsCommandTests.cs`

### Implementation For User Story 2

- [X] T039 [P] [US2] Implement deterministic project-check report construction and status-count summaries from observations in `TestMap/Services/ProjectDiscovery/ProjectCheckReportBuilder.cs`
- [X] T040 [P] [US2] Implement exact input-target partitioning into tests-detected and no-tests-detected subsets in `TestMap/Services/ProjectDiscovery/ProjectCheckPartitionService.cs`
- [X] T041 [US2] Build schema-2 derived manifests that preserve every target field and reference the parent manifest, policy, category, and report hash in `TestMap/Services/ProjectDiscovery/ProjectCheckPartitionService.cs`
- [X] T042 [US2] Implement report-first serialization, member hashing, content-addressed publication, published-hash verification, and bundle-pointer-last commit in `TestMap/Services/ProjectDiscovery/ProjectCheckBundlePublisher.cs`
- [X] T043 [US2] Implement bundle loading and member hash/count/provenance validation in `TestMap/Services/ProjectDiscovery/ProjectCheckBundleReader.cs`
- [X] T044 [US2] Extend target-source reading to accept schema-2 subsets and verify their referenced project-check report while retaining schema-1 rejection verification in `TestMap/Services/Configuration/TargetSourceReader.cs`
- [X] T045 [US2] Add a dedicated `check-projects` command with `--file`, `--output`, `--max-concurrency`, `--policy`, and optional `--config` resolution in `TestMap/Program.cs`
- [X] T046 [US2] Wire command execution through the manifest reader, coordinator, report builder, partition service, and bundle publisher in `TestMap/Program.cs`
- [X] T047 [US2] Return exit code 0 for a fully determinate published bundle and print all category totals plus the absolute pointer path in `TestMap/Program.cs`
- [X] T048 [US2] Add a mixed and empty-category bundle integration test that reloads both outputs through the downstream target reader in `TestMap.IntegrationTests/ProjectDiscovery/ProjectCheckBundleIntegrationTests.cs`
- [X] T049 [US2] Assert repeated publication replaces the stable pointer without appending or duplicating targets in `TestMap.IntegrationTests/ProjectDiscovery/ProjectCheckBundleIntegrationTests.cs`
- [X] T050 [US2] Run all US2 contract, publisher, reader, CLI, and integration filters from `TestMap.UnitTests/TestMap.UnitTests.csproj` and `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`

**Checkpoint**: Both categorized YAML outputs are complete, reusable target manifests and a stable
pointer identifies one internally consistent bundle.

---

## Phase 5: User Story 3 - Audit Every Classification (Priority: P2)

**Goal**: Account for every input target and retain explicit, sanitized indeterminate statuses
without allowing failures or partial trees into the no-tests-detected category.

**Independent Test**: Check a manifest containing positive, negative, unavailable, unauthorized,
rate-limited, mismatched, truncated, transient-failure, and unexpected-failure cases. Verify one
terminal observation per target, exact status totals, no indeterminate target in either output
manifest, and exit code 2 with a valid incomplete bundle.

### Tests For User Story 3

> Write these tests first and confirm they fail before implementing the story.

- [X] T051 [P] [US3] Add provider-failure mapping tests for invalid target, identity mismatch, repository unavailable, authentication, authorization, rate limit, commit unavailable/mismatch, tree unavailable, service unavailable, and unexpected failure in `TestMap.UnitTests/ProjectDiscovery/GitHubProjectTreeProbeTests.cs`
- [X] T052 [P] [US3] Add truncation tests proving positive evidence remains positive while an unmatched partial tree is indeterminate in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckCoordinatorTests.cs`
- [X] T053 [P] [US3] Add coordinator resilience tests proving every input receives one terminal observation when individual probes throw in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckCoordinatorTests.cs`
- [X] T054 [P] [US3] Add partition tests that reject missing, duplicate, foreign, mismatched-commit, multiply categorized, and indeterminate target entries in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckPartitionTests.cs`
- [X] T055 [P] [US3] Add complete status-vocabulary and aggregate-count tests in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckReportBuilderTests.cs`
- [X] T056 [P] [US3] Add redaction and bounded-summary tests using token-, header-, and provider-body-shaped failures in `TestMap.UnitTests/ProjectDiscovery/ProjectCheckSanitizerTests.cs`
- [X] T057 [P] [US3] Add CLI tests for exit code 2 with a published incomplete bundle and exit code 1 when complete accounting or publication fails in `TestMap.UnitTests/CLI/CheckProjectsCommandTests.cs`

### Implementation For User Story 3

- [X] T058 [P] [US3] Implement stable provider exception and response classification without persisting raw sensitive text in `TestMap/Services/ProjectDiscovery/GitHubProjectCheckFailureClassifier.cs`
- [X] T059 [P] [US3] Implement sanitized, length-bounded observation summaries and evidence paths in `TestMap/Services/ProjectDiscovery/ProjectCheckSanitizer.cs`
- [X] T060 [US3] Complete probe mapping for all indeterminate statuses and tree truncation semantics in `TestMap/Services/ProjectDiscovery/GitHubProjectTreeProbe.cs`
- [X] T061 [US3] Catch non-cancellation target-level failures and emit exactly one terminal observation while preserving cancellation semantics in `TestMap/Services/ProjectDiscovery/ProjectCheckCoordinator.cs`
- [X] T062 [US3] Enforce complete input/report accounting, determinate-only partitioning, exact commit equality, and all status totals before publication in `TestMap/Services/ProjectDiscovery/ProjectCheckContractValidator.cs`
- [X] T063 [US3] Return exit code 2 for a valid incomplete bundle, exit code 1 for fatal validation/publication failures, and print an indeterminate status breakdown in `TestMap/Program.cs`
- [X] T064 [US3] Add an end-to-end incomplete-screening test covering provider failures, truncated trees, report census, derived subsets, redaction, and CLI completion status in `TestMap.EndToEndTests/ProjectDiscovery/PinnedProjectCheckEndToEndTests.cs`
- [X] T065 [US3] Run all US3 unit and end-to-end filters from `TestMap.UnitTests/TestMap.UnitTests.csproj` and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`

**Checkpoint**: Every target is auditable, every missing observation has an honest reason, and no
failure is interpreted as evidence of test absence.

---

## Phase 6: Polish And Cross-Cutting Validation

**Purpose**: Remove the obsolete path, document the new workflow, validate scale and security, and
run the complete regression matrix.

- [X] T066 Remove the append-based implementation in `TestMap/Services/ProjectDiscovery/CheckProjectsService.cs` and `TestMap/Services/ProjectDiscovery/ICheckProjectsService.cs`
- [X] T067 Remove the obsolete pipeline wrappers in `TestMap/Execution/Steps/CheckProjectsStep.cs`, `TestMap/Runs/CheckProjectsRun.cs`, and `TestMap/CLIOptions/CheckProjectsOptions.cs`
- [X] T068 Remove `CheckProjects` pipeline factory mappings and registrations while retaining the public command in `TestMap/Runs/PipelineRunFactory.cs` and `TestMap/Services/ServiceCollectionExtensions.cs`
- [X] T069 [P] Update the command summary and YAML workflow in `README.md`
- [X] T070 [P] Update token, exact-commit, output-bundle, exit-code, and no-tests-detected guidance in `Docs/SETUP.md` and `Docs/HOW_TO_USE.md`
- [X] T071 [P] Add a checked smoke command/config example that uses the pinned YAML input in `TestMap/Config/pinned-target-check.example.json`
- [X] T072 Add a deterministic 10,000-target bundle publication benchmark that records environment and artifact sizes in `TestMap.IntegrationTests/ProjectDiscovery/ProjectCheckPublicationPerformanceTests.cs`
- [X] T073 Run the complete unit, integration, and end-to-end suites from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`
- [X] T074 Run every scenario in `specs/002-check-project-manifests/quickstart.md` and record command results, exit codes, counts, hashes, and timing in `specs/002-check-project-manifests/validation-report.md`
- [X] T075 Scan project-check fixtures, YAML artifacts, reports, summaries, and documentation for credentials or raw authorization/provider data and record the result in `specs/002-check-project-manifests/validation-report.md`
- [X] T076 Verify that no canonical `repos_with_tests.txt` or `repos_without_tests.txt` references remain and document the deliberate non-compatibility in `specs/002-check-project-manifests/validation-report.md`

---

## Dependencies And Execution Order

### Phase Dependencies

- **Phase 1, Setup**: Starts immediately. T002, T003, and T004 can run in parallel after T001 defines
  fixture conventions.
- **Phase 2, Foundational**: Depends on Phase 1 and blocks all stories. Tests T005-T008 precede their
  implementations T009-T015.
- **Phase 3, US1**: Depends on Phase 2. It produces exact-commit observations but does not require
  publication.
- **Phase 4, US2**: Depends on Phase 2 for contracts. Its publisher can be developed using fixture
  observations in parallel with US1, but production CLI integration T046 depends on T026-T027.
- **Phase 5, US3**: Depends on the US1 probe/coordinator and US2 report/partition/publisher. It
  completes the missingness vocabulary and CLI completion semantics.
- **Phase 6, Polish**: Depends on all selected stories. Obsolete code is removed only after the new
  command passes its integration tests.

### User Story Dependencies

```text
Foundational contracts
    +----> US1 exact-revision classification ----+
    |                                            +----> US3 complete audit/missingness
    +----> US2 reusable YAML publication --------+
```

- **US1 (P1)**: Independent service increment after Foundation; MVP for proving revision validity.
- **US2 (P1)**: Independently testable with fixture observations after Foundation; production command
  composition uses US1.
- **US3 (P2)**: Integrates US1 and US2 and is required before the command is scientifically safe for
  a pilot.

### Within Each Story

- Write the listed tests first and confirm they fail for the intended reason.
- Add models/contracts before services that consume them.
- Implement provider adapters before production coordinator wiring.
- Validate all observations and partitions before serializing or publishing.
- Publish immutable members and verify hashes before writing the stable bundle pointer.
- Complete story-specific tests before advancing to the next dependent story.

## Parallel Opportunities

### User Story 1

```text
Parallel tests: T017 policy, T018 probe, T019 coordinator, T020 adapter
Parallel implementation: T022 policy and T023 provider transport models
Sequential boundary: T024 -> T025 -> T026 -> T027
```

### User Story 2

```text
Parallel tests: T032 report, T033 manifest, T034 bundle, T035/T036 publisher, T037 reader, T038 CLI
Parallel implementation: T039 report builder and T040 partition service
Sequential publication: T041 -> T042 -> T043
Sequential command integration: T044/T045 -> T046 -> T047
```

### User Story 3

```text
Parallel tests: T051-T057
Parallel implementation: T058 failure classifier and T059 sanitizer
Sequential integration: T060 -> T061 -> T062 -> T063 -> T064
```

Different contributors may develop US1 and the fixture-driven portion of US2 concurrently after
Phase 2. They should avoid editing `Program.cs` or shared serializer files simultaneously.

## Implementation Strategy

### MVP First

1. Complete Setup and Foundation.
2. Complete US1 and demonstrate exact-commit classification with a moved default branch.
3. Stop and inspect requested/observed commit provenance before building output automation.

This MVP proves the central research-validity claim but is not yet sufficient for a pilot because it
does not publish a reusable sampling frame.

### Incremental Delivery

1. **US1**: Correct exact-revision observations.
2. **US2**: Deterministic, directly reusable categorized YAML bundle.
3. **US3**: Complete census, honest missingness, redaction, and automation-safe exit codes.
4. **Polish**: Remove the legacy path, update guidance, benchmark publication, and run full validation.

### Pilot Gate

Do not use `check-projects` to define an evaluation cohort until T073-T076 are complete and the
validation report confirms:

- every input target has one report observation;
- requested and observed commits agree for every determinate target;
- no indeterminate target appears in either categorized manifest;
- all member hashes and counts validate;
- derived manifests reload through downstream target consumers;
- the default branch is never used for classification;
- no credential-shaped data is present in published artifacts.

## Task Format Validation

All 76 tasks use the required checkbox, sequential task ID, optional `[P]` marker, required user-story
label within story phases, concrete action, and exact file path.
