# Tasks: Single Repository Target

**Input**: Design documents from `specs/003-single-repository-target/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required. This feature resolves repository identity and selects an exact source revision,
so tests must precede implementation and cover URL grammar, consistency, provenance, publication,
missingness, downstream compatibility, and file-mode non-regression.

**Organization**: Tasks are grouped by user story so URL convenience, revision pinning, and failure
diagnostics remain independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it affects different files and has no dependency on an
  incomplete task in the same phase.
- **[Story]**: Maps the task to a user story from spec.md.
- Every task includes an exact repository path.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish deterministic fixtures and contract examples without changing production
behavior.

- [x] T001 Create single-repository fixture conventions and document synthetic credential rules in `TestMap.UnitTests/Fixtures/SingleRepositoryTargets/README.md`
- [x] T002 [P] Add accepted, rejected, mixed-case, trailing-slash, `.git`, credential-bearing, encoded, and subpage URL cases in `TestMap.UnitTests/Fixtures/SingleRepositoryTargets/urls.json`
- [x] T003 [P] Add stable, moving-default-branch, inconsistent-pass, empty-repository, and failure provider response fixtures in `TestMap.UnitTests/Fixtures/SingleRepositoryTargets/provider-responses.json`
- [x] T004 [P] Add valid success, valid failure, malformed, secret-shaped, and schema-3 manifest YAML fixtures in `TestMap.UnitTests/Fixtures/SingleRepositoryTargets/Manifests/`

---

## Phase 2: Foundational (Blocking Contracts And Schema Evolution)

**Purpose**: Define strict URL-resolution records and schema 3 before implementing provider or CLI
behavior.

**CRITICAL**: No user-story implementation begins until this phase passes.

### Foundational Tests

> Write these tests first and confirm they fail for the intended missing contracts.

- [x] T005 [P] Add failing tests for all resolution statuses, success/failure field availability, pass counts, timestamps, retry time, policy, and authentication mode in `TestMap.UnitTests/Targets/SingleRepositoryResolutionModelTests.cs`
- [x] T006 [P] Add failing YAML contract tests against `specs/003-single-repository-target/contracts/single-repository-resolution-v1.yaml` in `TestMap.UnitTests/Targets/SingleRepositoryResolutionSerializerTests.cs`
- [x] T007 [P] Add failing schema-3 contract and round-trip tests against `specs/003-single-repository-target/contracts/target-manifest-v3.yaml` in `TestMap.UnitTests/Targets/TargetManifestSerializerTests.cs`
- [x] T008 [P] Add failing tests that schemas 1, 2, and 3 require mutually exclusive provenance fields and retain their target-count rules in `TestMap.UnitTests/Targets/TargetManifestSerializerTests.cs`
- [x] T009 [P] Add failing tests for exact URL source hashing, resolution-record hashing, basename-only references, one target, and `source_rows: [1]` in `TestMap.UnitTests/Targets/SingleRepositoryTargetContractTests.cs`

### Foundational Implementation

- [x] T010 Add URL source provenance, schema-3 resolution reference, and mutually exclusive manifest provenance models in `TestMap/Models/Targets/TargetManifest.cs`
- [x] T011 Add resolution policy, authentication mode, status vocabulary, request, observation, creation result, and categorized exception models in `TestMap/Models/Targets/SingleRepositoryResolution.cs`
- [x] T012 Define URL parser, provider client, resolver, resolution serializer, and creation-service contracts in `TestMap/Services/Targets/Contracts/SingleRepositoryTargetContracts.cs`
- [x] T013 Implement closed-field and cross-field resolution observation validation in `TestMap/Services/Targets/SingleRepositoryResolutionValidator.cs`
- [x] T014 Implement deterministic resolution-record YAML DTO mapping, strict enum/timestamp parsing, serialization, and deserialization in `TestMap/Services/Targets/SingleRepositoryResolutionSerializer.cs`
- [x] T015 Extend target-manifest DTO mapping and strict validation for schema 3 while preserving schema-1 and schema-2 semantics in `TestMap/Services/Targets/TargetManifestSerializer.cs`
- [x] T016 Implement schema-3 cross-artifact validation for URL hash, resolution hash, repository, canonical URL, commit, target ID, and source row in `TestMap/Services/Targets/SingleRepositoryTargetContractValidator.cs`
- [x] T017 Run and keep green all foundational resolution and target-manifest tests in `TestMap.UnitTests/TestMap.UnitTests.csproj`

**Checkpoint**: Resolution schema 1 and target-manifest schema 3 are strict, versioned, and cannot be
confused with CSV or categorized-manifest provenance.

---

## Phase 3: User Story 1 - Create A Target From One Repository URL (Priority: P1) MVP

**Goal**: Accept exactly one safe GitHub repository URL and create one standard pinned target
without an intermediate CSV.

**Independent Test**: Feed equivalent supported URLs through a deterministic successful provider,
create one-target manifests, and verify normalized repository/canonical URL equality, exact commit,
target identity, output defaults, and mutual exclusion from file mode.

### Tests For User Story 1

> Write these tests first and confirm they fail before implementing the story.

- [x] T018 [P] [US1] Add URL parser tests for supported HTTPS GitHub forms and equivalent normalized repository identity in `TestMap.UnitTests/Targets/SingleRepositoryUrlParserTests.cs`
- [x] T019 [P] [US1] Add URL rejection tests for schemes, hosts, ports, credentials, query, fragment, dot/encoded segments, malformed identities, and repository subpages in `TestMap.UnitTests/Targets/SingleRepositoryUrlParserTests.cs`
- [x] T020 [P] [US1] Add tests proving every invalid URL causes zero provider calls and produces no artifact in `TestMap.UnitTests/Targets/SingleRepositoryTargetCreationServiceTests.cs`
- [x] T021 [P] [US1] Add CLI contract tests for exactly one of `--url`/`--input`, URL-only/file-only options, repeated URL rejection, and validation before I/O in `TestMap.UnitTests/CLI/TargetCommandsTests.cs`
- [x] T022 [P] [US1] Add CLI tests for URL output defaults, custom output paths, success summary fields, and access-mode disclosure in `TestMap.UnitTests/CLI/TargetCommandsTests.cs`
- [x] T023 [P] [US1] Add creation tests proving one target, `[1]` source row, canonical URL, shared target ID policy, URL hash, resolution hash, and manifest-last publication in `TestMap.UnitTests/Targets/SingleRepositoryTargetCreationServiceTests.cs`

### Implementation For User Story 1

- [x] T024 [P] [US1] Implement strict local HTTPS GitHub repository URL parsing and normalization in `TestMap/Services/Targets/SingleRepositoryUrlParser.cs`
- [x] T025 [P] [US1] Implement URL-mode output defaults, content-addressed resolution naming, collision checks, and manifest-overwrite protection paths in `TestMap/Services/Targets/SingleRepositoryTargetPathResolver.cs`
- [x] T026 [US1] Implement successful observation-to-target construction using the existing target identity service in `TestMap/Services/Targets/SingleRepositoryTargetCreationService.cs`
- [x] T027 [US1] Implement resolution-record-first hashing, atomic publication, published-hash verification, schema-3 construction, and manifest-last publication in `TestMap/Services/Targets/SingleRepositoryTargetCreationService.cs`
- [x] T028 [US1] Add mutually exclusive `--url` and `--input` options plus URL-only `--resolution-output` validation to `targets create` in `TestMap/Program.cs`
- [x] T029 [US1] Preserve the existing `--input`, `--delimiter`, `--rejections`, output defaults, and file summary path unchanged in `TestMap/Program.cs`
- [x] T030 [US1] Add URL-mode service composition, environment anchoring, token presence detection, success summary, and exit code 0 handling in `TestMap/Program.cs`
- [x] T031 [US1] Add a successful URL creation integration test that reloads schema 3 through the shared serializer in `TestMap.IntegrationTests/Targets/SingleRepositoryTargetCreationIntegrationTests.cs`
- [x] T032 [US1] Run all US1 URL parser, creation, CLI, and integration tests from `TestMap.UnitTests/TestMap.UnitTests.csproj` and `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`

**Checkpoint**: A deterministic successful provider can turn one safe URL into one complete,
standard target manifest, while file mode remains independently available.

---

## Phase 4: User Story 2 - Pin The Resolved Revision Reproducibly (Priority: P1)

**Goal**: Resolve repository identity, named default branch, and exact commit from one compatible
observation, retrying the entire sequence once when metadata changes.

**Independent Test**: Run stable, first-pass-inconsistent/second-pass-stable, twice-inconsistent, and
moving-branch fixtures. Confirm only compatible-pass fields reach the record, every target uses a
full exact commit, and existing manifests never follow later branch movement.

### Tests For User Story 2

> Write these tests first and confirm they fail before implementing the story.

- [x] T033 [P] [US2] Add resolver sequence tests for metadata, named branch, exact Git commit, and confirmation metadata calls in `TestMap.UnitTests/Targets/SingleRepositoryResolverTests.cs`
- [x] T034 [P] [US2] Add tests for repository identity mismatch, absent default branch, malformed branch SHA, commit mismatch, and nonzero full-SHA enforcement in `TestMap.UnitTests/Targets/SingleRepositoryResolverTests.cs`
- [x] T035 [P] [US2] Add tests proving one full retry after metadata inconsistency, no pass-field mixing, and terminal inconsistency after two unstable passes in `TestMap.UnitTests/Targets/SingleRepositoryResolverTests.cs`
- [x] T036 [P] [US2] Add provider-adapter tests for repository metadata, branch-head, and Git commit response mapping without latest/default fallback calls in `TestMap.UnitTests/Targets/GitHubRepositoryResolutionClientTests.cs`
- [x] T037 [P] [US2] Add tests proving authentication mode records only authenticated/anonymous and never token content in `TestMap.UnitTests/Targets/GitHubRepositoryResolutionClientTests.cs`
- [x] T038 [P] [US2] Add deterministic moving-default-branch end-to-end tests proving manifest A retains commit A and a later manifest B records commit B in `TestMap.EndToEndTests/SingleRepositoryTargetEndToEndTests.cs`

### Implementation For User Story 2

- [x] T039 [P] [US2] Implement provider-neutral repository metadata, branch head, commit object, and retry-time transport records in `TestMap/Services/Targets/Contracts/SingleRepositoryProviderModels.cs`
- [x] T040 [P] [US2] Implement named resolution policy constants and pass-comparison helpers in `TestMap/Services/Targets/SingleRepositoryResolutionPolicy.cs`
- [x] T041 [US2] Implement the complete metadata-branch-commit-confirmation pass and compatibility checks in `TestMap/Services/Targets/SingleRepositoryResolver.cs`
- [x] T042 [US2] Implement discard-and-retry-once behavior with `resolution_passes` accounting and terminal `InconsistentResolution` in `TestMap/Services/Targets/SingleRepositoryResolver.cs`
- [x] T043 [US2] Implement Octokit repository metadata, named branch, and exact Git commit requests in `TestMap/Services/Targets/GitHubRepositoryResolutionClient.cs`
- [x] T044 [US2] Require provider `full_name` identity equality and forbid redirect/rename adoption in `TestMap/Services/Targets/SingleRepositoryResolver.cs`
- [x] T045 [US2] Record requested/completed time, requested URL hash, branch, commit, provider, policy, access mode, and sanitized success summary in `TestMap/Services/Targets/SingleRepositoryResolver.cs`
- [x] T046 [US2] Extend schema-3 target reading to verify the linked resolution record and return the pinned target without provider access in `TestMap/Services/Configuration/TargetSourceReader.cs`
- [x] T047 [US2] Add downstream tests for resolution-record absence/change, URL hash mismatch, identity/branch/commit mismatch, failed resolution, and proof of zero provider access in `TestMap.UnitTests/Configuration/TargetSourceReaderTests.cs`
- [x] T048 [US2] Add integration tests that consume schema 3 through `targets verify` and `check-projects` service boundaries in `TestMap.IntegrationTests/Targets/SingleRepositoryTargetConsumptionTests.cs`
- [x] T049 [US2] Complete the moving-branch end-to-end test and verify both resolution records remain independently hash-valid in `TestMap.EndToEndTests/SingleRepositoryTargetEndToEndTests.cs`
- [x] T050 [US2] Run all US2 resolver, adapter, reader, integration, and end-to-end tests from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`

**Checkpoint**: Every URL-created target is tied to one compatible, full, immutable commit
observation and is never re-resolved downstream.

---

## Phase 5: User Story 3 - Diagnose Unresolvable Single Targets (Priority: P2)

**Goal**: Retain one sanitized, categorized resolution record for every valid URL attempt that
cannot produce a target, while preserving any previous completed manifest.

**Independent Test**: Exercise every failure status with synthetic secret-bearing provider errors.
Verify exact status/reason, content-addressed failure record, exit code 1, no new manifest, unchanged
previous manifest bytes, no automatic rate-limit wait, and no secret leakage.

### Tests For User Story 3

> Write these tests first and confirm they fail before implementing the story.

- [x] T051 [P] [US3] Add provider failure mapping tests for unavailable repository, authentication, authorization, rate limit, empty repository, unavailable branch/commit, service failure, and unexpected failure in `TestMap.UnitTests/Targets/GitHubRepositoryResolutionClientTests.cs`
- [x] T052 [P] [US3] Add failure-record invariant and strict YAML round-trip tests for every status in `TestMap.UnitTests/Targets/SingleRepositoryResolutionSerializerTests.cs`
- [x] T053 [P] [US3] Add tests proving valid-URL failures publish exactly one content-addressed resolution record and no manifest in `TestMap.UnitTests/Targets/SingleRepositoryTargetCreationServiceTests.cs`
- [x] T054 [P] [US3] Add tests proving failure and manifest-publication exceptions preserve previous completed manifest bytes in `TestMap.UnitTests/Targets/SingleRepositoryTargetCreationServiceTests.cs`
- [x] T055 [P] [US3] Add redaction tests for authorization, bearer, PAT, token, password, raw body, stack trace, and overlong provider messages in `TestMap.UnitTests/Targets/SingleRepositoryResolutionSanitizerTests.cs`
- [x] T056 [P] [US3] Add tests proving rate-limit observations retain only sanitized retry time and do not delay or retry provider calls in `TestMap.UnitTests/Targets/SingleRepositoryResolverTests.cs`
- [x] T057 [P] [US3] Add CLI tests for categorized failure summary, resolution path, exit code 1, and no secret output in `TestMap.UnitTests/CLI/TargetCommandsTests.cs`

### Implementation For User Story 3

- [x] T058 [P] [US3] Implement stable Octokit/provider exception classification and optional retry-time extraction in `TestMap/Services/Targets/GitHubRepositoryResolutionFailureClassifier.cs`
- [x] T059 [P] [US3] Implement bounded summary and credential/raw-response sanitization in `TestMap/Services/Targets/SingleRepositoryResolutionSanitizer.cs`
- [x] T060 [US3] Map resolver validation and provider failures to the complete resolution status and reason vocabulary in `TestMap/Services/Targets/SingleRepositoryResolver.cs`
- [x] T061 [US3] Publish and verify failed resolution records before throwing a categorized creation exception in `TestMap/Services/Targets/SingleRepositoryTargetCreationService.cs`
- [x] T062 [US3] Prevent manifest creation or replacement for every non-Resolved observation and every partial publication failure in `TestMap/Services/Targets/SingleRepositoryTargetCreationService.cs`
- [x] T063 [US3] Add categorized URL-resolution failure summaries, record paths, and exit code 1 without stack traces in `TestMap/Program.cs`
- [x] T064 [US3] Add an end-to-end failure matrix covering preserved output, failure artifacts, redaction, and no provider retry on rate limits in `TestMap.EndToEndTests/SingleRepositoryTargetEndToEndTests.cs`
- [x] T065 [US3] Run all US3 failure, redaction, publication, CLI, and end-to-end tests from `TestMap.UnitTests/TestMap.UnitTests.csproj` and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`

**Checkpoint**: Every valid-URL failure is auditable and sanitized, but no failure can masquerade as
a usable target manifest.

---

## Phase 6: Polish And Cross-Cutting Validation

**Purpose**: Complete registration, documentation, live validation, compatibility evidence, and the
pilot gate.

- [x] T066 Register single-repository parser, resolver, provider adapter, serializers, validators, and creation service in `TestMap/Services/ServiceCollectionExtensions.cs`
- [x] T067 [P] Update URL-mode command examples, moving-branch explanation, schema-3 provenance, and file-mode compatibility in `README.md`
- [x] T068 [P] Update target configuration, token behavior, failure records, and downstream usage in `Docs/CONFIG.md`, `Docs/SETUP.md`, and `Docs/HOW_TO_USE.md`
- [x] T069 [P] Add checked schema-3 and resolution-record examples based on synthetic fixture values in `TestMap/Data/single-repository-target-example.yaml` and `TestMap/Data/single-repository-resolution-example.yaml`
- [x] T070 Run the existing CSV target import command and assert schema 1, rejection artifact, accounting, and deterministic behavior remain unchanged in `TestMap.IntegrationTests/Targets/LicenseFilteredTargetImportTests.cs`
- [x] T071 Run the complete unit, integration, and end-to-end suites from `TestMap.UnitTests/TestMap.UnitTests.csproj`, `TestMap.IntegrationTests/TestMap.IntegrationTests.csproj`, and `TestMap.EndToEndTests/TestMap.EndToEndTests.csproj`
- [x] T072 Run the public-repository URL smoke scenario in `specs/003-single-repository-target/quickstart.md` with anonymous and authenticated modes as available and record elapsed time in `specs/003-single-repository-target/validation-report.md`
- [x] T073 Validate the live schema-3 manifest with `targets verify`, `check-projects`, and configuration loading and record exact repository/branch/commit agreement in `specs/003-single-repository-target/validation-report.md`
- [x] T074 Recompute URL, resolution, target, and manifest hashes and record publication-order evidence in `specs/003-single-repository-target/validation-report.md`
- [x] T075 Scan fixtures, records, manifests, console captures, and documentation for credential/raw-provider patterns and record results in `specs/003-single-repository-target/validation-report.md`
- [x] T076 Run `git diff --check`, verify no unresolved placeholders or old schema assumptions remain, and record deliberate branch/commit-override deferral in `specs/003-single-repository-target/validation-report.md`

---

## Dependencies And Execution Order

### Phase Dependencies

- **Phase 1, Setup**: Starts immediately. T002-T004 can run in parallel after T001 establishes
  fixture conventions.
- **Phase 2, Foundational**: Depends on Phase 1 and blocks every story. Contract tests T005-T009
  precede models and serializers T010-T016.
- **Phase 3, US1**: Depends on Phase 2. A deterministic successful resolver may be stubbed before the
  real GitHub adapter exists.
- **Phase 4, US2**: Depends on US1 request/output contracts and completes production resolution plus
  downstream pinning.
- **Phase 5, US3**: Depends on the US2 resolver and US1 publisher; it completes all failure semantics.
- **Phase 6, Polish**: Depends on all three stories and is the pilot gate.

### User Story Dependencies

```text
Foundation
   -> US1 safe URL + one-target publication
       -> US2 exact compatible revision resolution
           -> US3 complete failure and missingness accounting
               -> Pilot validation
```

- **US1 (P1)**: Independently testable with a deterministic successful resolver; establishes the
  convenient user workflow and schema-3 bundle.
- **US2 (P1)**: Extends US1 with real exact-revision consistency and downstream immutability.
- **US3 (P2)**: Requires the resolver and publisher boundaries, then makes every unsuccessful
  acquisition auditable and safe.

### Within Each Story

- Write the listed tests first and confirm each fails for the expected missing behavior.
- Parse and normalize locally before any provider call.
- Define provider-neutral records before the GitHub adapter.
- Resolve identity and commit before building a target.
- Validate and hash the resolution record before manifest construction.
- Publish the resolution artifact and verify it before publishing the manifest last.
- Validate schema-3 provenance downstream without network access.
- Finish story-specific tests before advancing to a dependent story.

## Parallel Opportunities

### User Story 1

```text
Parallel tests: T018-T023
Parallel implementation: T024 URL parser and T025 path resolver
Sequential creation boundary: T026 -> T027 -> T028/T029 -> T030 -> T031
```

### User Story 2

```text
Parallel tests: T033-T038
Parallel implementation: T039 provider models and T040 policy helpers
Sequential resolution: T041 -> T042 -> T043/T044 -> T045 -> T046
Parallel downstream validation: T047 and T048 after T046
```

### User Story 3

```text
Parallel tests: T051-T057
Parallel implementation: T058 failure classifier and T059 sanitizer
Sequential failure publication: T060 -> T061 -> T062 -> T063 -> T064
```

Contributors should avoid editing `Program.cs`, `TargetManifest.cs`, or
`TargetManifestSerializer.cs` concurrently.

## Implementation Strategy

### MVP First

1. Complete Setup and Foundation.
2. Complete US1 with a deterministic successful resolver.
3. Demonstrate that one safe URL produces one valid schema-3 target and that URL/file options are
   mutually exclusive.

This MVP validates the workflow but is not pilot-ready until US2 establishes production consistency
and US3 closes every failure path.

### Incremental Delivery

1. **US1**: Safe local URL input and one-target publication.
2. **US2**: Consistency-checked GitHub default-branch head pinning and downstream non-resolution.
3. **US3**: Complete failure records, redaction, and prior-manifest preservation.
4. **Polish**: Documentation, live smoke, file-mode regression, hash/security audit, full suites.

### Pilot Gate

Do not use URL mode to define an evaluation target until T071-T076 confirm:

- the accepted URL identifies exactly one normalized GitHub repository;
- repository identity and default-branch metadata were consistent within the successful pass;
- branch head and Git commit object contain the same full nonzero SHA;
- the resolution record and schema-3 manifest hashes validate;
- downstream consumers load the exact target without network resolution;
- every failed valid-URL attempt has one sanitized record and no new manifest;
- an older completed manifest survives all resolution/publication failures;
- CSV creation remains schema-1 compatible and unchanged;
- generated and captured artifacts contain no credential-shaped values.

## Task Format Validation

All 76 tasks use the required checkbox, sequential task ID, optional `[P]` marker, required
user-story label inside story phases, concrete action, and exact repository path.
