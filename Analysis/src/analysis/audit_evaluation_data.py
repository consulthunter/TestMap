"""Data completeness auditor for evaluation datasets.

Checks that evaluation data is analyzable before notebook work begins.

Outputs:
  audit_report.json
  audit_report.csv
  audit_report.md
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import ensure_output_dir, find_databases, read_results_csvs
from analysis.normalize import normalize_attempts


# ---------------------------------------------------------------------------
# Individual checks
# ---------------------------------------------------------------------------

def audit_missing_coverage(df: pd.DataFrame) -> list[dict]:
    findings = []
    for col in ("coverage_before", "coverage_after"):
        n = int(df[col].isna().sum()) if col in df.columns else len(df)
        if n > 0:
            findings.append({
                "check": f"missing_{col}",
                "severity": "warning",
                "count": n,
                "detail": f"{n} attempts are missing {col}.",
            })
    return findings


def audit_missing_mutation(df: pd.DataFrame) -> list[dict]:
    findings = []
    for col in ("mutation_score_before", "mutation_score_after"):
        n = int(df[col].isna().sum()) if col in df.columns else len(df)
        if n > 0:
            findings.append({
                "check": f"missing_{col}",
                "severity": "warning",
                "count": n,
                "detail": f"{n} attempts are missing {col}.",
            })
    return findings


def audit_missing_tokens(df: pd.DataFrame) -> list[dict]:
    findings = []
    col = "total_tokens"
    if col in df.columns:
        n = int(df[col].isna().sum())
        if n > 0:
            findings.append({
                "check": "missing_total_tokens",
                "severity": "info",
                "count": n,
                "detail": f"{n} attempts have no token data (expected for some tools).",
            })
    return findings


def audit_duplicate_rows(df: pd.DataFrame) -> list[dict]:
    findings = []
    if "attempt_id" in df.columns:
        n_dupes = int(df.duplicated(subset=["attempt_id"]).sum())
        if n_dupes > 0:
            findings.append({
                "check": "duplicate_attempt_ids",
                "severity": "error",
                "count": n_dupes,
                "detail": f"{n_dupes} duplicate attempt_id values found.",
            })
    if "candidate_key" in df.columns and "lane" in df.columns:
        # More than one row per (candidate_key, lane) is expected — skip
        pass
    return findings


def audit_lane_labels(df: pd.DataFrame) -> list[dict]:
    findings = []
    if "lane" not in df.columns:
        return [{"check": "missing_lane_column", "severity": "error", "count": len(df),
                 "detail": "No 'lane' column found."}]
    unexpected = df[~df["lane"].isin(("llm", "agentic"))]["lane"].unique().tolist()
    if unexpected:
        findings.append({
            "check": "unexpected_lane_values",
            "severity": "error",
            "count": len(unexpected),
            "detail": f"Unexpected lane values after normalization: {unexpected}",
        })
    return findings


def audit_agentic_no_post_attempt(df: pd.DataFrame) -> list[dict]:
    findings = []
    if "lane" not in df.columns:
        return findings
    agentic = df[df["lane"] == "agentic"]
    if agentic.empty:
        return findings
    if "tool_post_attempt_test_run_id" in agentic.columns:
        measurable = agentic.get("tool_run_status", pd.Series("", index=agentic.index)).fillna("").eq("Completed")
        n = int(agentic.loc[measurable, "tool_post_attempt_test_run_id"].isna().sum())
        if n > 0:
            findings.append({
                "check": "agentic_missing_post_attempt_measurement",
                "severity": "warning",
                "count": n,
                "detail": f"{n} completed agentic attempts have no post-attempt test run ID.",
            })
    return findings


def audit_agentic_identity_and_artifacts(df: pd.DataFrame) -> list[dict]:
    findings = []
    if "lane" not in df.columns:
        return findings
    agentic = df[df["lane"] == "agentic"]
    if agentic.empty:
        return findings
    if "tool_attempt_id" not in agentic.columns:
        findings.append({
            "check": "missing_tool_attempt_id_column",
            "severity": "error",
            "count": len(agentic),
            "detail": "Agentic analysis requires tool_attempt_id.",
        })
    else:
        missing = agentic["tool_attempt_id"].isna() | agentic["tool_attempt_id"].astype(str).str.strip().eq("")
        if missing.any():
            findings.append({
                "check": "missing_tool_attempt_id",
                "severity": "error",
                "count": int(missing.sum()),
                "detail": "Agentic rows must have non-empty tool_attempt_id values.",
            })
    if "tool_artifact_path" in agentic.columns:
        missing_artifact = agentic["tool_artifact_path"].isna() | agentic["tool_artifact_path"].astype(str).str.strip().isin(["", "nan"])
        if missing_artifact.any():
            findings.append({
                "check": "agentic_missing_artifact_path",
                "severity": "warning",
                "count": int(missing_artifact.sum()),
                "detail": "Agentic attempts are missing artifact paths for qualitative drill-down.",
            })
    return findings


def audit_outcome_classification(df: pd.DataFrame) -> list[dict]:
    if "outcome_classification" not in df.columns:
        return [{
            "check": "missing_outcome_classification",
            "severity": "error",
            "count": len(df),
            "detail": "Normalized attempts must include outcome_classification.",
        }]
    missing = df["outcome_classification"].isna() | df["outcome_classification"].astype(str).str.strip().eq("")
    if missing.any():
        return [{
            "check": "unclassified_attempts",
            "severity": "warning",
            "count": int(missing.sum()),
            "detail": "Some attempts could not be assigned a normalized outcome classification.",
        }]
    return []


def audit_generated_test_links(df: pd.DataFrame) -> list[dict]:
    findings = []
    if "generated_test_count" not in df.columns:
        return findings
    agentic = df[df.get("lane", pd.Series("", index=df.index)) == "agentic"]
    if agentic.empty:
        return findings
    changed = agentic.get("produced_change", pd.Series(False, index=agentic.index)).fillna(False)
    missing_links = changed & pd.to_numeric(agentic["generated_test_count"], errors="coerce").fillna(0).eq(0)
    if missing_links.any():
        findings.append({
            "check": "agentic_missing_generated_test_links",
            "severity": "warning",
            "count": int(missing_links.sum()),
            "detail": "Agentic attempts with changes have no generated/linked tests.",
        })
    return findings


def audit_missing_repo_metadata(df: pd.DataFrame) -> list[dict]:
    findings = []
    for col in ("repo_owner", "repo_name", "commit_hash"):
        if col in df.columns:
            n = int(df[col].isna().sum()) + int((df[col] == "").sum())
            if n > 0:
                findings.append({
                    "check": f"missing_{col}",
                    "severity": "warning",
                    "count": n,
                    "detail": f"{n} attempts are missing {col}.",
                })
    return findings


# ---------------------------------------------------------------------------
# Report builder
# ---------------------------------------------------------------------------

def build_audit_report(findings: list[dict], df: pd.DataFrame) -> dict:
    errors = [f for f in findings if f["severity"] == "error"]
    warnings = [f for f in findings if f["severity"] == "warning"]
    infos = [f for f in findings if f["severity"] == "info"]
    return {
        "summary": {
            "total_attempts": len(df),
            "error_count": len(errors),
            "warning_count": len(warnings),
            "info_count": len(infos),
            "pass": len(errors) == 0,
        },
        "findings": findings,
    }


def _write_markdown_report(report: dict, path: Path) -> None:
    lines = ["# Audit Report\n"]
    summary = report["summary"]
    lines += [
        f"- Total attempts: {summary['total_attempts']}",
        f"- Errors: {summary['error_count']}",
        f"- Warnings: {summary['warning_count']}",
        f"- Infos: {summary['info_count']}",
        f"- Pass: {'yes' if summary['pass'] else 'NO — errors found'}",
        "",
    ]
    for f in report["findings"]:
        icon = {"error": "❌", "warning": "⚠️", "info": "ℹ️"}.get(f["severity"], "•")
        lines.append(f"{icon} **{f['check']}** (n={f['count']}): {f['detail']}")
    path.write_text("\n".join(lines), encoding="utf-8")


def run(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
    output_dir: str,
) -> None:
    """Entry point for the ``audit`` CLI command."""
    out = ensure_output_dir(output_dir)

    raw = read_results_csvs(list(results))
    df = normalize_attempts(raw)

    findings: list[dict] = []
    findings += audit_lane_labels(df)
    findings += audit_missing_coverage(df)
    findings += audit_missing_mutation(df)
    findings += audit_missing_tokens(df)
    findings += audit_duplicate_rows(df)
    findings += audit_agentic_identity_and_artifacts(df)
    findings += audit_agentic_no_post_attempt(df)
    findings += audit_generated_test_links(df)
    findings += audit_outcome_classification(df)
    findings += audit_missing_repo_metadata(df)

    report = build_audit_report(findings, df)

    with open(out / "audit_report.json", "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, default=str)

    pd.DataFrame(findings).to_csv(out / "audit_report.csv", index=False)
    _write_markdown_report(report, out / "audit_report.md")

    status = "PASS" if report["summary"]["pass"] else "FAIL"
    print(f"Audit {status}: {report['summary']['error_count']} errors, "
          f"{report['summary']['warning_count']} warnings. "
          f"Report written to {out}")
