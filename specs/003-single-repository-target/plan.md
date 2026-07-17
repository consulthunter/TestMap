# Implementation Plan: Single Repository Target

**Branch**: `not created - no before-plan branch hook configured` | **Date**: 2026-07-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-single-repository-target/spec.md`

## Summary

Extend `targets create` with a mutually exclusive `--url` input mode for one strict GitHub HTTPS
repository URL. URL mode validates and normalizes locally, resolves the repository's current default
branch head as a full commit through a consistency-checked provider observation, writes a sanitized
content-addressed resolution record, and atomically publishes a one-target schema-3 manifest last.
Existing delimited-file creation remains unchanged. Downstream target readers validate the linked
resolution record once and then use only the pinned commit; they never re-resolve the URL.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`, nullable reference types enabled)

**Primary Dependencies**: System.CommandLine 2.0.9, Octokit 14.0.0, YamlDotNet 18.1.0, existing
target identity, fingerprinting, serializer, sanitizer, and atomic publisher services

**Storage**: Versioned YAML manifest and content-addressed YAML resolution record; no database or
migration changes

**Testing**: xUnit unit, integration, and end-to-end projects already present; deterministic fake
provider clients plus one live public-repository smoke check

**Target Platform**: Cross-platform .NET CLI with GitHub.com as the sole v1 repository provider

**Project Type**: CLI research tool

**Performance Goals**: For a normally responsive public repository, complete or fail with a stable
category within 30 seconds; local parsing, validation, serialization, hashing, and publication under
one second

**Constraints**: Strict HTTPS GitHub repository URL; exactly one repository; full 40-character
nonzero commit; no branch/commit override in v1; no redirect-based identity substitution; no
manifest on failed resolution; previous completed output preserved; credentials never persisted

**Scale/Scope**: One repository and one target per invocation; up to two complete resolution passes
when repository metadata changes during observation

## Constitution Check

*GATE: PASS before Phase 0 research. Re-checked after Phase 1 design: PASS.*

- **Scientific purpose**: This feature simplifies creation of a one-repository pilot target while
  protecting the claim that extraction and all evaluation lanes use one immutable source revision.
  The URL is acquisition input only, never an experiment-time target.
- **Units and scope**: One URL-resolution observation produces zero or one repository target. Target
  identity remains normalized repository plus exact commit. No attempt, test, candidate, metric, or
  outcome grain is introduced.
- **Eligibility and pairing**: The researcher selects one repository. Resolution fixes its source
  revision but does not imply test presence, successful collection, candidate eligibility, or cohort
  membership. Later lanes consume the same manifest and commit.
- **Provenance**: Persist requested URL and URL fingerprint, provider identity, normalized repository,
  canonical URL, default branch, exact commit, resolution time, policy name/version, authentication
  mode, resolution status, and record hash. Target and manifest identities derive from this exact
  commit.
- **Lane fairness and isolation**: Resolution occurs before lane assignment. No lane may re-resolve
  the repository or use a newer branch head. Existing exact-commit materialization and integrity
  checks remain authoritative during experiments.
- **Missingness and failures**: Invalid URL, identity mismatch, repository unavailable,
  authentication/authorization failure, rate limit, empty repository, missing default branch,
  unavailable commit, inconsistent observation, service failure, and unexpected failure are
  explicit resolution statuses. None produces a target or substitutes another revision.
- **Verification**: Unit tests cover URL grammar, normalization, status mapping, retries, redaction,
  schema 3, and input exclusivity. Integration tests cover bundle publication and downstream
  consumption. End-to-end tests cover a moving default branch and output preservation. Contract and
  live smoke checks validate serialized artifacts and CLI behavior.
- **Compatibility**: Schema 1 delimited-source manifests and schema 2 categorized manifests remain
  unchanged. Schema 3 explicitly represents URL-resolution provenance. Existing database, result
  CSV, cohort, analysis, and notebook semantics do not change.

### Post-Design Re-check

The design keeps the URL-resolution observation separate from target identity, versions the new
provenance instead of overloading rejection or derivation fields, publishes the provenance record
before the manifest, and requires every downstream consumer to verify it. No constitution exception
or complexity waiver is required.

## Project Structure

### Documentation (this feature)

```text
specs/003-single-repository-target/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli-contract.md
│   ├── single-repository-resolution-v1.yaml
│   └── target-manifest-v3.yaml
└── tasks.md
```

### Source Code (repository root)

```text
TestMap/
├── Program.cs
├── Models/Targets/
│   ├── TargetManifest.cs
│   └── SingleRepositoryResolution.cs
├── Services/Configuration/
│   └── TargetSourceReader.cs
└── Services/Targets/
    ├── TargetIdentityService.cs
    ├── TargetManifestSerializer.cs
    ├── SingleRepositoryUrlParser.cs
    ├── GitHubRepositoryResolutionClient.cs
    ├── SingleRepositoryResolver.cs
    ├── SingleRepositoryResolutionSerializer.cs
    ├── SingleRepositoryTargetCreationService.cs
    └── Contracts/
        └── SingleRepositoryTargetContracts.cs

TestMap.UnitTests/
├── CLI/TargetCommandsTests.cs
└── Targets/
    ├── SingleRepositoryUrlParserTests.cs
    ├── SingleRepositoryResolverTests.cs
    ├── SingleRepositoryResolutionSerializerTests.cs
    └── TargetManifestSerializerTests.cs

TestMap.IntegrationTests/
└── Targets/SingleRepositoryTargetCreationIntegrationTests.cs

TestMap.EndToEndTests/
└── SingleRepositoryTargetEndToEndTests.cs
```

**Structure Decision**: Keep all URL parsing, provider resolution, provenance serialization, and
creation orchestration in the existing `Targets` boundary. Reuse the current target identity and
manifest reader rather than adding a second target format or project pipeline. The CLI composes the
new service only for URL mode; file mode continues through the existing creation service unchanged.

## Phase 0: Research Decisions

Research is consolidated in [research.md](research.md). The principal decisions are:

1. Accept only strict `https://github.com/{owner}/{repository}[.git][/ ]` URLs in v1.
2. Resolve repository metadata, named default branch, exact Git commit object, then repository
   metadata again; retry the complete observation once when identity/default-branch metadata changes.
3. Reject repository redirects, renames, and transfers as identity mismatches.
4. Add target-manifest schema 3 with URL source provenance and a required resolution-record reference.
5. Interpret `source_rows: [1]` as the one logical URL input record to preserve target-entry
   compatibility without inventing a second target shape.
6. Publish a content-addressed resolution record for both success and failure; publish or replace the
   stable target manifest only after a successful record is verified.
7. Use policy `github-default-branch-head/v1`; omit automatic rate-limit sleeps and return a stable
   retryable failure instead.
8. Reuse `GITHUB_TOKEN` when present and record only `authenticated` or `anonymous` mode.

## Phase 1: Design And Contracts

### Data Model

[data-model.md](data-model.md) defines the strict URL request, resolution observation and status
vocabulary, schema-3 manifest provenance, one-target invariants, and publication state machine.

### Public Contracts

- [CLI contract](contracts/cli-contract.md): mutual exclusivity, options, output defaults, summaries,
  and exit behavior.
- [Resolution record v1](contracts/single-repository-resolution-v1.yaml): canonical success example
  and failure-field rules.
- [Target manifest v3](contracts/target-manifest-v3.yaml): directly consumable one-target manifest
  linked to the resolution record.

### Implementation Sequence

1. Add strict URL parsing and normalization tests, then implement the parser without remote access.
2. Add schema-3 target models and serializer tests while preserving schemas 1 and 2 byte semantics.
3. Define provider-neutral resolution transport and implement the consistency-checked resolver with
   one complete retry.
4. Add the GitHub adapter for repository metadata, branch head, and exact commit object, with stable
   failure classification and no raw response persistence.
5. Implement deterministic resolution-record serialization, validation, hashing, and redaction.
6. Implement URL creation orchestration: resolution record first, hash verification, manifest last,
   and previous-manifest preservation on failure.
7. Add mutually exclusive `--url` and `--input` CLI handling, URL-mode option validation, output
   defaults, summaries, and exit semantics.
8. Extend downstream target reading to verify schema-3 resolution record hash and semantic agreement
   without making a provider request.
9. Add integration and end-to-end fixtures for equivalent URL forms, moving branch heads,
   inconsistent metadata, all failure statuses, and downstream consumption.
10. Update documentation and run all test suites plus an authenticated/anonymous public-repository
    smoke check as available.

## Complexity Tracking

No constitution violations require justification.
