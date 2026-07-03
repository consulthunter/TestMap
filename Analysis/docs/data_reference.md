# Data Reference

Column and semantics reference for the datasets in `data/`. For the operational quickstart see
[../README.md](../README.md); for design rationale see [../analysis_plan.md](../analysis_plan.md).

## Lanes and grain

- `lane`: `llm` (direct LLM generation — one CSV row per attempt) or `agentic` (external tool — the
  raw CSV has one row per generated test; `normalize` collapses these to one row per tool attempt).
- `attempt_id` is globally unique: `llm:{owner}|{repo}:{generation_attempt_id}` or
  `agentic:{tool_artifact_path}`. (Per-repo numeric ids are *not* unique across repos, hence the
  qualifier.)
- `candidate_key` = `owner|repo|commit|source_member_id` (lane-independent, so the two lanes pair on
  it). `repository_key` = `owner|repo|commit`.

## Outcome columns

- `outcome_classification` — authoritative label: `ValidatedEvidencePositive` (VEP),
  `ValidatedLowImpact` (VLI), `FailedEvidencePositive`, `ValidationFailed`, `BuildFailed`,
  `TestsFailed`, `TimedOut`, `ToolFailed`, `ConstraintViolation`, `NoChange`, `NotEvaluated`.
- `validated_success` — VEP **or** VLI (broad success).
- `validated_evidence_positive` / `positive_impact` — VEP only (passed **and** metrics improved
  above the noise floor). This is the primary practical-impact measure.
- `metric_improved` — `coverage_delta` ≥ 0.01 (1pp on the 0–1 scale) **or** `mutation_score_delta`
  ≥ 1.0 (1pp on the 0–100 scale).
- `produced_change` — the attempt applied a change (LLM: a test was written; agentic:
  `changed_files_count > 0`).

## Metrics

- Coverage stored as a 0–1 fraction; mutation score as 0–100.
- `*_before` / `*_after` / `*_delta` for coverage and mutation. When a build/test failure prevents a
  post-measurement, `*_after`/`*_delta` are set to `NaN` (not 0) so they drop out of distributions.
- `effective_tokens` — lane-fair token cost. **Attempt level**: agentic `total_tokens`; LLM
  `cumulative_tokens` (repair-chain running total). **Candidate level**: LLM max cumulative over the
  chain, agentic sum. Do **not** sum attempt-level LLM `effective_tokens` (cumulative double-counts).
- Change footprint (agentic): `changed_files_count`, `production_files_changed`,
  `test_files_changed`, `project_files_changed`, `deleted_files_count`. A non-zero production-file
  rate is a correctness risk.

## DB-derived columns (require `--db` at build time)

| Column(s) | Meaning |
|---|---|
| `assertion_count`, `invocation_count`, `assertion_source` | Assertions in the generated test. `assertion_source = invocations` is real Roslyn detection (validated tests with a persisted member); `code_regex` is the fallback for tests with no member. |
| `lines_closed`, `mutants_newly_killed` | Before/after set differences vs the targeted source member: coverage-gap lines closed, and mutants that survived the baseline but were killed after. Tool lane uses `targeted_baseline_id` + `post_attempt_test_run_id`; LLM lane uses `baseline_test_run_id` + `test_run_id`. |
| `generated_test_n` | Number of generated tests counted for the attempt (agents often produce several). |
| `mutation_operators.csv` | Per repo × mutator: `Killed`/`Survived`/… counts and `survival_rate` = Survived / (Survived + Killed). |

These populate only when the relevant SQLite columns exist (added by the
`AddGeneratedTestExecutionMemberAndBaseline` migration) and the experiment has been run with that
build — otherwise the analysis degrades gracefully (e.g. LLM assertions fall back to regex; diffs
stay `NaN`).

## Candidate-level additions (`evaluation_candidates.csv`)

`attempt_count`, `any_validated_success`, `any_positive_impact`,
`validated_evidence_positive_count`, `validated_low_impact_count`, `best_coverage_delta`,
`best_mutation_delta`, `total_generated_tests`, `effective_tokens` (lane-fair candidate cost).
