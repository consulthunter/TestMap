"""Evaluation overview exporter.

Computes headline counts for the cross-repository overview notebook and
writes evaluation_overview.json, evaluation_overview.csv, and
evaluation_overview.md.
"""

from __future__ import annotations

import json
from pathlib import Path

import pandas as pd

from analysis.files import ensure_output_dir
from analysis.schema import LANE_AGENTIC, LANE_LLM


# ---------------------------------------------------------------------------
# Count builders
# ---------------------------------------------------------------------------

def compute_scope_counts(df: pd.DataFrame) -> dict:
    """Repositories, projects, candidates, attempts evaluated."""
    return {
        "repositories_evaluated": int(df["repository_key"].nunique()) if "repository_key" in df.columns else None,
        "candidates_evaluated": int(df["candidate_key"].nunique()) if "candidate_key" in df.columns else None,
        "total_attempts": len(df),
        "llm_attempts": int((df["lane"] == LANE_LLM).sum()) if "lane" in df.columns else None,
        "agentic_attempts": int((df["lane"] == LANE_AGENTIC).sum()) if "lane" in df.columns else None,
        "models_evaluated": int(df["model"].nunique()) if "model" in df.columns else None,
        "tools_evaluated": int(df["tool_id"].dropna().nunique()) if "tool_id" in df.columns else None,
    }


def compute_generated_test_counts(df: pd.DataFrame) -> dict:
    """Generated test volume totals."""
    total = int(df["generated_test_count"].sum()) if "generated_test_count" in df.columns else None
    llm_mask = df.get("lane", pd.Series()) == LANE_LLM
    ag_mask = df.get("lane", pd.Series()) == LANE_AGENTIC
    return {
        "generated_tests_total": total,
        "generated_tests_llm": int(df.loc[llm_mask, "generated_test_count"].sum()) if "generated_test_count" in df.columns else None,
        "generated_tests_agentic": int(df.loc[ag_mask, "generated_test_count"].sum()) if "generated_test_count" in df.columns else None,
        "candidates_with_at_least_one_test": int(
            df.groupby("candidate_key")["generated_test_count"].sum().gt(0).sum()
        ) if "candidate_key" in df.columns and "generated_test_count" in df.columns else None,
        "candidates_with_multiple_tests": int(
            df.groupby("candidate_key")["generated_test_count"].sum().gt(1).sum()
        ) if "candidate_key" in df.columns and "generated_test_count" in df.columns else None,
    }


def compute_outcome_counts(df: pd.DataFrame) -> dict:
    """Validated successes, failures, no-changes, timeouts, crashes."""
    def _count(col: str, val) -> int | None:
        return int((df[col] == val).sum()) if col in df.columns else None

    return {
        "validated_successes": int(df["validated_success"].sum()) if "validated_success" in df.columns else None,
        "no_change_attempts": _count("failure_kind", "no_change"),
        "timeout_attempts": _count("failure_kind", "timeout"),
        "tool_crash_attempts": _count("failure_kind", "tool_crash"),
        "compile_failures": _count("failure_stage", "compile"),
        "test_failures": _count("failure_stage", "test"),
        "build_failures": _count("failure_stage", "build"),
    }


def compute_metric_movement(df: pd.DataFrame) -> dict:
    """How much did coverage and mutation scores move."""
    def _pos(col: str) -> int | None:
        return int((df[col].fillna(0) > 0).sum()) if col in df.columns else None

    return {
        "coverage_improvements": _pos("coverage_delta"),
        "mutation_improvements": _pos("mutation_score_delta"),
        "mean_coverage_delta": float(df["coverage_delta"].mean()) if "coverage_delta" in df.columns else None,
        "mean_mutation_delta": float(df["mutation_score_delta"].mean()) if "mutation_score_delta" in df.columns else None,
    }


def compute_cost_summary(df: pd.DataFrame) -> dict:
    """Runtime and token totals."""
    return {
        "total_duration_seconds": float(df["duration_seconds"].sum()) if "duration_seconds" in df.columns else None,
        "median_duration_seconds": float(df["duration_seconds"].median()) if "duration_seconds" in df.columns else None,
        "total_tokens": int(df["total_tokens"].sum()) if "total_tokens" in df.columns else None,
    }


def compute_data_completeness(df: pd.DataFrame) -> dict:
    """Missing measurement counts."""
    def _missing(col: str) -> int | None:
        return int(df[col].isna().sum()) if col in df.columns else None

    return {
        "missing_coverage_before": _missing("coverage_before"),
        "missing_coverage_after": _missing("coverage_after"),
        "missing_mutation_before": _missing("mutation_score_before"),
        "missing_mutation_after": _missing("mutation_score_after"),
        "missing_tokens": _missing("total_tokens"),
    }


def build_overview(df: pd.DataFrame) -> dict:
    """Build the complete evaluation overview dictionary."""
    return {
        "scope": compute_scope_counts(df),
        "generated_tests": compute_generated_test_counts(df),
        "outcomes": compute_outcome_counts(df),
        "metric_movement": compute_metric_movement(df),
        "cost": compute_cost_summary(df),
        "data_completeness": compute_data_completeness(df),
    }


# ---------------------------------------------------------------------------
# Write outputs
# ---------------------------------------------------------------------------

def run_overview(input_path: str, output_dir: str) -> None:
    """Build and write evaluation overview files from *input_path*."""
    df = pd.read_csv(input_path, low_memory=False)
    overview = build_overview(df)

    out = ensure_output_dir(output_dir)

    with open(out / "evaluation_overview.json", "w", encoding="utf-8") as f:
        json.dump(overview, f, indent=2, default=str)

    # Flatten to a two-column CSV: metric, value
    rows = [
        {"section": section, "metric": k, "value": v}
        for section, metrics in overview.items()
        for k, v in metrics.items()
    ]
    pd.DataFrame(rows).to_csv(out / "evaluation_overview.csv", index=False)

    # Markdown summary
    lines: list[str] = ["# Evaluation Overview\n"]
    for section, metrics in overview.items():
        lines.append(f"\n## {section.replace('_', ' ').title()}\n")
        for k, v in metrics.items():
            lines.append(f"- **{k}**: {v}")
    (out / "evaluation_overview.md").write_text("\n".join(lines), encoding="utf-8")

    print(f"Overview written to {out}")
