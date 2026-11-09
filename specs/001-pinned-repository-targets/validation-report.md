# Pinned Repository Targets Validation Report

**Validation date**: 2026-07-16  
**Feature**: `001-pinned-repository-targets`

## Summary

The pinned-target implementation satisfies the feature specification and its local validation
scenarios. Repository identity, requested and resolved revisions, workspace integrity, result
provenance, and analysis grouping are enforced across collection, experiment execution, persistence,
CSV export, and analysis ingestion.

This validation used deterministic local Git repositories and simulated lane fixtures. It did not
invoke paid or external model services, so a small real-model pilot remains the final operational
check before beginning the evaluation campaign.

## Automated Test Evidence

| Suite | Result |
|---|---:|
| .NET unit tests | 901 passed |
| .NET integration tests | 30 passed |
| .NET end-to-end tests | 4 passed |
| Python analysis tests | 147 passed |
| Notebook smoke executions | 5 of 5 passed |

All five notebooks executed through Papermill with `Analysis/notebooks` as the working directory.
Required-file checks also produced a clear failure when a notebook was intentionally launched from
an invalid data context.

## Quickstart Scenario Evidence

| Scenario | Evidence | Result |
|---|---|---:|
| Create a target manifest from the replication CSV | 1,525 rows read, 1,525 targets emitted, 0 rejected, 0 deduplicated | Pass |
| Repeat manifest creation | Target sections were byte-equivalent; only creation time and content-addressed report filename varied | Pass |
| Verify checked smoke targets | 2 targets verified, 2 available, 0 unavailable | Pass |
| Materialize an exact revision | Local bare-remote integration tests checked detached exact-commit checkout, origin validation, reuse, and missing-object fetch | Pass |
| Preserve revision isolation across lanes | End-to-end two-lane fixture used a pinned commit while the remote default branch moved | Pass |
| Detect workspace drift | Checkpoint tests covered expected changes, unexpected changes, revision mismatch, origin mismatch, rollback, and terminal blocking outcomes | Pass |
| Audit valid schema 3.0 results | 0 errors; 4 expected missing-metric warnings in the minimal fixture | Pass |
| Reject blocking integrity results | 1 blocking error; audit process exited with code 1 | Pass |
| Separate repository families and revisions | Python normalization, aggregation, audit, and export tests cover revision-aware keys and family-level summaries | Pass |

## Contract And Persistence Checks

- Target manifests require normalized repository identity and full 40-character nonzero commit SHAs.
- Manifest publication is last in the output bundle; rejection and verification reports are
  content-addressed.
- Revision workspaces and outputs are isolated by repository identity and resolved commit.
- Project, experiment-run, LLM-attempt, agent-attempt, and integrity-observation provenance is
  persisted by ordered EF Core migrations.
- `TestMap/schema.sql` was regenerated from the current migration chain.
- `dotnet ef migrations has-pending-model-changes` reported no model changes after the latest
  migration.
- Result schema 3.0 requires pinned provenance and verified integrity for validated or
  positive-impact outcomes.
- Analysis rejects legacy schemas and audits cross-revision collisions, provenance mismatch,
  cohort mismatch, child-row mismatch, and success with blocking integrity.
- A credential-shaped-value scan found no new secrets in feature fixtures or configuration. Matches
  in pre-existing repository lists were repository names containing `sk-`, not credentials.

## Residual Operational Checks

Before a full evaluation, run a small real-model pilot with both LLM and agentic lanes against two
or more pinned repositories. Confirm remote authentication, container volume behavior, external tool
usage reporting, and end-to-end result publication in the intended deployment environment.

The .NET restore/test output reports the existing `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 advisory
`GHSA-2m69-gcr7-jv3q`. This is not introduced by the feature, but should be resolved or explicitly
documented before distributing an evaluation release.

The installed EF Core command-line tools are version 10.0.5 while the runtime is 10.0.9. The parity
check completed successfully, but aligning those versions will remove the tooling warning.
