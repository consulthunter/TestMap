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
from pathlib import Path, PureWindowsPath
from typing import Optional

import pandas as pd

from analysis.files import (
    ensure_output_dir,
    find_artifact_dir,
    read_log_excerpt,
    read_result_grains,
    read_schema4_result_bundle,
    read_text_artifact,
)
from analysis.build_evaluation_dataset import build_attempts_dataset
from analysis.normalize import chain_key_columns
from analysis.schema import (
    ASSERTION_RESULTS_SCHEMA_VERSION,
    FAILURE_CASE_FIELDS,
    LANE_AGENTIC,
    LEGACY_RESULTS_SCHEMA_VERSION,
    PRELIMINARY_FAILURE_LABELS,
)


CASE_MARKDOWN_TEMPLATE = """\
# Failure Case {failure_case_id}

**Repo:** {repo_owner}/{repo_name}
**Commit:** {commit_hash}
**Lane:** {lane}
**Producer:** {producer}
**Model:** {model}
**Attempt:** {attempt_number} (first attempt: {is_first_attempt}; chain later succeeded: {chain_succeeded})
**Source method:** `{source_method_name}`
**Signature:** `{source_method_signature}`
**Source file:** `{source_file_path}`
**Failure label:** `{preliminary_failure_label}`
**Outcome:** `{outcome_classification}`
**Failure kind:** `{failure_kind}`
**Failure stage:** `{failure_stage}`

## Failure Summary

{failure_summary}

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
        infrastructure = row.get("infrastructure_failure")
        if not pd.isna(infrastructure) and bool(infrastructure):
            return "infrastructure"
        tool_status = str(row.get("run_status", "") or row.get("tool_run_status", "")).lower()
        obs_outcome = str(row.get("observed_outcome", "") or row.get("tool_observed_outcome", "")).lower()
        failure_kind = str(row.get("failure_kind", "")).lower()
        failure_stage = str(row.get("failure_stage", "")).lower()
        prod_changed = row.get("production_files_changed", 0)
        proj_changed = row.get("project_files_changed", 0)
        validation = str(row.get("tool_validation_outcome", "")).lower()
        constraint = validation == "constraintviolation"
        deleted = row.get("deleted_files_count", 0)

        if "nochange" in tool_status or "no_change" in obs_outcome or failure_kind == "no_change":
            return "no_change"
        if "timedout" in tool_status or "timeout" in obs_outcome or failure_kind == "timeout":
            return "timeout"
        if "toolcrashed" in tool_status or "toolfailed" in obs_outcome or failure_kind == "tool_crash":
            return "tool_crash"
        if constraint:
            return "overbroad_change"
        if ("buildfailed" in tool_status or "buildfailed" in obs_outcome or failure_stage == "build"
                or failure_kind == "compilation" or validation == "buildfailed"):
            return "build_failed"
        if failure_stage == "compile" or failure_kind == "compile_error":
            return "compile_failed"
        if (failure_stage == "test" or failure_kind in {"runtime", "assertion"}
                or validation == "testsfailed"):
            return "test_failed"
        if failure_kind == "generation" or failure_stage == "generation":
            return "generation_failed"
        if str(prod_changed or 0) != "0" and str(prod_changed or 0) != "nan":
            return "production_code_modified"
        if str(proj_changed or 0) != "0" and str(proj_changed or 0) != "nan":
            return "project_file_modified"
        if str(deleted or 0) != "0" and str(deleted or 0) != "nan":
            return "overbroad_change"
        return "unclassified"

    out["preliminary_failure_label"] = out.apply(_label, axis=1)
    return out


def _db_key(owner: object, repo: object, commit: object) -> str:
    return "/".join(_str(part).strip().lower() for part in (owner, repo, commit))


def _db_key_for_path(db_path: str | Path) -> str:
    """Key a database by the owner/repo/commit folders it sits in."""
    commit_dir = Path(db_path).parent
    return _db_key(commit_dir.parent.parent.name, commit_dir.parent.name, commit_dir.name)


def _relocate(raw_path: object, commit_dir: Optional[Path]) -> str:
    """Resolve a recorded artifact path, re-rooting it under the database's folder.

    Paths are recorded where the run happened. When the output has been moved (for
    example into a replication package), the part after the commit folder still
    matches, so it is joined onto the folder the database now sits in.
    """
    path = _str(raw_path)
    if not path or commit_dir is None or Path(path).exists():
        return path
    parts = _path_parts(path)
    if commit_dir.name in parts:
        moved = commit_dir.joinpath(*parts[parts.index(commit_dir.name) + 1:])
        if moved.exists():
            return str(moved)
    return path


def _relocate_log(raw_path: object, logs_root: Optional[Path]) -> str:
    """Re-root a recorded run-log path under *logs_root*, the folder of dated log folders.

    Run logs are recorded under the producer's ``Logs`` folder; the part after the
    last ``Logs`` segment is joined onto *logs_root*.
    """
    path = _str(raw_path)
    if not path or logs_root is None or Path(path).exists():
        return path
    parts = _path_parts(path)
    lowered = [part.lower() for part in parts]
    if "logs" in lowered:
        last = len(lowered) - 1 - lowered[::-1].index("logs")
        moved = logs_root.joinpath(*parts[last + 1:])
        if moved.exists():
            return str(moved)
    return path


def _path_parts(path: str) -> tuple[str, ...]:
    """Split a recorded path, whichever platform recorded it."""
    return PureWindowsPath(path).parts if "\\" in path else Path(path).parts


def _tool_file(artifact_dir: Optional[Path], tool_id: str, suffix: str) -> str:
    """Name of a tool's ``*<suffix>`` artifact.

    Tools write these under their family name (``openhands.events.jsonl`` for
    ``openhands-custom-kimi``), so fall back to the one file with that suffix.
    """
    preferred = f"{tool_id}{suffix}" if tool_id else suffix.lstrip(".")
    if artifact_dir is None or (artifact_dir / preferred).exists():
        return preferred
    matches = sorted(artifact_dir.glob(f"*{suffix}"))
    return matches[0].name if matches else preferred


def _truncate(d: dict, key: str, max_chars: int) -> str:
    val = d.get(key) or ""
    return str(val)[:max_chars] if val else ""


def _str(val) -> str:
    return "" if (val is None or (isinstance(val, float) and val != val)) else str(val)


def build_failure_cases(
    attempts_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
    include_infrastructure: bool = False,
    logs_root: Optional[str] = None,
) -> pd.DataFrame:
    """Build failure case rows from the normalized attempts dataset.

    A case is an attempt that did not validate. Infrastructure failures carry no
    model or tool behaviour to code and are left out unless *include_infrastructure*.
    Each case records whether it is its chain's first attempt and whether the chain
    (candidate, lane, producer) later succeeded, so a coder can tell a repaired
    intermediate step from a terminal failure.

    Recorded artifact paths are re-rooted under each database's folder, and run-log
    paths under *logs_root*, so a moved output (a replication package) still resolves.
    """
    if attempts_df.empty:
        return pd.DataFrame()
    logs = Path(logs_root) if logs_root else None

    validated = (attempts_df.get("validated_success", pd.Series(False, index=attempts_df.index))
                 .astype("boolean").fillna(False).astype(bool))
    infrastructure = (attempts_df["infrastructure_failure"].astype("boolean").fillna(False).astype(bool)
                      if "infrastructure_failure" in attempts_df.columns
                      else pd.Series(False, index=attempts_df.index))

    chain_cols = chain_key_columns(attempts_df)
    if chain_cols:
        chain = [attempts_df[c] for c in chain_cols]
        chain_succeeded = validated.groupby(chain, dropna=False).transform("any")
        number = pd.to_numeric(
            attempts_df.get("attempt_number", pd.Series(1, index=attempts_df.index)), errors="coerce")
        is_first = number.eq(number.groupby(chain, dropna=False).transform("min"))
    else:
        chain_succeeded = validated
        is_first = pd.Series(True, index=attempts_df.index)

    case_mask = ~validated & (include_infrastructure | ~infrastructure)
    failed = attempts_df[case_mask].copy()
    failed["chain_succeeded"] = chain_succeeded[case_mask]
    failed["is_first_attempt"] = is_first[case_mask]

    failed = assign_preliminary_labels(failed)
    failed["failure_case_id"] = range(1, len(failed) + 1)

    # Pre-load DB artifacts.
    # Keys are (db_key, id) to avoid collisions when member IDs repeat across repos.
    # db_key is "<owner>/<repo>/<commit>" from the Output/<owner>/<repo>/<commit>/analysis.db layout.
    llm_artifacts: dict[tuple, dict] = {}   # (db_key, attempt_id)
    source_members: dict[tuple, dict] = {}  # (db_key, member_id)
    existing_tests: dict[tuple, dict] = {}  # (db_key, source_member_id)
    tool_post_logs: dict[tuple, str] = {}   # (db_key, tool_attempt_id) -> post-attempt log_path
    db_dirs: dict[str, Path] = {}           # db_key -> folder holding analysis.db

    if db_paths:
        from analysis.db import (
            connect,
            get_existing_tests_by_source_member,
            get_llm_attempt_artifacts,
            get_llm_final_prompts,
            get_members_with_source,
            get_tool_attempt_post_logs,
        )
        from analysis.files import find_databases

        for db_path in find_databases(list(db_paths)):
            db_key = _db_key_for_path(db_path)
            db_dirs[db_key] = Path(db_path).parent
            try:
                with connect(db_path) as conn:
                    for _, r in get_tool_attempt_post_logs(conn).iterrows():
                        taid = r.get("tool_attempt_id")
                        if taid is not None and pd.notna(taid):
                            tool_post_logs[(db_key, int(taid))] = _str(r.get("log_path", ""))
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
        db_key = _db_key(row.get("repo_owner"), row.get("repo_name"), row.get("commit_hash"))
        commit_dir = db_dirs.get(db_key)
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
                artifact_dir: Optional[Path] = Path(_relocate(raw_path, commit_dir))
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
            log_file    = _tool_file(artifact_dir, tool_id, ".stderr.log")
            events_file = _tool_file(artifact_dir, tool_id, ".events.jsonl")

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
            taid = row.get("tool_attempt_id")
            row["_post_attempt_log_path"] = (
                _relocate_log(_relocate(tool_post_logs.get((db_key, int(float(taid))), ""), commit_dir), logs)
                if taid is not None and pd.notna(taid) else "")

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
            log_path = _relocate_log(_relocate(db_row.get("log_path") if db_row else "", commit_dir), logs)
            if db_row:
                db_row = {**db_row, "log_path": log_path}
            row["_db_row"]                = db_row or {}

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
            _tool_file(artifact_dir, tool_id, ".events.jsonl"),
            _tool_file(artifact_dir, tool_id, ".stderr.log"),
            "changed-files.txt",
        ]:
            if artifact_dir and (artifact_dir / filename).exists():
                shutil.copy2(artifact_dir / filename, case_dir / filename)

        # Post-attempt (integration) test-run log — the run where the applied test was
        # built and executed; symmetric with the LLM lane's test_run.log.
        post_log = _str(case.get("_post_attempt_log_path", ""))
        if post_log and Path(post_log).exists():
            shutil.copy2(post_log, case_dir / "test_run.log")

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
        producer=_str(case.get("producer", "")) or _str(case.get("tool_id", "")),
        model=_str(case.get("model", "")),
        attempt_number=_str(case.get("attempt_number", "")),
        is_first_attempt=_str(case.get("is_first_attempt", "")),
        chain_succeeded=_str(case.get("chain_succeeded", "")),
        source_method_name=_str(case.get("source_method_name", "")),
        source_method_signature=_str(case.get("source_method_signature", "")),
        source_file_path=_str(case.get("_source_member_file_path", "")),
        preliminary_failure_label=_str(case.get("preliminary_failure_label", "")),
        outcome_classification=_str(case.get("outcome_classification", "")),
        failure_kind=_str(case.get("failure_kind", "")),
        failure_stage=_str(case.get("failure_stage", "")),
        failure_summary=_str(case.get("failure_summary", "")) or "(none recorded)",
        artifact_table_rows=artifact_table_rows,
    )
    (case_dir / "case.md").write_text(content, encoding="utf-8")

    return case_dir


# Labels that mean the producer changed something outside the test it was asked to
# write. These are correctness risks; ordinary build or test failures are not.
SAFETY_FAILURE_LABELS = frozenset({
    "overbroad_change", "production_code_modified", "project_file_modified",
})

DEFAULT_SAMPLE_SEED = 20260914


def _stratified(df: pd.DataFrame, column: str, per_group: int, seed: int) -> pd.DataFrame:
    """Up to *per_group* rows per value of *column* (all rows when 0), reproducibly."""
    shuffled = df.sample(frac=1, random_state=seed)
    picked = shuffled.groupby(column, dropna=False, sort=False).head(per_group) if per_group else shuffled
    return picked.sort_index()


def sample_cases(
    failure_df: pd.DataFrame,
    strategy: str = "all",
    top_n: int = 0,
    seed: int = DEFAULT_SAMPLE_SEED,
) -> pd.DataFrame:
    """Apply a sampling strategy to the failure case DataFrame.

    Stratified strategies draw up to *top_n* cases per stratum (all when 0) with a
    fixed *seed*, so the same coding sample can be drawn again.
    """
    if failure_df.empty:
        return failure_df

    if strategy == "all":
        return failure_df
    if strategy == "stratified-lane":
        return _stratified(failure_df, "lane", top_n, seed)
    if strategy == "stratified-label":
        return _stratified(failure_df, "preliminary_failure_label", top_n, seed)
    if strategy == "stratified-tool":
        return _stratified(failure_df, "tool_id", top_n, seed)
    if strategy == "top-n":
        counts = failure_df["preliminary_failure_label"].value_counts()
        top_labels = counts.head(top_n or 5).index
        return failure_df[failure_df["preliminary_failure_label"].isin(top_labels)]
    if strategy == "high-severity":
        return failure_df[failure_df["preliminary_failure_label"].isin(SAFETY_FAILURE_LABELS)]
    if strategy == "first-attempt":
        return failure_df[failure_df["is_first_attempt"].astype("boolean").fillna(False).astype(bool)]
    if strategy == "lane-llm":
        return failure_df[failure_df.get("lane", pd.Series()) == "llm"]
    if strategy == "lane-agentic":
        return failure_df[failure_df.get("lane", pd.Series()) == "agentic"]
    return failure_df


def run(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: Optional[str],
    output_dir: str,
    sample: str = "all",
    top_n: int = 0,
    write_markdown: bool = False,
    results_schema_version: str = ASSERTION_RESULTS_SCHEMA_VERSION,
    include_infrastructure: bool = False,
    seed: int = DEFAULT_SAMPLE_SEED,
    logs_root: Optional[str] = None,
) -> None:
    """Entry point for the ``export-failures`` CLI command."""
    out = ensure_output_dir(output_dir)

    # Same schema dispatch as build-datasets and audit: schema 4 is the default,
    # schema 3 is the explicit legacy path.
    if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION:
        raw, generated, _, _ = read_schema4_result_bundle(
            list(results), require_assertion_sidecar=False
        )
    elif results_schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        raw, generated, _ = read_result_grains(
            list(results), expected_schema_version=LEGACY_RESULTS_SCHEMA_VERSION
        )
    else:
        raise ValueError(f"Unsupported results_schema_version: {results_schema_version}.")
    attempts = build_attempts_dataset(raw, generated)
    cases = build_failure_cases(attempts, db_paths, artifacts_root,
                                include_infrastructure=include_infrastructure,
                                logs_root=logs_root)
    sampled = sample_cases(cases, strategy=sample, top_n=top_n, seed=seed)

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
