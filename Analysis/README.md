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
| `export-failures` | Build the failure dataset. `--markdown` writes per-case files; `--sample {all,stratified-lane,stratified-label,stratified-tool,top-n,high-severity,first-attempt,lane-llm,lane-agentic}` filters (`--seed` fixes stratified draws). Infrastructure failures are excluded unless `--include-infrastructure`. Artifact paths are re-rooted under each database's folder; pass `--logs <run>/logs` to re-root run logs. |
| `overview` | Headline counts (JSON/CSV) from an attempts CSV. |
| `audit` | Data-completeness report to run before analysis. |
| `export-training` | ML training export. `--grain {mapping,candidate,pair}`. |
| `repo-report` | Render a single-repository report. |
| `msr-population` | Select the MSR validation population from execution + validation reports; copy the corpus. |
| `build-msr-datasets` | Consolidate per-repo `analysis.db` files into MSR validation frames + structural checks. |
| `msr-coverage-audit` | Find repos claiming coverage with no coverage rows; classify why, write exclusions. |
| `msr-validate` | Draw the seeded human-judged sample; write units, codebooks, and rater forms. |
| `msr-collect` | Parse filled rating forms; report inter-rater agreement and agreement with TestMap. |

## Outputs (`data/`)

| File | Grain / contents |
|---|---|
| `evaluation_attempts.csv` | one row per attempt (LLM generation attempt or agentic tool attempt) |
| `evaluation_candidates.csv` | best attempt per (candidate, lane) |
| `evaluation_repositories.csv` | one row per (repository revision, lane) |
| `evaluation_repository_families.csv` | one row per (repository family, lane), aggregating revisions explicitly |
| `generated_tests.csv` | one row per generated/linked test |
| `tool_generated_test_links.csv` | agentic tool attempt → generated test member links |
| `assertion_observations.csv` | one row per recognized assertion from the schema-1 sidecar |
| `traced_assertions.csv` | derived view containing only semantic `Traced` observations |
| `mutation_operators.csv` | mutation-operator survival profile per repo (needs `--db`) |
| `evaluation_overview.json` / `.csv` | headline totals and outcome counts |
| `failures/failure_cases.csv` / `.jsonl` | qualitative failure dataset |
| `failures/cases/` | per-case Markdown (with `--markdown`) |

## Notebooks (`notebooks/`)

| Notebook | Focus |
|---|---|
| `00_msr_validation` | Mining-layer validation: availability, distributions, IQRs, structural checks. |
| `00b_pilot_msr_validation` | The same checks on the pilot's post-experiment databases. |

Exploratory notebooks live in `notebooks/exploratory/`; point them at a dataset with
`ANALYSIS_DATA_DIR` (default `../../data`).

| Notebook | Focus |
|---|---|
| `exploratory/01_repository_evaluation` | One repository (run per repo by `repo-report`); same sections as 02. |
| `exploratory/02_cross_repo_overview` | All repositories: outcomes, metric movement, generated tests, smells, assertions, mutation profile, footprint, cost, completeness. |
| `exploratory/03_cross_repo_lane_comparison` | LLM vs agentic: headline pass@1 pairs, chain/attempt/repository-weighted rates, cost-effectiveness. |
| `exploratory/04_model_tool_analysis` | Per model/tool: rates with Wilson CIs, producer summary and cost frontier, robustness, smells, predictors, repair. |
| `exploratory/05_failure_casebook` | Failure cases for qualitative coding: labels, seeded sample, headline-pair disagreements, safety failures. |

## Key semantics

- Two lanes: `llm` and `agentic`, both canonicalized to one row per attempt; generated tests are
  loaded from the separate result child file.
- `validated_success` = `ValidatedEvidencePositive` **or** `ValidatedLowImpact`; `positive_impact`
  (VEP) = test passed **and** metrics improved ≥ noise floor (coverage ≥ 1pp or mutation ≥ 1pp).
- `effective_tokens` = lane-fair cost (LLM cumulative repair-chain total; agentic total run) — use
  it, not raw `total_tokens`, for cost comparisons.
- Candidate-weighted views are the primary headline; attempt-weighted is a sensitivity check.
- `read_result_grains()` is the explicit schema-3 compatibility reader. Because schema 3 has no
  classified lineage, its assertion status is `NotMeasured` and its category counts remain null.
- `read_schema4_result_bundle()` reads schema-4 attempts/children together with the schema-1
  `*.assertions.csv` sidecar. Only these persisted semantic observations can populate `Traced`,
  `Trivial`, and `Unresolved`; legacy regex or invocation counts remain inventory-only.
- Run the strict audit before publishing assertion-quality results. It blocks unsupported versions,
  missing policy/reasons, duplicate or orphan identities, broken trace paths, count mismatches,
  missing sidecars, and cross-lane policy differences.
- Assertion lineage measures direct data dependence only. `Traced` does not establish oracle
  strength or mutation sensitivity, and it does not attribute a mutant kill to the assertion.

Design rationale and methodology: [analysis_plan.md](analysis_plan.md).
Column dictionary and lane-specific details: [docs/data_reference.md](docs/data_reference.md).
