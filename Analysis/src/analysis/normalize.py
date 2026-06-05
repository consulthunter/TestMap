"""Normalization functions for building clean evaluation datasets.

These functions operate on DataFrames loaded from result CSVs or database
queries and produce the standardized tables that notebooks consume.
"""

from __future__ import annotations

import pandas as pd

from analysis.schema import (
    CANDIDATE_KEY_FIELDS,
    LANE_AGENTIC,
    LANE_LLM,
    REPOSITORY_KEY_FIELDS,
    WINNER_LABELS,
)

# ---------------------------------------------------------------------------
# Lane normalization
# ---------------------------------------------------------------------------

_LANE_MAP: dict[str, str] = {
    "llm": LANE_LLM,
    "testmap": LANE_LLM,
    "direct": LANE_LLM,
    "agentic": LANE_AGENTIC,
    "tool": LANE_AGENTIC,
    "agent": LANE_AGENTIC,
    "agent_tool": LANE_AGENTIC,
}


def normalize_lane(raw: str) -> str:
    """Map a raw lane string to the canonical ``llm`` or ``agentic`` value."""
    return _LANE_MAP.get(str(raw).strip().lower(), raw)


# ---------------------------------------------------------------------------
# Key construction
# ---------------------------------------------------------------------------

def make_candidate_key(row: pd.Series) -> str:
    """Build a stable string key for a candidate (repo + source member)."""
    parts = [
        str(row.get("repo_owner", "")),
        str(row.get("repo_name", "")),
        str(row.get("commit_hash", "")),
        str(row.get("source_member_id", "")),
        str(row.get("project_path", "")),
    ]
    return "|".join(parts)


def make_repository_key(row: pd.Series) -> str:
    """Build a stable string key for a repository slice."""
    parts = [
        str(row.get("repo_owner", "")),
        str(row.get("repo_name", "")),
        str(row.get("commit_hash", "")),
    ]
    return "|".join(parts)


# ---------------------------------------------------------------------------
# Attempt-level normalization
# ---------------------------------------------------------------------------

def normalize_attempts(df: pd.DataFrame) -> pd.DataFrame:
    """Apply shared normalization to a raw attempt DataFrame.

    - Standardizes the ``lane`` column.
    - Adds ``candidate_key`` and ``repository_key`` columns.
    - Coerces numeric columns to float where expected.
    """
    if df.empty:
        return df

    out = df.copy()

    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)

    numeric_cols = [
        "coverage_before", "coverage_after", "coverage_delta",
        "mutation_score_before", "mutation_score_after", "mutation_score_delta",
        "duration_seconds", "total_tokens", "generated_test_count",
        "changed_files_count", "test_files_changed", "production_files_changed",
        "project_files_changed", "deleted_files_count",
    ]
    for col in numeric_cols:
        if col in out.columns:
            out[col] = pd.to_numeric(out[col], errors="coerce")

    bool_cols = ["validated_success", "produced_change", "mutant_killed"]
    for col in bool_cols:
        if col in out.columns:
            out[col] = out[col].map(
                lambda v: True if str(v).lower() in ("1", "true", "yes") else
                          False if str(v).lower() in ("0", "false", "no") else
                          pd.NA
            )

    return out


# ---------------------------------------------------------------------------
# Candidate-level aggregation
# ---------------------------------------------------------------------------

def build_candidate_summary(attempts_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse attempt rows to one row per candidate.

    The best attempt per candidate is selected as:
    1. Any attempt where ``validated_success`` is True.
    2. Otherwise the attempt with the highest ``coverage_delta``.
    3. Otherwise the first attempt.
    """
    if attempts_df.empty:
        return pd.DataFrame()

    records: list[dict] = []
    group_cols = ["candidate_key", "lane"]
    if not all(c in attempts_df.columns for c in group_cols):
        return pd.DataFrame()

    for (candidate_key, lane), group in attempts_df.groupby(group_cols, sort=False):
        successes = group[group.get("validated_success", pd.Series(dtype=bool)) == True]
        if not successes.empty:
            best = successes.iloc[0]
        elif "coverage_delta" in group.columns:
            best = group.loc[group["coverage_delta"].fillna(-999).idxmax()]
        else:
            best = group.iloc[0]

        row = best.to_dict()
        row["attempt_count"] = len(group)
        row["any_validated_success"] = bool((group.get("validated_success", pd.Series()) == True).any())
        row["best_coverage_delta"] = group["coverage_delta"].max() if "coverage_delta" in group.columns else None
        row["best_mutation_delta"] = group["mutation_score_delta"].max() if "mutation_score_delta" in group.columns else None
        row["total_generated_tests"] = group["generated_test_count"].sum() if "generated_test_count" in group.columns else None
        records.append(row)

    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Repository-level aggregation
# ---------------------------------------------------------------------------

def build_repository_summary(candidates_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse candidate rows to one row per repository/lane slice."""
    if candidates_df.empty:
        return pd.DataFrame()

    records: list[dict] = []
    for (repo_key, lane), group in candidates_df.groupby(["repository_key", "lane"], sort=False):
        row: dict = {
            "repository_key": repo_key,
            "lane": lane,
            "repo_owner": group["repo_owner"].iloc[0] if "repo_owner" in group.columns else "",
            "repo_name": group["repo_name"].iloc[0] if "repo_name" in group.columns else "",
            "commit_hash": group["commit_hash"].iloc[0] if "commit_hash" in group.columns else "",
            "candidate_count": len(group),
            "validated_success_count": int((group.get("any_validated_success", pd.Series()) == True).sum()),
            "validated_success_rate": (group.get("any_validated_success", pd.Series()) == True).mean(),
            "mean_coverage_delta": group["best_coverage_delta"].mean() if "best_coverage_delta" in group.columns else None,
            "mean_mutation_delta": group["best_mutation_delta"].mean() if "best_mutation_delta" in group.columns else None,
            "total_generated_tests": group["total_generated_tests"].sum() if "total_generated_tests" in group.columns else None,
        }
        records.append(row)

    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Paired comparison builder
# ---------------------------------------------------------------------------

def build_paired_comparison(candidates_df: pd.DataFrame) -> pd.DataFrame:
    """Build a paired table with one row per candidate, comparing LLM vs agentic lanes.

    Winner labels (from schema.WINNER_LABELS):
      llm_won, agentic_won, tie_success, tie_failure,
      llm_only, agentic_only, no_comparable_result
    """
    if candidates_df.empty:
        return pd.DataFrame()

    llm = candidates_df[candidates_df["lane"] == LANE_LLM].copy()
    agentic = candidates_df[candidates_df["lane"] == LANE_AGENTIC].copy()

    llm = llm.set_index("candidate_key")
    agentic = agentic.set_index("candidate_key")

    all_keys = llm.index.union(agentic.index)
    records: list[dict] = []

    for key in all_keys:
        has_llm = key in llm.index
        has_agentic = key in agentic.index
        llm_row = llm.loc[key] if has_llm else None
        ag_row = agentic.loc[key] if has_agentic else None

        def _success(r: pd.Series | None) -> bool:
            return bool(r is not None and r.get("any_validated_success", False))

        llm_success = _success(llm_row)
        ag_success = _success(ag_row)

        if has_llm and has_agentic:
            if llm_success and ag_success:
                winner = "tie_success"
            elif not llm_success and not ag_success:
                winner = "tie_failure"
            elif llm_success:
                winner = "llm_won"
            else:
                winner = "agentic_won"
        elif has_llm:
            winner = "llm_only"
        elif has_agentic:
            winner = "agentic_only"
        else:
            winner = "no_comparable_result"

        record: dict = {
            "candidate_key": key,
            "winner": winner,
            "llm_validated_success": llm_success,
            "agentic_validated_success": ag_success,
            "llm_attempt_count": int(llm_row["attempt_count"]) if llm_row is not None and "attempt_count" in llm_row else None,
            "agentic_attempt_count": int(ag_row["attempt_count"]) if ag_row is not None and "attempt_count" in ag_row else None,
            "llm_best_coverage_delta": float(llm_row["best_coverage_delta"]) if llm_row is not None and pd.notna(llm_row.get("best_coverage_delta")) else None,
            "agentic_best_coverage_delta": float(ag_row["best_coverage_delta"]) if ag_row is not None and pd.notna(ag_row.get("best_coverage_delta")) else None,
            "llm_best_mutation_delta": float(llm_row["best_mutation_delta"]) if llm_row is not None and pd.notna(llm_row.get("best_mutation_delta")) else None,
            "agentic_best_mutation_delta": float(ag_row["best_mutation_delta"]) if ag_row is not None and pd.notna(ag_row.get("best_mutation_delta")) else None,
        }
        records.append(record)

    return pd.DataFrame(records)
