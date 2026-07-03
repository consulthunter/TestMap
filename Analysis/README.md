# TestMap Analysis

Post-run analysis of TestMap test-generation evaluations. Builds clean CSVs from experiment
outputs, then explores them in notebooks — comparing the **LLM lane** against the **agentic-tool
lane**.

## Prerequisites

- Python 3.13+ with [uv](https://docs.astral.sh/uv/) — run `uv sync` once to install deps.
- Source data from a TestMap run:
  - experiment **result CSV(s)** (e.g. `combined-llm-vs-tools-experiment-results.csv`), and
  - per-repo **`analysis.db`** SQLite files — typically under `TestMap/Output/`.

The `--db` files are optional but required for assertion counts, before/after diffs, and
mutation-operator survival (see [docs/data_reference.md](docs/data_reference.md)).

## Quickstart

```powershell
# 1. Build the canonical datasets into data/
uv run python -m analysis build-datasets `
  --results "../TestMap/Output/combined-llm-vs-tools-experiment-results.csv" `
  --db "../TestMap/Output/**/analysis.db" --out data

# 2. Export the qualitative failure set (Markdown case files for open coding)
uv run python -m analysis export-failures `
  --results "../TestMap/Output/combined-llm-vs-tools-experiment-results.csv" `
  --db "../TestMap/Output/**/analysis.db" --out data/failures --markdown

# 3. Explore — notebooks read only from data/
uv run jupyter lab notebooks
```

Point `--results` at your run's result CSV (repeat the flag or glob for several); repeat `--db`
per repository database.

## Commands

| Command | Purpose |
|---|---|
| `build-datasets` | Build the canonical CSVs + overview from result CSVs (and DBs). |
| `export-failures` | Build the failure dataset. `--markdown` writes per-case files; `--sample {all,stratified-lane,stratified-label,stratified-tool,top-n,high-severity,llm-won,agentic-won}` filters. |
| `overview` | Headline counts (JSON/CSV) from an attempts CSV. |
| `audit` | Data-completeness report to run before analysis. |
| `export-training` | ML training export. `--grain {mapping,candidate,pair}`. |
| `repo-report` | Render a single-repository report. |

## Outputs (`data/`)

| File | Grain / contents |
|---|---|
| `evaluation_attempts.csv` | one row per attempt (LLM generation attempt or agentic tool attempt) |
| `evaluation_candidates.csv` | best attempt per (candidate, lane) |
| `evaluation_repositories.csv` | one row per (repository, lane) |
| `generated_tests.csv` | one row per generated/linked test |
| `tool_generated_test_links.csv` | agentic tool attempt → generated test member links |
| `mutation_operators.csv` | mutation-operator survival profile per repo (needs `--db`) |
| `evaluation_overview.json` / `.csv` | headline totals and outcome counts |
| `failures/failure_cases.csv` / `.jsonl` | qualitative failure dataset |
| `failures/cases/` | per-case Markdown (with `--markdown`) |

## Notebooks (`notebooks/`)

| Notebook | Focus |
|---|---|
| `01_repository_evaluation` | Deep dive on one repository. |
| `02_cross_repo_overview` | Study population, headline outcomes, data completeness. |
| `03_cross_repo_lane_comparison` | LLM vs agentic on shared outcomes (paired, weighted, cost). |
| `04_model_tool_analysis` | Per model/tool ranking, predictors, best-by-category. |
| `05_failure_casebook` | Sampling frame + flow for qualitative coding. |

## Key semantics

- Two lanes: `llm` (one row/attempt) and `agentic` (collapsed to one row per tool attempt).
- `validated_success` = `ValidatedEvidencePositive` **or** `ValidatedLowImpact`; `positive_impact`
  (VEP) = test passed **and** metrics improved ≥ noise floor (coverage ≥ 1pp or mutation ≥ 1pp).
- `effective_tokens` = lane-fair cost (LLM cumulative repair-chain total; agentic total run) — use
  it, not raw `total_tokens`, for cost comparisons.
- Candidate-weighted views are the primary headline; attempt-weighted is a sensitivity check.

Design rationale and methodology: [analysis_plan.md](analysis_plan.md).
Column dictionary and lane-specific details: [docs/data_reference.md](docs/data_reference.md).
