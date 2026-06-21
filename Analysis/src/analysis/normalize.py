"""Normalization functions for building clean evaluation datasets.

These functions operate on DataFrames loaded from result CSVs and produce
the standardized tables that notebooks consume.

Key design constraints
----------------------
- The raw CSV has **one row per generated test** for the agentic lane and
  **one row per attempt** (= one generated test) for the LLM lane.
- ``normalize_attempts`` handles the full pipeline and returns a DataFrame
  with one row per *attempt* for both lanes.
- ``build_generated_tests_dataset`` returns the un-collapsed per-test grain.
"""

from __future__ import annotations

import json

import pandas as pd

from analysis.schema import (
    AGENTIC_ATTEMPT_KEY_FIELDS,
    CANDIDATE_KEY_FIELDS,
    LANE_AGENTIC,
    LANE_LLM,
    RAW_COLUMN_RENAMES,
    REPOSITORY_KEY_FIELDS,
    WINNER_LABELS,
)

# ---------------------------------------------------------------------------
# Lane normalization
# ---------------------------------------------------------------------------

_LANE_MAP: dict[str, str] = {
    # LLM lane raw values
    "llm": LANE_LLM,
    "testmap": LANE_LLM,
    "direct": LANE_LLM,
    # Agentic lane raw values
    "agentic": LANE_AGENTIC,
    "tool": LANE_AGENTIC,
    "agent": LANE_AGENTIC,
    "agent_tool": LANE_AGENTIC,
    "agent-tool": LANE_AGENTIC,
}


def normalize_lane(raw: str) -> str:
    """Map a raw lane string to the canonical ``llm`` or ``agentic`` value."""
    return _LANE_MAP.get(str(raw).strip().lower(), raw)


# ---------------------------------------------------------------------------
# Column renaming
# ---------------------------------------------------------------------------

def rename_raw_columns(df: pd.DataFrame) -> pd.DataFrame:
    """Apply ``RAW_COLUMN_RENAMES`` to a DataFrame in place (returns copy)."""
    rename_map = {k: v for k, v in RAW_COLUMN_RENAMES.items() if k in df.columns}
    return df.rename(columns=rename_map) if rename_map else df.copy()


# ---------------------------------------------------------------------------
# Key construction
# ---------------------------------------------------------------------------

def make_candidate_key(row: pd.Series) -> str:
    """Build a stable pipe-delimited key for a candidate (repo + source member)."""
    parts = [str(row.get(f, "")) for f in CANDIDATE_KEY_FIELDS]
    return "|".join(parts)


def make_repository_key(row: pd.Series) -> str:
    """Build a stable pipe-delimited key for a repository slice (repo + commit)."""
    parts = [str(row.get(f, "")) for f in REPOSITORY_KEY_FIELDS]
    return "|".join(parts)


def _assign_attempt_ids(df: pd.DataFrame) -> None:
    """Assign canonical attempt IDs in-place.

    LLM rows use ``generation_attempt_id``. Agentic rows require
    ``tool_attempt_id``; no legacy fallback is supported.
    """
    if "attempt_id" not in df.columns:
        df["attempt_id"] = pd.NA

    lane = df.get("lane", pd.Series("", index=df.index, dtype=str))
    llm_mask = lane == LANE_LLM
    agentic_mask = lane == LANE_AGENTIC

    if llm_mask.any():
        if "generation_attempt_id" not in df.columns:
            raise ValueError("LLM rows require generation_attempt_id.")
        df.loc[llm_mask, "attempt_id"] = (
            "llm:" + df.loc[llm_mask, "generation_attempt_id"].astype(str).str.strip()
        )

    if agentic_mask.any():
        # Derive tool_attempt_id from tool_artifact_path when the column is absent.
        # tool_artifact_path is unique per attempt in the current CSV format.
        if "tool_attempt_id" not in df.columns:
            if "tool_artifact_path" in df.columns:
                df["tool_attempt_id"] = df["tool_artifact_path"].astype(str).str.strip()
            else:
                raise ValueError(
                    "Agentic rows require tool_attempt_id (or tool_artifact_path as fallback)."
                )
        missing = df.loc[agentic_mask, "tool_attempt_id"].isna() | (
            df.loc[agentic_mask, "tool_attempt_id"].astype(str).str.strip() == ""
        )
        if missing.any():
            # Fill remaining blanks from tool_artifact_path if available
            if "tool_artifact_path" in df.columns:
                fill = df.loc[agentic_mask & missing, "tool_artifact_path"].astype(str).str.strip()
                df.loc[agentic_mask & missing, "tool_attempt_id"] = fill
                missing = df.loc[agentic_mask, "tool_attempt_id"].isna() | (
                    df.loc[agentic_mask, "tool_attempt_id"].astype(str).str.strip() == ""
                )
            if missing.any():
                raise ValueError("Agentic rows require non-empty tool_attempt_id values.")
        df.loc[agentic_mask, "attempt_id"] = (
            "agentic:" + df.loc[agentic_mask, "tool_attempt_id"].astype(str).str.strip()
        )


# ---------------------------------------------------------------------------
# Computed columns
# ---------------------------------------------------------------------------

OUTCOME_VALIDATED_EVIDENCE_POSITIVE = "ValidatedEvidencePositive"
OUTCOME_VALIDATED_LOW_IMPACT = "ValidatedLowImpact"
OUTCOME_FAILED_EVIDENCE_POSITIVE = "FailedEvidencePositive"
OUTCOME_VALIDATION_FAILED = "ValidationFailed"
OUTCOME_NO_CHANGE = "NoChange"
OUTCOME_SKIPPED = "Skipped"
OUTCOME_TIMED_OUT = "TimedOut"
OUTCOME_TOOL_FAILED = "ToolFailed"
OUTCOME_BUILD_FAILED = "BuildFailed"
OUTCOME_TESTS_FAILED = "TestsFailed"
OUTCOME_CONSTRAINT_VIOLATION = "ConstraintViolation"
OUTCOME_NOT_EVALUATED = "NotEvaluated"

VALIDATED_OUTCOMES = {
    OUTCOME_VALIDATED_EVIDENCE_POSITIVE,
    OUTCOME_VALIDATED_LOW_IMPACT,
}

# positive_impact is VEP only: test passed AND metrics improved above the noise floor.
# FailedEvidencePositive is excluded — a failing test is not a positive outcome.
POSITIVE_IMPACT_OUTCOMES = {
    OUTCOME_VALIDATED_EVIDENCE_POSITIVE,
}

# Minimum metric delta required to count as an improvement (noise floor).
# Coverage is stored as a 0–1 fraction; 0.01 = 1 percentage point.
# Mutation score is stored as 0–100; 1.0 = 1 percentage point.
COVERAGE_DELTA_MIN: float = 0.01
MUTATION_DELTA_MIN: float = 1.0


def _is_blank(value: object) -> bool:
    return pd.isna(value) or str(value).strip() == ""


def _string_or_empty(value: object) -> str:
    return "" if pd.isna(value) else str(value).strip()


def _metric_improved_row(row: pd.Series) -> bool:
    cov = pd.to_numeric(pd.Series([row.get("coverage_delta")]), errors="coerce").iloc[0]
    if pd.notna(cov) and cov >= COVERAGE_DELTA_MIN:
        return True
    mut = pd.to_numeric(pd.Series([row.get("mutation_score_delta")]), errors="coerce").iloc[0]
    if pd.notna(mut) and mut >= MUTATION_DELTA_MIN:
        return True
    mutant = row.get("mutant_killed")
    if isinstance(mutant, bool):
        return mutant
    return str(mutant).strip().lower() in {"true", "1", "yes"}


def _compute_metric_improved(df: pd.DataFrame) -> pd.Series:
    if not any(c in df.columns for c in ("coverage_delta", "mutation_score_delta", "mutant_killed")):
        return pd.Series([False] * len(df), index=df.index, dtype="boolean")
    return df.apply(_metric_improved_row, axis=1).astype("boolean")


def _classify_row(row: pd.Series) -> object:
    observed = _string_or_empty(row.get("tool_observed_outcome"))
    if observed:
        return observed

    lane = _string_or_empty(row.get("lane"))
    metric_improved = bool(row.get("metric_improved", False))

    if lane == LANE_AGENTIC:
        status = _string_or_empty(row.get("tool_run_status")).lower()
        validation = _string_or_empty(row.get("tool_validation_outcome"))
        validation_lower = validation.lower()

        if not status and not validation:
            return pd.NA
        if status in {"completednochange", "nochange"} or validation_lower == "nochange":
            return OUTCOME_NO_CHANGE
        if status in {"timedout", "timeout"} or validation_lower == "timedout":
            return OUTCOME_TIMED_OUT
        if status in {"toolcrashed", "toolfailed"} or validation_lower == "toolfailed":
            return OUTCOME_TOOL_FAILED
        if validation_lower == "skipped":
            return OUTCOME_SKIPPED
        if validation_lower == "constraintviolation":
            return OUTCOME_CONSTRAINT_VIOLATION
        if validation_lower == "buildfailed":
            return OUTCOME_BUILD_FAILED
        if validation_lower == "testsfailed":
            return OUTCOME_FAILED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_TESTS_FAILED
        if validation_lower == "passed":
            return OUTCOME_VALIDATED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_VALIDATED_LOW_IMPACT
        if validation_lower == "notevaluated":
            return OUTCOME_NOT_EVALUATED

    has_llm_failure_signal = any(c in row.index for c in ("failure_kind", "failure_stage", "failure_category"))
    if not has_llm_failure_signal:
        return pd.NA

    failure_kind = _string_or_empty(row.get("failure_kind"))
    failure_stage = _string_or_empty(row.get("failure_stage")).lower()
    failure_category = _string_or_empty(row.get("failure_category")).lower()

    if failure_kind.lower() in {"none", ""}:
        if row.get("generated_test_passed") is True or _is_blank(row.get("failure_kind")) or failure_kind.lower() == "none":
            return OUTCOME_VALIDATED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_VALIDATED_LOW_IMPACT
    if failure_stage == "compile" or failure_kind.lower() in {"compilation", "compile_error"}:
        return OUTCOME_FAILED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_BUILD_FAILED
    if failure_stage == "test" or failure_kind.lower() in {"runtime", "assertion"}:
        return OUTCOME_FAILED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_TESTS_FAILED
    if failure_kind.lower() == "generation" or failure_stage == "application" or "application" in failure_category:
        return OUTCOME_VALIDATION_FAILED
    if failure_kind:
        return OUTCOME_FAILED_EVIDENCE_POSITIVE if metric_improved else OUTCOME_VALIDATION_FAILED
    return pd.NA


def _compute_outcome_classification(df: pd.DataFrame) -> pd.Series:
    """Return a unified outcome classification string for both lanes.

    Uses the pipeline's observed outcome when available, and falls back to
    lane-specific status fields for CSVs produced by the current result writer.
    """
    return df.apply(_classify_row, axis=1)


def _compute_validated_success(df: pd.DataFrame) -> pd.Series:
    """Return True when validation passed, including low-impact passing attempts."""
    if "outcome_classification" not in df.columns:
        temp = df.copy()
        if "metric_improved" not in temp.columns:
            temp["metric_improved"] = _compute_metric_improved(temp)
        oc = _compute_outcome_classification(temp)
    else:
        oc = df["outcome_classification"]
    result = oc.isin(VALIDATED_OUTCOMES)
    return result.where(oc.notna(), other=pd.NA).astype("boolean")


def _compute_positive_impact(df: pd.DataFrame) -> pd.Series:
    """Return True when the attempt produced a validated test that improved metrics (VEP only)."""
    if "outcome_classification" not in df.columns:
        temp = df.copy()
        if "metric_improved" not in temp.columns:
            temp["metric_improved"] = _compute_metric_improved(temp)
        oc = _compute_outcome_classification(temp)
    else:
        oc = df["outcome_classification"]
    result = oc.isin(POSITIVE_IMPACT_OUTCOMES)
    return result.where(oc.notna(), other=pd.NA).astype("boolean")


def _add_outcome_flags(df: pd.DataFrame) -> None:
    df["metric_improved"] = _compute_metric_improved(df)
    df["outcome_classification"] = _compute_outcome_classification(df)
    oc = df["outcome_classification"]
    df["validated_evidence_positive"] = oc.eq(OUTCOME_VALIDATED_EVIDENCE_POSITIVE).astype("boolean")
    df["validated_low_impact"] = oc.eq(OUTCOME_VALIDATED_LOW_IMPACT).astype("boolean")
    df["validated_success"] = _compute_validated_success(df)
    df["positive_impact"] = _compute_positive_impact(df)


def _parse_smell_string(s: object) -> tuple[int, str]:
    """Parse ``Name=count; Name=count`` smell string into (total_count, json_types).

    Returns (0, '[]') for null/empty input.
    """
    if not isinstance(s, str) or not s.strip():
        return 0, "[]"
    types: list[str] = []
    total = 0
    for part in s.split(";"):
        part = part.strip()
        if "=" in part:
            name, _, count_str = part.rpartition("=")
            try:
                total += int(count_str.strip())
            except ValueError:
                total += 1
            types.append(name.strip())
        elif part:
            types.append(part)
            total += 1
    return total, json.dumps(sorted(set(types)))


def _expand_smell_columns(df: pd.DataFrame, raw_col: str, count_col: str, types_col: str) -> None:
    """Parse a raw smell string column into count + JSON types columns in-place."""
    if raw_col not in df.columns:
        return
    parsed = df[raw_col].map(_parse_smell_string)
    df[count_col] = parsed.map(lambda t: t[0]).astype("Int64")
    df[types_col] = parsed.map(lambda t: t[1])


def _compute_produced_change(df: pd.DataFrame) -> pd.Series:
    """Return a boolean Series indicating whether the attempt produced *any* output.

    - **LLM**: ``failure_kind`` is not ``'Generation'`` (a test was at least written).
    - **Agentic**: ``changed_files_count > 0`` (files were actually modified),
      falling back to ``tool_run_status == 'Completed'`` when the count is absent.

    Returns a nullable-boolean Series aligned to *df.index*.
    """
    lane = df.get("lane", pd.Series("", index=df.index, dtype=str))
    result = pd.array([pd.NA] * len(df), dtype="boolean")

    llm_mask = lane == LANE_LLM
    agentic_mask = lane == LANE_AGENTIC

    if "failure_kind" in df.columns:
        failures = df["failure_kind"].fillna("").astype(str).str.strip().str.lower()
        stages = df.get("failure_stage", pd.Series("", index=df.index)).fillna("").astype(str).str.strip().str.lower()
        llm_produced = ~(failures.isin(["generation"]) | stages.isin(["application"]))
        result[llm_mask.values] = llm_produced[llm_mask].values

    if "changed_files_count" in df.columns:
        ag_produced = pd.to_numeric(df["changed_files_count"], errors="coerce").fillna(0).gt(0)
        result[agentic_mask.values] = ag_produced[agentic_mask].values
    elif "tool_run_status" in df.columns:
        ag_produced = df["tool_run_status"].fillna("").eq("Completed")
        result[agentic_mask.values] = ag_produced[agentic_mask].values

    return pd.Series(result, index=df.index)


# ---------------------------------------------------------------------------
# Agentic attempt collapse
# ---------------------------------------------------------------------------

def collapse_agentic_to_attempts(df: pd.DataFrame) -> pd.DataFrame:
    """Collapse agentic generated-test rows to one row per tool attempt.

    The raw CSV has one row per *generated test* for agentic attempts.  All
    rows in the same attempt share identical attempt-level values
    (``tool_validation_outcome``, coverage/mutation deltas, etc.).  This
    function takes the first row per attempt and adds a ``generated_test_count``
    column reflecting how many tests were produced.

    Parameters
    ----------
    df:
        Only agentic rows (``lane == LANE_AGENTIC``).  Must already have
        ``rename_raw_columns`` applied (so ``changed_files_count`` etc. exist).

    Returns
    -------
    DataFrame with one row per unique attempt key.
    """
    if df.empty:
        return df.copy()

    # Derive tool_attempt_id from tool_artifact_path when the column is absent.
    if "tool_attempt_id" not in df.columns:
        if "tool_artifact_path" in df.columns:
            df = df.copy()
            df["tool_attempt_id"] = df["tool_artifact_path"].astype(str).str.strip()
        else:
            raise ValueError(
                "Agentic rows require tool_attempt_id (or tool_artifact_path as fallback)."
            )

    key_cols = [c for c in AGENTIC_ATTEMPT_KEY_FIELDS if c in df.columns]
    if "tool_attempt_id" not in key_cols:
        raise ValueError("Agentic rows require tool_attempt_id for attempt-level collapse.")
    if df["tool_attempt_id"].isna().any() or df["tool_attempt_id"].astype(str).str.strip().eq("").any():
        raise ValueError("Agentic rows require non-empty tool_attempt_id values.")

    first_rows = df.groupby(key_cols, sort=False).first().reset_index()

    # Drop any pre-existing generated_test_count so the merge doesn't produce
    # suffixed duplicates (generated_test_count_x / generated_test_count_y).
    if "generated_test_count" in first_rows.columns:
        first_rows = first_rows.drop(columns=["generated_test_count"])

    counts = (
        df.groupby(key_cols, sort=False)
        .size()
        .reset_index(name="generated_test_count")
    )

    result = first_rows.merge(counts, on=key_cols, how="left")
    return result


# ---------------------------------------------------------------------------
# Attempt-level normalization (main entry point)
# ---------------------------------------------------------------------------

def normalize_attempts(df: pd.DataFrame) -> pd.DataFrame:
    """Full normalization pipeline producing one row per attempt.

    Handles both lanes:
    - **LLM** rows are already one-row-per-attempt; ``generated_test_count``
      is set to 1.
    - **Agentic** rows are collapsed from per-generated-test to per-attempt
      via :func:`collapse_agentic_to_attempts`.

    Steps applied in order:

    1. Rename raw CSV column names (``producer_lane`` → ``lane``, etc.).
    2. Normalize lane values to canonical ``llm`` / ``agentic``.
    3. Compute ``validated_success`` and ``produced_change``.
    4. Stamp ``generated_test_count = 1`` for LLM rows.
    5. Collapse agentic rows to attempt level.
    6. Concatenate LLM + collapsed agentic.
    7. Add ``candidate_key`` and ``repository_key``.
    8. Coerce numeric and boolean columns.
    """
    if df.empty:
        return df.copy()

    out = rename_raw_columns(df)

    # 2. Normalize lane
    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    _assign_attempt_ids(out)

    # 3. Computed columns (must run before split so lane is available)
    _add_outcome_flags(out)
    out["produced_change"] = _compute_produced_change(out)
    out["impact_attribution"] = "attempt_level"

    # 4. LLM: one generated test per attempt
    llm_mask = out["lane"] == LANE_LLM
    agentic_mask = out["lane"] == LANE_AGENTIC

    if "generated_test_count" not in out.columns:
        out["generated_test_count"] = pd.NA
    out.loc[llm_mask, "generated_test_count"] = 1

    # 5 & 6. Collapse agentic, then concat
    llm_df = out[llm_mask].copy()
    agentic_df = out[agentic_mask].copy()

    if not agentic_df.empty:
        agentic_df = collapse_agentic_to_attempts(agentic_df)

    out = pd.concat([llm_df, agentic_df], ignore_index=True)

    # Collapse preserves first row values; re-apply flags to keep types stable.
    _add_outcome_flags(out)

    # 7. Keys
    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)

    # 8. Coerce types
    _coerce_numeric(out)
    _coerce_bool(out)

    # 9. Parse smell strings into structured columns
    _expand_smell_columns(out, "baseline_test_smells", "baseline_test_smell_count", "baseline_test_smell_types")
    _expand_smell_columns(out, "generated_test_smells", "generated_test_smell_count", "generated_test_smell_types")

    return out


def _coerce_numeric(df: pd.DataFrame) -> None:
    """Coerce expected numeric columns in-place (ignores missing columns)."""
    numeric_cols = [
        "coverage_before", "coverage_after", "coverage_delta",
        "mutation_score_before", "mutation_score_after", "mutation_score_delta",
        "duration_seconds", "total_tokens", "generated_test_count",
        "changed_files_count", "tool_attempt_id", "source_member_id",
        "test_files_changed", "production_files_changed", "project_files_changed",
        "deleted_files_count", "tool_post_attempt_test_run_id",
        "roslyn_diagnostics_before_raw_count",
        "roslyn_diagnostics_after_raw_count",
        "new_actionable_roslyn_diagnostics_count",
        "attempt_number", "repair_attempt_number",
        "test_mapping_count", "setup_binding_count",
        # Source method code metrics
        "source_method_baseline_coverage", "source_method_complexity",
        "source_method_mi", "source_method_cc", "source_method_coupling",
        "source_method_dit", "source_method_sloc", "source_method_eloc",
        # Baseline test code metrics
        "baseline_test_mi", "baseline_test_cc", "baseline_test_coupling",
        "baseline_test_dit", "baseline_test_sloc", "baseline_test_eloc",
        # Generated test code metrics
        "generated_test_mi", "generated_test_cc", "generated_test_coupling",
        "generated_test_dit", "generated_test_sloc", "generated_test_eloc",
    ]
    for col in numeric_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")


def _coerce_bool(df: pd.DataFrame) -> None:
    """Coerce string booleans to Python bool in-place (ignores missing columns)."""
    _true = frozenset(("1", "true", "yes"))
    _false = frozenset(("0", "false", "no"))

    def _to_bool(v: object) -> "bool | pd._libs.missing.NAType":
        s = str(v).strip().lower()
        if s in _true:
            return True
        if s in _false:
            return False
        return pd.NA

    bool_cols = [
        "generated_test_compiled", "generated_test_executed", "generated_test_passed",
        "roslyn_validation_succeeded", "roslyn_validation_skipped",
        "mutant_killed",
    ]
    for col in bool_cols:
        if col in df.columns:
            df[col] = df[col].map(_to_bool)


# ---------------------------------------------------------------------------
# Generated-test-level dataset (pre-collapse grain)
# ---------------------------------------------------------------------------

def build_generated_tests_dataset(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Return a per-generated-test dataset from the raw CSV DataFrame.

    This preserves the full row-per-test grain for both lanes:
    - LLM: each row is already one test.
    - Agentic: each row is already one test (no collapsing).

    Applies column renames, lane normalization, and key columns, but does
    **not** collapse agentic rows.
    """
    if raw_df.empty:
        return raw_df.copy()

    out = rename_raw_columns(raw_df)

    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)
    _assign_attempt_ids(out)
    _add_outcome_flags(out)
    out["impact_attribution"] = "attempt_level"
    out["produced_change"] = _compute_produced_change(out)

    _coerce_numeric(out)
    _coerce_bool(out)
    _expand_smell_columns(out, "baseline_test_smells", "baseline_test_smell_count", "baseline_test_smell_types")
    _expand_smell_columns(out, "generated_test_smells", "generated_test_smell_count", "generated_test_smell_types")

    return out


# ---------------------------------------------------------------------------
# Candidate-level aggregation
# ---------------------------------------------------------------------------

def build_candidate_summary(attempts_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse attempt rows to one row per (candidate_key, lane).

    Best attempt selection:
    1. Any attempt where ``validated_success`` is True.
    2. Otherwise the attempt with the highest ``coverage_delta``.
    3. Otherwise the first attempt.

    Adds summary columns:
    - ``attempt_count``
    - ``any_validated_success``
    - ``best_coverage_delta``
    - ``best_mutation_delta``
    - ``total_generated_tests``
    """
    if attempts_df.empty:
        return pd.DataFrame()

    group_cols = ["candidate_key", "lane"]
    if not all(c in attempts_df.columns for c in group_cols):
        return pd.DataFrame()

    records: list[dict] = []

    for (candidate_key, lane), group in attempts_df.groupby(group_cols, sort=False):
        # Best attempt selection
        successes = group[group["validated_success"] == True]  # noqa: E712
        if not successes.empty:
            positive_successes = (
                successes[successes["positive_impact"] == True]  # noqa: E712
                if "positive_impact" in successes.columns
                else pd.DataFrame()
            )
            best = positive_successes.iloc[0] if not positive_successes.empty else successes.iloc[0]
        elif "coverage_delta" in group.columns:
            best = group.loc[group["coverage_delta"].fillna(-999).idxmax()]
        else:
            best = group.iloc[0]

        row = best.to_dict()
        row["attempt_count"] = len(group)
        row["any_validated_success"] = bool(
            (group["validated_success"] == True).any()  # noqa: E712
        ) if "validated_success" in group.columns else False
        row["any_positive_impact"] = bool(
            (group["positive_impact"] == True).any()  # noqa: E712
        ) if "positive_impact" in group.columns else False
        row["validated_evidence_positive_count"] = int(
            (group["validated_evidence_positive"] == True).sum()  # noqa: E712
        ) if "validated_evidence_positive" in group.columns else 0
        row["validated_low_impact_count"] = int(
            (group["validated_low_impact"] == True).sum()  # noqa: E712
        ) if "validated_low_impact" in group.columns else 0
        row["positive_impact_count"] = int(
            (group["positive_impact"] == True).sum()  # noqa: E712
        ) if "positive_impact" in group.columns else 0
        row["best_coverage_delta"] = (
            group["coverage_delta"].max() if "coverage_delta" in group.columns else None
        )
        row["best_mutation_delta"] = (
            group["mutation_score_delta"].max()
            if "mutation_score_delta" in group.columns
            else None
        )
        row["total_generated_tests"] = (
            group["generated_test_count"].sum()
            if "generated_test_count" in group.columns
            else None
        )
        records.append(row)

    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Repository-level aggregation
# ---------------------------------------------------------------------------

def build_repository_summary(candidates_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse candidate rows to one row per (repository_key, lane) slice."""
    if candidates_df.empty:
        return pd.DataFrame()

    records: list[dict] = []

    for (repo_key, lane), group in candidates_df.groupby(
        ["repository_key", "lane"], sort=False
    ):
        row: dict = {
            "repository_key": repo_key,
            "lane": lane,
            "repo_owner": group["repo_owner"].iloc[0] if "repo_owner" in group.columns else "",
            "repo_name": group["repo_name"].iloc[0] if "repo_name" in group.columns else "",
            "commit_hash": group["commit_hash"].iloc[0] if "commit_hash" in group.columns else "",
            "candidate_count": len(group),
            "validated_success_count": int(
                (group["any_validated_success"] == True).sum()  # noqa: E712
            ) if "any_validated_success" in group.columns else 0,
            "validated_success_rate": (
                (group["any_validated_success"] == True).mean()  # noqa: E712
            ) if "any_validated_success" in group.columns else None,
            "positive_impact_count": int(
                (group["any_positive_impact"] == True).sum()  # noqa: E712
            ) if "any_positive_impact" in group.columns else 0,
            "positive_impact_rate": (
                (group["any_positive_impact"] == True).mean()  # noqa: E712
            ) if "any_positive_impact" in group.columns else None,
            "mean_coverage_delta": (
                group["best_coverage_delta"].mean()
                if "best_coverage_delta" in group.columns
                else None
            ),
            "mean_mutation_delta": (
                group["best_mutation_delta"].mean()
                if "best_mutation_delta" in group.columns
                else None
            ),
            "total_generated_tests": (
                group["total_generated_tests"].sum()
                if "total_generated_tests" in group.columns
                else None
            ),
        }
        records.append(row)

    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Paired comparison builder
# ---------------------------------------------------------------------------

def build_paired_comparison(candidates_df: pd.DataFrame) -> pd.DataFrame:
    """Build a paired table with one row per candidate, comparing LLM vs agentic.

    Winner labels (``schema.WINNER_LABELS``):
      ``llm_won``, ``agentic_won``, ``tie_success``, ``tie_failure``,
      ``llm_only``, ``agentic_only``, ``no_comparable_result``
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
            "llm_attempt_count": (
                int(llm_row["attempt_count"])
                if llm_row is not None and "attempt_count" in llm_row
                else None
            ),
            "agentic_attempt_count": (
                int(ag_row["attempt_count"])
                if ag_row is not None and "attempt_count" in ag_row
                else None
            ),
            "llm_best_coverage_delta": (
                float(llm_row["best_coverage_delta"])
                if llm_row is not None and pd.notna(llm_row.get("best_coverage_delta"))
                else None
            ),
            "agentic_best_coverage_delta": (
                float(ag_row["best_coverage_delta"])
                if ag_row is not None and pd.notna(ag_row.get("best_coverage_delta"))
                else None
            ),
            "llm_best_mutation_delta": (
                float(llm_row["best_mutation_delta"])
                if llm_row is not None and pd.notna(llm_row.get("best_mutation_delta"))
                else None
            ),
            "agentic_best_mutation_delta": (
                float(ag_row["best_mutation_delta"])
                if ag_row is not None and pd.notna(ag_row.get("best_mutation_delta"))
                else None
            ),
        }
        records.append(record)

    return pd.DataFrame(records)
