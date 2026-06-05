"""Export a qualitative failure dataset for later open coding.

Outputs:
  failure_cases.csv
  failure_cases.jsonl
  failure_cases/*.md   (when --markdown is passed)
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import (
    ensure_output_dir,
    find_artifact_dir,
    read_results_csvs,
    read_text_artifact,
)
from analysis.normalize import normalize_attempts
from analysis.schema import FAILURE_CASE_FIELDS, PRELIMINARY_FAILURE_LABELS


CASE_MARKDOWN_TEMPLATE = """\
# Failure Case {failure_case_id}

Repo: {repo_owner}/{repo_name}
Commit: {commit_hash}
Lane: {lane}
Producer: {producer_id}
Model/Tool: {model_or_tool}
Source method: {source_method_signature}
Location: {source_file_path}:{source_line}
Failure: {preliminary_failure_label}

## Context

{outcome_summary}

## Prompt or Task

{prompt_excerpt}

## Response or Tool Log

{response_excerpt_or_logs_excerpt}

## Generated Code

{generated_code_excerpt}

## Diagnostics

{diagnostics_excerpt}

## Open Coding Notes

Open code 1:
Open code 2:
Open code 3:
Coder:
Coded at:
"""


def assign_preliminary_labels(df: pd.DataFrame) -> pd.DataFrame:
    """Assign a ``preliminary_failure_label`` to each row using available columns."""
    out = df.copy()
    out["preliminary_failure_label"] = "unclassified"

    def _label(row: pd.Series) -> str:
        tool_status = str(row.get("run_status", "") or row.get("tool_run_status", "")).lower()
        obs_outcome = str(row.get("observed_outcome", "") or row.get("tool_observed_outcome", "")).lower()
        failure_kind = str(row.get("failure_kind", "")).lower()
        failure_stage = str(row.get("failure_stage", "")).lower()
        prod_changed = row.get("production_files_changed", 0)
        proj_changed = row.get("project_files_changed", 0)
        validated = row.get("validated_success", False)

        if validated:
            return "unclassified"  # not a failure

        if "nochange" in tool_status or "no_change" in obs_outcome or failure_kind == "no_change":
            return "no_change"
        if "timedout" in tool_status or "timeout" in obs_outcome or failure_kind == "timeout":
            return "timeout"
        if "toolcrashed" in tool_status or "toolfailed" in obs_outcome or failure_kind == "tool_crash":
            return "tool_crash"
        if "buildfailed" in tool_status or "buildfailed" in obs_outcome or failure_stage == "build":
            return "build_failed"
        if failure_stage == "compile" or failure_kind == "compile_error":
            return "compile_failed"
        if failure_stage == "test":
            return "test_failed"
        if str(prod_changed or 0) != "0" and str(prod_changed or 0) != "nan":
            return "production_code_modified"
        if str(proj_changed or 0) != "0" and str(proj_changed or 0) != "nan":
            return "project_file_modified"
        return "unclassified"

    out["preliminary_failure_label"] = out.apply(_label, axis=1)
    return out


def build_failure_cases(
    attempts_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
) -> pd.DataFrame:
    """Build failure case rows from the normalized attempts dataset."""
    if attempts_df.empty:
        return pd.DataFrame()

    failed = attempts_df[
        ~attempts_df.get("validated_success", pd.Series(dtype=bool)).fillna(False)
    ].copy()

    failed = assign_preliminary_labels(failed)
    failed["failure_case_id"] = range(1, len(failed) + 1)

    # Enrich with artifact excerpts where possible
    def _enrich(row: pd.Series) -> pd.Series:
        artifact_dir = find_artifact_dir(
            artifacts_root,
            experiment_id=row.get("experiment_run_id", ""),
            candidate_id=row.get("candidate_method_id", ""),
            tool_id=str(row.get("tool_id", "")),
        )
        row["logs_excerpt"] = read_text_artifact(artifact_dir, "stdout.log", max_chars=2000)
        row["prompt_excerpt"] = read_text_artifact(artifact_dir, ".testmap/prompt.md", max_chars=1000)
        row["generated_code_excerpt"] = ""
        row["diagnostics_excerpt"] = ""
        row["response_excerpt"] = read_text_artifact(artifact_dir, "final-message.md", max_chars=1000)
        row["artifact_path"] = str(artifact_dir) if artifact_dir else ""
        row["raw_log_path"] = str(artifact_dir / "stdout.log") if artifact_dir else ""
        row["case_markdown_path"] = ""
        row["qualitative_notes"] = ""
        row["open_code_1"] = ""
        row["open_code_2"] = ""
        row["open_code_3"] = ""
        row["coder"] = ""
        row["coded_at"] = ""
        return row

    failed = failed.apply(_enrich, axis=1)
    return failed


def sample_cases(
    failure_df: pd.DataFrame,
    strategy: str = "all",
    top_n: int = 0,
) -> pd.DataFrame:
    """Apply a sampling strategy to the failure case DataFrame."""
    if failure_df.empty:
        return failure_df

    if strategy == "all":
        return failure_df
    if strategy == "stratified-lane":
        return failure_df.groupby("lane", group_keys=False).apply(
            lambda g: g.sample(min(len(g), max(1, top_n or len(g))))
        )
    if strategy == "stratified-label":
        return failure_df.groupby("preliminary_failure_label", group_keys=False).apply(
            lambda g: g.sample(min(len(g), max(1, top_n or len(g))))
        )
    if strategy == "stratified-tool":
        return failure_df.groupby("tool_id", group_keys=False).apply(
            lambda g: g.sample(min(len(g), max(1, top_n or len(g))))
        )
    if strategy == "top-n":
        counts = failure_df["preliminary_failure_label"].value_counts()
        top_labels = counts.head(top_n or 5).index
        return failure_df[failure_df["preliminary_failure_label"].isin(top_labels)]
    if strategy == "high-severity":
        severe = {"tool_crash", "timeout", "build_failed", "overbroad_change",
                  "production_code_modified"}
        return failure_df[failure_df["preliminary_failure_label"].isin(severe)]
    if strategy == "llm-won":
        return failure_df[failure_df.get("lane", pd.Series()) == "agentic"]
    if strategy == "agentic-won":
        return failure_df[failure_df.get("lane", pd.Series()) == "llm"]
    return failure_df


def write_markdown_case(case: pd.Series, output_dir: Path) -> Path:
    """Write a single failure case Markdown file and return its path."""
    md_dir = output_dir / "failure_cases"
    md_dir.mkdir(exist_ok=True)
    path = md_dir / f"case_{case['failure_case_id']:05d}.md"
    content = CASE_MARKDOWN_TEMPLATE.format(
        failure_case_id=case.get("failure_case_id", ""),
        repo_owner=case.get("repo_owner", ""),
        repo_name=case.get("repo_name", ""),
        commit_hash=case.get("commit_hash", "")[:12] if case.get("commit_hash") else "",
        lane=case.get("lane", ""),
        producer_id=case.get("producer_id", ""),
        model_or_tool=case.get("model", "") or case.get("tool_id", ""),
        source_method_signature=case.get("source_method_signature", ""),
        source_file_path=case.get("source_file_path", ""),
        source_line=case.get("source_line", ""),
        preliminary_failure_label=case.get("preliminary_failure_label", ""),
        outcome_summary=case.get("outcome_summary", ""),
        prompt_excerpt=case.get("prompt_excerpt", ""),
        response_excerpt_or_logs_excerpt=case.get("response_excerpt", "") or case.get("logs_excerpt", ""),
        generated_code_excerpt=case.get("generated_code_excerpt", ""),
        diagnostics_excerpt=case.get("diagnostics_excerpt", ""),
    )
    path.write_text(content, encoding="utf-8")
    return path


def run(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
    output_dir: str,
    sample: str = "all",
    top_n: int = 0,
    write_markdown: bool = False,
) -> None:
    """Entry point for the ``export-failures`` CLI command."""
    out = ensure_output_dir(output_dir)

    raw = read_results_csvs(list(results))
    attempts = normalize_attempts(raw)
    cases = build_failure_cases(attempts, db_paths, artifacts_root)
    sampled = sample_cases(cases, strategy=sample, top_n=top_n)

    sampled.to_csv(out / "failure_cases.csv", index=False)
    sampled.to_json(out / "failure_cases.jsonl", orient="records", lines=True)

    if write_markdown:
        for _, row in sampled.iterrows():
            md_path = write_markdown_case(row, out)
            sampled.loc[row.name, "case_markdown_path"] = str(md_path)
        sampled.to_csv(out / "failure_cases.csv", index=False)

    print(f"Failure cases written to {out} ({len(sampled):,} cases, strategy={sample})")
