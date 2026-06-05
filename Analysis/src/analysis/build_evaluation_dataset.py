"""Build normalized evaluation datasets from result CSVs and optional SQLite databases.

Outputs written to --out:
  evaluation_attempts.csv       one row per attempt
  evaluation_candidates.csv     best attempt per candidate per lane
  evaluation_repositories.csv   one row per repository/lane slice
  generated_tests.csv           one row per generated/linked test
  evaluation_overview.json      headline counts
  evaluation_overview.csv       flattened overview
"""

from __future__ import annotations

import json
from pathlib import Path

import pandas as pd

from analysis.files import ensure_output_dir, read_results_csvs
from analysis.normalize import (
    build_candidate_summary,
    build_repository_summary,
    normalize_attempts,
)
from analysis.summaries import build_overview


def load_raw_attempts(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
) -> pd.DataFrame:
    """Load and concatenate attempt rows from result CSVs.

    SQLite join queries are deferred to a future pass when the column
    mapping between CSVs and the DB schema is finalized.
    """
    df = read_results_csvs(list(results))
    if df.empty:
        print("[warn] No result CSVs found.")
    return df


def build_attempts_dataset(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Normalize raw attempt rows into the shared attempt-level schema."""
    if raw_df.empty:
        return pd.DataFrame()
    return normalize_attempts(raw_df)


def build_generated_tests_dataset(
    attempts_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...],
) -> pd.DataFrame:
    """Build a generated-test-level dataset.

    For agentic attempts this requires joining tool_attempt_generated_tests
    with the SQLite database. Returns an empty DataFrame when no databases
    are provided.
    """
    if not db_paths:
        return pd.DataFrame()

    frames: list[pd.DataFrame] = []
    from analysis.db import connect, get_tool_attempt_generated_tests, get_members
    from analysis.files import find_databases

    for db_path in find_databases(list(db_paths)):
        try:
            with connect(db_path) as conn:
                links = get_tool_attempt_generated_tests(conn)
                members = get_members(conn)
                merged = links.merge(members, left_on="member_id", right_on="id",
                                     how="left", suffixes=("", "_member"))
                merged["_source_db"] = str(db_path)
                frames.append(merged)
        except Exception as exc:
            print(f"[warn] Could not read generated tests from {db_path}: {exc}")

    return pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()


def save_datasets(
    attempts: pd.DataFrame,
    candidates: pd.DataFrame,
    repositories: pd.DataFrame,
    generated_tests: pd.DataFrame,
    overview: dict,
    output_dir: Path,
) -> None:
    """Write all datasets to *output_dir*."""
    attempts.to_csv(output_dir / "evaluation_attempts.csv", index=False)
    candidates.to_csv(output_dir / "evaluation_candidates.csv", index=False)
    repositories.to_csv(output_dir / "evaluation_repositories.csv", index=False)
    if not generated_tests.empty:
        generated_tests.to_csv(output_dir / "generated_tests.csv", index=False)

    with open(output_dir / "evaluation_overview.json", "w", encoding="utf-8") as f:
        json.dump(overview, f, indent=2, default=str)

    rows = [
        {"section": section, "metric": k, "value": v}
        for section, metrics in overview.items()
        for k, v in metrics.items()
    ]
    pd.DataFrame(rows).to_csv(output_dir / "evaluation_overview.csv", index=False)


def run(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: str | None,
    output_dir: str,
) -> None:
    """Entry point for the ``build-datasets`` CLI command."""
    out = ensure_output_dir(output_dir)

    print("Loading raw attempts...")
    raw = load_raw_attempts(results, db_paths)
    print(f"  {len(raw)} raw rows loaded.")

    attempts = build_attempts_dataset(raw)
    candidates = build_candidate_summary(attempts)
    repositories = build_repository_summary(candidates)
    generated_tests = build_generated_tests_dataset(attempts, db_paths)
    overview = build_overview(attempts)

    save_datasets(attempts, candidates, repositories, generated_tests, overview, out)

    print(f"Datasets written to {out}")
    print(f"  attempts:     {len(attempts):,}")
    print(f"  candidates:   {len(candidates):,}")
    print(f"  repositories: {len(repositories):,}")
    print(f"  gen. tests:   {len(generated_tests):,}")
