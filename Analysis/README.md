# Analysis

Post-run analysis for TestMap repository-level and cross-repository test-generation evaluations.

The notebooks consume clean CSV files only. Use the scripts in `src/analysis` to build those CSVs from experiment result files and, when needed, SQLite databases.

## Build Datasets

```powershell
python -m analysis build-datasets --results "Output/**/*.csv" --db "Output/**/*.db" --out Analysis/data
```

Canonical outputs:

- `evaluation_attempts.csv`: one row per LLM generation attempt or agentic tool attempt.
- `evaluation_candidates.csv`: one row per candidate and lane.
- `evaluation_repositories.csv`: one row per repository and lane.
- `generated_tests.csv`: one row per generated or linked test.
- `evaluation_overview.json` and `evaluation_overview.csv`: headline totals and outcome counts.

Agentic rows require `tool_attempt_id`. Agentic attempts with multiple generated tests are collapsed at attempt level; generated-test rows keep the parent attempt deltas with `impact_attribution = attempt_level`.

## Semantics

- `validated_success` includes `ValidatedEvidencePositive` and `ValidatedLowImpact`.
- `positive_impact` is separate and requires evidence-positive status or positive coverage/mutation movement.
- Low-impact passing attempts are validation successes, but not positive-impact successes.

## Qualitative Failures

```powershell
python -m analysis export-failures --results "Output/**/*.csv" --db "Output/**/*.db" --out Analysis/data/failures --markdown
```

Failure exports write `failure_cases.csv`, `failure_cases.jsonl`, and Markdown case directories under `Analysis/data/failures/cases`.
