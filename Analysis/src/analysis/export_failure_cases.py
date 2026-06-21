"""Export a qualitative failure dataset for later open coding.

Outputs:
  failure_cases.csv
  failure_cases.jsonl
  cases/case_XXXXX/     (when --markdown is passed)
    case.md
    source_member.cs
    existing_test.cs      (if available)
    patch.diff            (agentic: copied; LLM: constructed from patch_json)
    prompt.md             (agentic only)
    {tool}.events.jsonl   (agentic only)
    {tool}.stderr.log     (agentic only)
    generated_test.json   (LLM only)
    modified_file.cs      (LLM only)
    test_run.log          (LLM: when test_run FK available)
"""

from __future__ import annotations

import shutil
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import (
    ensure_output_dir,
    find_artifact_dir,
    read_log_excerpt,
    read_results_csvs,
    read_text_artifact,
)
from analysis.normalize import normalize_attempts
from analysis.schema import FAILURE_CASE_FIELDS, LANE_AGENTIC, PRELIMINARY_FAILURE_LABELS


CASE_MARKDOWN_TEMPLATE = """\
# Failure Case {failure_case_id}

**Repo:** {repo_owner}/{repo_name}
**Commit:** {commit_hash}
**Lane:** {lane}
**Producer:** {producer_id}
**Model/Tool:** {model_or_tool}
**Source method:** `{source_method_name}`
**Signature:** `{source_method_signature}`
**Location:** `{source_file_path}:{source_line}`
**Failure label:** `{preliminary_failure_label}`
**Failure kind:** `{failure_kind}`
**Failure stage:** `{failure_stage}`

## Outcome Summary

{outcome_summary}

## Artifacts

| File | Description |
|------|-------------|
| `source_class.cs` | Full class containing the source method |
| `existing_test_class.cs` | Full test class used as context (if any) |
{artifact_table_rows}

## Open Coding Notes

- **Open code 1:**
- **Open code 2:**
- **Open code 3:**
- **Coder:**
- **Coded at:**
"""

_AGENTIC_ARTIFACT_ROWS = """\
| `patch.diff` | What the tool changed |
| `prompt.md` | Task prompt given to the tool |
| `{tool}.events.jsonl` | Full agent event trace |
| `{tool}.stderr.log` | Tool stderr output |"""

_LLM_ARTIFACT_ROWS = """\
| `prompt.md` | Full chained LLM prompt (FinalTest step) |
| `generated_test.json` | Structured test generation output |
| `modified_file.cs` | Full generated test file |"""


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
        constraint = str(row.get("tool_validation_outcome", "")).lower() == "constraintviolation"
        deleted = row.get("deleted_files_count", 0)

        if "nochange" in tool_status or "no_change" in obs_outcome or failure_kind == "no_change":
            return "no_change"
        if "timedout" in tool_status or "timeout" in obs_outcome or failure_kind == "timeout":
            return "timeout"
        if "toolcrashed" in tool_status or "toolfailed" in obs_outcome or failure_kind == "tool_crash":
            return "tool_crash"
        if constraint:
            return "overbroad_change"
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
        if str(deleted or 0) != "0" and str(deleted or 0) != "nan":
            return "overbroad_change"
        return "unclassified"

    out["preliminary_failure_label"] = out.apply(_label, axis=1)
    return out


def _truncate(d: dict, key: str, max_chars: int) -> str:
    val = d.get(key) or ""
    return str(val)[:max_chars] if val else ""


def _str(val) -> str:
    return "" if (val is None or (isinstance(val, float) and val != val)) else str(val)


def build_failure_cases(
    attempts_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
) -> pd.DataFrame:
    """Build failure case rows from the normalized attempts dataset."""
    if attempts_df.empty:
        return pd.DataFrame()

    validated = attempts_df.get("validated_success", pd.Series(False, index=attempts_df.index)).fillna(False)
    case_mask = ~validated
    failed = attempts_df[case_mask].copy()

    failed = assign_preliminary_labels(failed)
    failed["failure_case_id"] = range(1, len(failed) + 1)

    # Pre-load DB artifacts.
    # Keys are (db_key, id) to avoid collisions when member IDs repeat across repos.
    # db_key = Path(db_path).parent.name, e.g. "consulthunter-TestMap-Example"
    llm_artifacts: dict[tuple, dict] = {}   # (db_key, attempt_id)
    source_members: dict[tuple, dict] = {}  # (db_key, member_id)
    existing_tests: dict[tuple, dict] = {}  # (db_key, source_member_id)

    if db_paths:
        from analysis.db import (
            connect,
            get_existing_tests_by_source_member,
            get_llm_attempt_artifacts,
            get_llm_final_prompts,
            get_members_with_source,
        )
        from analysis.files import find_databases

        for db_path in find_databases(list(db_paths)):
            db_key = Path(db_path).parent.name
            try:
                with connect(db_path) as conn:
                    prompts = {
                        int(r["attempt_id"]): r["prompt"]
                        for _, r in get_llm_final_prompts(conn).iterrows()
                        if pd.notna(r.get("attempt_id"))
                    }
                    for _, r in get_llm_attempt_artifacts(conn).iterrows():
                        aid = r.get("attempt_id")
                        if aid is not None and pd.notna(aid):
                            row_dict = r.to_dict()
                            row_dict["_prompt"] = prompts.get(int(aid), "")
                            llm_artifacts[(db_key, int(aid))] = row_dict

                    for _, r in get_members_with_source(conn).iterrows():
                        mid = r.get("id")
                        if mid is not None and pd.notna(mid):
                            source_members[(db_key, int(mid))] = r.to_dict()

                    for _, r in get_existing_tests_by_source_member(conn).iterrows():
                        smid = r.get("source_member_id")
                        if smid is not None and pd.notna(smid):
                            existing_tests[(db_key, int(smid))] = r.to_dict()

            except Exception as exc:
                print(f"[warn] Could not load DB artifacts from {db_path}: {exc}")

    def _enrich(row: pd.Series) -> pd.Series:
        lane = str(row.get("lane", ""))

        # Source member and existing test (both lanes, from DB)
        # db_key derived from "{repo_owner}-{repo_name}" must match Path(db_path).parent.name
        repo_owner = _str(row.get("repo_owner", ""))
        repo_name  = _str(row.get("repo_name", ""))
        db_key     = f"{repo_owner}-{repo_name}"
        smid       = row.get("source_member_id")
        sm = source_members.get((db_key, int(smid))) if smid is not None and pd.notna(smid) else None
        et = existing_tests.get((db_key, int(smid))) if smid is not None and pd.notna(smid) else None
        row["_source_member_class_source"] = _str(sm.get("class_source") if sm else "")
        row["_source_member_file_path"]    = _str(sm.get("file_path") if sm else "")
        row["_existing_test_class_source"] = _str(et.get("existing_test_class_source") if et else "")
        row["_existing_test_file_path"]    = _str(et.get("existing_test_file_path") if et else "")

        if lane == LANE_AGENTIC:
            raw_path = row.get("tool_artifact_path") or row.get("artifact_path")
            if raw_path and _str(raw_path) not in ("", "nan"):
                artifact_dir: Optional[Path] = Path(_str(raw_path))
                if not artifact_dir.is_dir():
                    artifact_dir = None
            else:
                artifact_dir = find_artifact_dir(
                    artifacts_root,
                    experiment_id=row.get("experiment_run_id", ""),
                    candidate_id=row.get("candidate_method_id", ""),
                    tool_id=_str(row.get("tool_id", "")),
                )

            tool_id = _str(row.get("tool_id", "")).lower()
            log_file    = f"{tool_id}.stderr.log" if tool_id else "stderr.log"
            events_file = f"{tool_id}.events.jsonl" if tool_id else "events.jsonl"

            row["prompt_excerpt"]         = read_text_artifact(artifact_dir, "prompt.md", max_chars=1000)
            row["generated_code_excerpt"] = read_text_artifact(artifact_dir, "patch.diff", max_chars=2000)
            row["logs_excerpt"]           = read_text_artifact(artifact_dir, log_file, max_chars=2000)
            row["response_excerpt"]       = read_text_artifact(artifact_dir, events_file, max_chars=4000)
            row["diagnostics_excerpt"]    = read_text_artifact(artifact_dir, "changed-files.txt", max_chars=500)
            row["artifact_path"]          = str(artifact_dir) if artifact_dir else ""
            row["raw_log_path"]           = str(artifact_dir / log_file) if artifact_dir else ""
            row["modified_file_path"]     = ""
            row["_artifact_dir"]          = str(artifact_dir) if artifact_dir else ""
            row["_tool_id_lower"]         = tool_id

        else:  # LLM lane
            attempt_id = row.get("generation_attempt_id") or row.get("attempt_id")
            db_row = llm_artifacts.get((db_key, int(attempt_id))) if pd.notna(attempt_id) else None

            row["prompt_excerpt"]         = ""
            row["generated_code_excerpt"] = _truncate(db_row, "generated_test_code", 2000) if db_row else ""
            row["response_excerpt"]       = _truncate(db_row, "modified_file_contents", 2000) if db_row else ""
            row["modified_file_path"]     = _str(db_row.get("modified_file_path") if db_row else "")
            row["diagnostics_excerpt"]    = ""
            row["artifact_path"]          = ""
            row["_artifact_dir"]          = ""
            row["_db_row"]                = db_row or {}

            log_path = _str(db_row.get("log_path") if db_row else "")
            if log_path and Path(log_path).exists():
                row["logs_excerpt"] = read_log_excerpt(log_path, max_lines=50)
                row["raw_log_path"] = log_path
            else:
                row["logs_excerpt"] = ""
                row["raw_log_path"] = log_path

        row["case_markdown_path"] = ""
        row["qualitative_notes"]  = ""
        row["open_code_1"]        = ""
        row["open_code_2"]        = ""
        row["open_code_3"]        = ""
        row["coder"]              = ""
        row["coded_at"]           = ""
        return row

    failed = failed.apply(_enrich, axis=1)
    return failed


def write_case_dir(case: pd.Series, cases_root: Path) -> Path:
    """Write a case directory with all artifacts and return its path."""
    case_id = int(case.get("failure_case_id", 0))
    case_dir = cases_root / f"case_{case_id:05d}"
    case_dir.mkdir(parents=True, exist_ok=True)

    lane    = str(case.get("lane", ""))
    tool_id = str(case.get("_tool_id_lower", "") or case.get("tool_id", "") or "").lower()

    # --- source_class.cs ---
    source_code = _str(case.get("_source_member_class_source", ""))
    if source_code:
        (case_dir / "source_class.cs").write_text(source_code, encoding="utf-8")

    # --- existing_test_class.cs ---
    existing_test = _str(case.get("_existing_test_class_source", ""))
    if existing_test:
        (case_dir / "existing_test_class.cs").write_text(existing_test, encoding="utf-8")

    if lane == LANE_AGENTIC:
        artifact_dir_str = _str(case.get("_artifact_dir", ""))
        artifact_dir = Path(artifact_dir_str) if artifact_dir_str else None

        for filename in [
            "prompt.md",
            "patch.diff",
            f"{tool_id}.events.jsonl",
            f"{tool_id}.stderr.log",
            "changed-files.txt",
        ]:
            if artifact_dir and (artifact_dir / filename).exists():
                shutil.copy2(artifact_dir / filename, case_dir / filename)

    else:  # LLM
        db_row: dict = case.get("_db_row") or {}

        prompt = _str(db_row.get("_prompt", ""))
        if prompt:
            (case_dir / "prompt.md").write_text(prompt, encoding="utf-8")

        patch_json = _str(db_row.get("patch_json", ""))
        if patch_json:
            (case_dir / "generated_test.json").write_text(patch_json, encoding="utf-8")

        # Full file contents — untruncated
        modified_contents = _str(db_row.get("modified_file_contents", ""))
        if modified_contents:
            (case_dir / "modified_file.cs").write_text(modified_contents, encoding="utf-8")

        log_path = _str(db_row.get("log_path", ""))
        if log_path and Path(log_path).exists():
            shutil.copy2(log_path, case_dir / "test_run.log")

    # --- case.md ---
    artifact_table_rows = (
        _AGENTIC_ARTIFACT_ROWS.format(tool=tool_id) if lane == LANE_AGENTIC else _LLM_ARTIFACT_ROWS
    )
    commit = _str(case.get("commit_hash", ""))
    content = CASE_MARKDOWN_TEMPLATE.format(
        failure_case_id=case_id,
        repo_owner=_str(case.get("repo_owner", "")),
        repo_name=_str(case.get("repo_name", "")),
        commit_hash=commit[:12] if commit else "",
        lane=lane,
        producer_id=_str(case.get("producer_id", "")),
        model_or_tool=_str(case.get("model", "")) or _str(case.get("tool_id", "")),
        source_method_name=_str(case.get("source_method_name", "")),
        source_method_signature=_str(case.get("source_method_signature", "")),
        source_file_path=_str(case.get("source_file_path", "")),
        source_line=_str(case.get("source_line", "")),
        preliminary_failure_label=_str(case.get("preliminary_failure_label", "")),
        failure_kind=_str(case.get("failure_kind", "")),
        failure_stage=_str(case.get("failure_stage", "")),
        outcome_summary=_str(case.get("outcome_summary", "")),
        artifact_table_rows=artifact_table_rows,
    )
    (case_dir / "case.md").write_text(content, encoding="utf-8")

    return case_dir


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

    # Strip internal working columns before writing CSV/JSONL
    internal_cols = [c for c in sampled.columns if c.startswith("_")]
    export_df = sampled.drop(columns=internal_cols, errors="ignore")

    export_df.to_csv(out / "failure_cases.csv", index=False)
    export_df.to_json(out / "failure_cases.jsonl", orient="records", lines=True)

    if write_markdown:
        cases_root = out / "cases"
        if cases_root.exists():
            shutil.rmtree(cases_root)
        cases_root.mkdir()
        for _, row in sampled.iterrows():
            case_dir = write_case_dir(row, cases_root)
            sampled.loc[row.name, "case_markdown_path"] = str(case_dir / "case.md")
        export_df = sampled.drop(columns=internal_cols, errors="ignore")
        export_df.to_csv(out / "failure_cases.csv", index=False)
        export_df.to_json(out / "failure_cases.jsonl", orient="records", lines=True)
        print(f"  case directories written to {cases_root}")

    print(f"Failure cases written to {out} ({len(sampled):,} cases, strategy={sample})")
