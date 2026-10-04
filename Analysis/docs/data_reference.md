# Data Reference

Column and semantics reference for the datasets in `data/`. For the operational quickstart see
[../README.md](../README.md); for design rationale see [../analysis_plan.md](../analysis_plan.md).

## Lanes and grain

- `lane`: `llm` (direct LLM generation) or `agentic` (external tool).
- Result schema v3 writes separate attempt, generated-test, and test-result CSVs with pinned target,
  revision, manifest, and workspace-integrity provenance. Agentic attempts
  remain one row at attempt grain even when one tool invocation creates several tests.
- `attempt_id` is a deterministic SHA-256 key over repository, experiment series/run, lane, stable
  work-item key, and attempt number. Local `generation_attempt_id` and `tool_attempt_id` values are
  retained for database joins but are not globally unique.
- `candidate_key` = `repository_identity|resolved_commit|target_id|source_member_id`
  (lane-independent). `repository_key` = `repository_identity|resolved_commit`, where
  `repository_identity` is the canonical `owner/repo`.
- Chains group by candidate, lane, producer, budget arm, and available run/work-item identity:
  `experiment_run_uid` (legacy fallback `experiment_run_id`) and `resume_stable_key`
  (fallback `matrix_work_item_key`). Independent repetitions remain separate. Pairing requires
  explicitly selecting one run/configuration per candidate and lane when repetitions exist.

## Outcome columns

- `outcome_classification` — authoritative label: `ValidatedEvidencePositive` (VEP),
  `ValidatedLowImpact` (VLI), `ValidatedImpactUnknown`, `FailedEvidencePositive`, `ValidationFailed`, `BuildFailed`,
  `TestsFailed`, `TimedOut`, `ToolFailed`, `ConstraintViolation`, `NoChange`, `NotEvaluated`.
- `validated_success` — VEP, VLI, or `ValidatedImpactUnknown` (validation passed).
- `validated_evidence_positive` / `positive_impact` — VEP only (passed **and** metrics improved
  above the noise floor). This is the primary practical-impact measure.
- `metric_improved` — `coverage_delta` ≥ 0.01 (1pp on the 0–1 scale) **or** `mutation_score_delta`
  ≥ 1.0 (1pp on the 0–100 scale).
- `ValidatedImpactUnknown` means validation passed but no comparable before/after metric was
  available. `positive_impact` remains missing for these attempts rather than becoming false.
- `produced_change` — the attempt applied a change (LLM: a test was written; agentic:
  `changed_files_count > 0`).

## Metrics

- Coverage stored as a 0–1 fraction; mutation score as 0–100.
- `*_before` / `*_after` / `*_delta` for coverage and mutation. When a build/test failure prevents a
  post-measurement, `*_after`/`*_delta` are set to `NaN` (not 0) so they drop out of distributions.
- `effective_tokens` — lane-fair token cost. **Attempt level**: agentic `total_tokens`; LLM
  `cumulative_tokens` (repair-chain running total). **Chain level**: LLM last non-null cumulative
  total in numeric attempt order, agentic sum. Duplicate or partially missing sequence numbers
  are rejected; multi-step cumulative costs require a sequence. Input/output components follow
  the same order. Do **not** sum attempt-level LLM cumulative totals.
- For tokens among VEP results, filter chains to `any_positive_impact == True`, then summarize
  their measured `effective_tokens`. Pool both LLM budget arms for the all-LLM comparison.
  Missing agentic usage remains missing; report the number of VEP chains with measured usage.
- Change footprint (agentic): `changed_files_count`, `production_files_changed`,
  `test_files_changed`, `project_files_changed`, `deleted_files_count`. A non-zero production-file
  rate is a correctness risk.

## DB-derived columns (require `--db` at build time)

| Column(s) | Meaning |
|---|---|
| `assertion_count`, `invocation_count`, `assertion_source` | Assertions in the generated test. `assertion_source = invocations` is real Roslyn detection (validated tests with a persisted member); `code_regex` is the fallback for tests with no member. |
| `lines_closed`, `mutants_newly_killed` | Before/after set differences vs the targeted source member: coverage-gap lines closed, and mutants that survived the baseline but were killed after. Tool lane uses `targeted_baseline_id` + `post_attempt_test_run_id`; LLM lane uses `baseline_test_run_id` + `test_run_id`. Gap differences require explicitly usable member and report coverage on both sides; an absent gap row alone is not evidence of coverage. |
| `generated_test_n` | Number of generated tests counted for the attempt (agents often produce several). |
| `mutation_operators.csv` | Per repository revision × baseline report × mutator: status counts and `survival_rate` = Survived / (Survived + Killed). Includes `repository_key`, owner, commit, `mutation_testing_report_id`, and `_source_db`; profiles are restricted to the evaluation cohort. |

These populate only when the relevant SQLite columns exist (added by the
`AddGeneratedTestExecutionMemberAndBaseline` migration) and the experiment has been run with that
build — otherwise the analysis degrades gracefully (e.g. LLM assertions fall back to regex; diffs
stay `NaN`).

## Candidate-level additions (`evaluation_candidates.csv`)

`attempt_count`, `any_validated_success`, `any_positive_impact`,
`validated_evidence_positive_count`, `validated_low_impact_count`, `best_coverage_delta`,
`best_mutation_delta`, `total_generated_tests`, `effective_tokens` (lane-fair candidate cost).

`any_positive_impact` uses three-valued logic over evaluable attempts: any true makes the chain
true; all measured false makes it false; otherwise it remains unknown. Infrastructure attempts
are excluded from outcome aggregation.

## Training mapping snapshot

`export-training --grain mapping` retains one row per `mapping_id`. Measurement policy
`initial-test-run-v1` selects the first persisted test run (lowest ID) per project, then its
first coverage report and first solution-baseline mutation report. It does not substitute a
later or targeted report when the initial measurement is unavailable. Report IDs and test-run
IDs are exported. Legacy databases without run provenance use only an unambiguous single
snapshot (`legacy-single-snapshot`); rows that cannot be tied to a selected report stay unknown.

Coverage, gap counts, and mutation features come from these selected reports. Identical
duplicate member-coverage observations are collapsed; conflicting values become missing with
`coverage_observation_status = ConflictingObservations`. Other statuses are `Measured` and
`Unavailable`. Missing or unusable coverage does not produce a zero gap count. Static joins
that duplicate a mapping are rejected rather than expanding the training population.

## Empty datasets and rebuilds

Header-only result grains and assertion sidecars are valid under the explicitly selected
schema. Nonempty rows still require the correct version. A present, empty assertion sidecar
is distinct from a missing sidecar. A successful rebuild overwrites or removes each optional
evaluation output so an older cohort's generated tests, assertions, profiles, or families
cannot remain in that output directory. MSR appends validate and align columns; legacy trace
metadata stays nullable within the same CSV schema.
