"""Normalization functions for building clean evaluation datasets.

These functions operate on DataFrames loaded from result CSVs and produce
the standardized tables that notebooks consume.

Key design constraints
----------------------
- Result schema v3 separates row grains and requires pinned target provenance.
- ``normalize_attempts`` handles the full pipeline and returns a DataFrame
  with one row per *attempt* for both lanes.
- ``build_generated_tests_dataset`` normalizes the generated-test grain without
  attributing attempt-level deltas to individual tests.
"""

from __future__ import annotations

import json

import pandas as pd

from analysis.schema import (
    AGENTIC_ATTEMPT_KEY_FIELDS,
    ASSERTION_RESULT_FIELDS,
    ASSERTION_RESULTS_SCHEMA_VERSION,
    ASSERTION_SIDECAR_REQUIRED_FIELDS,
    ASSERTION_SIDECAR_SCHEMA_VERSION,
    CANDIDATE_KEY_FIELDS,
    LANE_AGENTIC,
    LANE_LLM,
    LEGACY_RESULTS_SCHEMA_VERSION,
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


def _result_schema_version(df: pd.DataFrame) -> str:
    if "results_schema_version" not in df.columns:
        return ""
    values = df["results_schema_version"].dropna().astype(str).str.strip().unique()
    return values[0] if len(values) == 1 else ""


def _apply_assertion_result_semantics(df: pd.DataFrame) -> None:
    """Derive honest legacy missingness without fabricating schema-4 evidence."""
    schema_version = _result_schema_version(df)
    for column in ASSERTION_RESULT_FIELDS:
        if column not in df.columns:
            df[column] = pd.NA
    if schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        df["assertion_measurement_status"] = "NotMeasured"
        df["assertion_measurement_reason"] = "HistoricalNotMeasured"
    elif schema_version == ASSERTION_RESULTS_SCHEMA_VERSION:
        # Strict audits report missing schema-4 values. Normalization must not
        # invent a policy, category count, or observed zero.
        return


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


def make_repository_family_key(row: pd.Series) -> str:
    """Build the cross-revision repository identity key."""
    return str(row.get("repository_identity", "")).strip().lower()


def _validate_pinned_provenance(df: pd.DataFrame) -> None:
    required = {
        "results_schema_version", "target_id", "repository_identity",
        "requested_commit", "resolved_commit", "target_manifest_sha256",
        "target_source_sha256", "provenance_policy_version",
        "workspace_integrity_status",
    }
    missing = required - set(df.columns)
    if missing:
        raise ValueError(f"Result rows are missing pinned provenance fields: {sorted(missing)}.")
    versions = df["results_schema_version"].astype(str).str.strip()
    supported = {LEGACY_RESULTS_SCHEMA_VERSION, ASSERTION_RESULTS_SCHEMA_VERSION}
    if not versions.isin(supported).all() or versions.nunique() != 1:
        raise ValueError(
            "Normalization requires one supported results schema version (3.0 or 4.0)."
        )
    schema_version = versions.iloc[0]
    blank_fields = required - {"results_schema_version"}
    for column in sorted(blank_fields):
        blank = df[column].isna() | df[column].astype(str).str.strip().eq("")
        if blank.any():
            raise ValueError(f"Schema {schema_version} rows require non-empty {column}.")
    requested = df["requested_commit"].astype(str).str.strip().str.lower()
    resolved = df["resolved_commit"].astype(str).str.strip().str.lower()
    full_sha = requested.str.fullmatch(r"[0-9a-f]{40}") & resolved.str.fullmatch(r"[0-9a-f]{40}")
    if not full_sha.all() or not requested.eq(resolved).all():
        raise ValueError(
            f"Schema {schema_version} rows require equal full requested and resolved commits."
        )
    if "commit_hash" in df.columns:
        alias = df["commit_hash"].astype(str).str.strip().str.lower()
        if not alias.eq(resolved).all():
            raise ValueError(
                f"commit_hash must equal resolved_commit in schema {schema_version}."
            )


def _assign_attempt_ids(df: pd.DataFrame) -> None:
    """Validate canonical attempt IDs emitted by current result schemas."""
    if "attempt_id" not in df.columns:
        raise ValueError("Current result rows require attempt_id.")
    missing = df["attempt_id"].isna() | df["attempt_id"].astype(str).str.strip().eq("")
    if missing.any():
        raise ValueError("Current result rows require non-empty attempt_id values.")
    df["attempt_id"] = df["attempt_id"].astype(str).str.strip()

    lane = df.get("lane", pd.Series("", index=df.index, dtype=str))
    agentic_mask = lane == LANE_AGENTIC
    if agentic_mask.any():
        if "tool_attempt_id" not in df.columns:
            raise ValueError("Agentic rows require tool_attempt_id.")
        missing_tool_id = (
            df.loc[agentic_mask, "tool_attempt_id"].isna()
            | df.loc[agentic_mask, "tool_attempt_id"].astype(str).str.strip().eq("")
        )
        if missing_tool_id.any():
            raise ValueError("Agentic rows require non-empty tool_attempt_id values.")


# ---------------------------------------------------------------------------
# Computed columns
# ---------------------------------------------------------------------------

OUTCOME_VALIDATED_EVIDENCE_POSITIVE = "ValidatedEvidencePositive"
OUTCOME_VALIDATED_LOW_IMPACT = "ValidatedLowImpact"
OUTCOME_VALIDATED_IMPACT_UNKNOWN = "ValidatedImpactUnknown"
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
    OUTCOME_VALIDATED_IMPACT_UNKNOWN,
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
    exported = _string_or_empty(row.get("outcome_classification"))
    if exported:
        return exported
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
    result = result.mask(oc.eq(OUTCOME_VALIDATED_IMPACT_UNKNOWN), pd.NA)
    return result.where(oc.notna(), other=pd.NA).astype("boolean")


def _add_outcome_flags(df: pd.DataFrame) -> None:
    df["metric_improved"] = _compute_metric_improved(df)
    df["outcome_classification"] = _compute_outcome_classification(df)
    oc = df["outcome_classification"]
    df["validated_evidence_positive"] = oc.eq(OUTCOME_VALIDATED_EVIDENCE_POSITIVE).astype("boolean")
    df["validated_low_impact"] = oc.eq(OUTCOME_VALIDATED_LOW_IMPACT).astype("boolean")
    df["validated_success"] = _compute_validated_success(df)
    df["positive_impact"] = _compute_positive_impact(df)


def _is_true(series: pd.Series) -> pd.Series:
    """Boolean mask that treats missing values as False."""
    return series.eq(True).fillna(False).astype(bool)


# ---------------------------------------------------------------------------
# Producer and infrastructure failures
# ---------------------------------------------------------------------------

# A failure caused by the environment (provider credentials, provider HTTP errors,
# container start-up, workspace locks) says nothing about the model or tool. Such
# attempts are missing data, not failures. The first matching pattern names the reason.
INFRASTRUCTURE_FAILURE_PATTERNS: list[tuple[str, str]] = [
    ("provider_auth", r"security token|invalid api key|unauthori[sz]ed|\b401\b|\b403\b"),
    ("provider_http_error", r"service request failed|status:\s*[45]\d\d\b|rate limit|\b429\b"),
    ("provider_timeout", r"retry failed after|exceeded the configured timeout"),
    ("repair_input_missing", r"no previous attempt available for repair"),
    ("container_start_failed", r"exited with code 125\b"),
    ("workspace_file_lock", r"process cannot access the file"),
]


def _compute_infrastructure_failure_reason(df: pd.DataFrame) -> pd.Series:
    """Return the infrastructure failure reason per row, or NA for other rows.

    Only attempts that did not validate can be infrastructure failures. The reason is
    read from ``failure_summary``.
    """
    reason = pd.Series(pd.NA, index=df.index, dtype="object")
    if "failure_summary" not in df.columns:
        return reason
    summary = df["failure_summary"].fillna("").astype(str)
    failed = (
        ~_is_true(df["validated_success"])
        if "validated_success" in df.columns
        else pd.Series(True, index=df.index)
    )
    for name, pattern in INFRASTRUCTURE_FAILURE_PATTERNS:
        match = failed & reason.isna() & summary.str.contains(pattern, case=False, regex=True)
        reason[match] = name
    return reason


def _compute_producer(df: pd.DataFrame) -> pd.Series:
    """The model (LLM lane) or tool (agentic lane) that produced each attempt."""
    def _column(name: str) -> pd.Series:
        if name not in df.columns:
            return pd.Series(pd.NA, index=df.index, dtype="object")
        values = df[name].astype("object")
        return values.where(values.notna() & values.astype(str).str.strip().ne(""), pd.NA)

    model = _column("model")
    tool = _column("tool_id")
    lane = df.get("lane", pd.Series("", index=df.index))
    return tool.where(lane.eq(LANE_AGENTIC) & tool.notna(), model)


def _add_producer_and_infrastructure(df: pd.DataFrame) -> None:
    df["producer"] = _compute_producer(df)
    df["infrastructure_failure_reason"] = _compute_infrastructure_failure_reason(df)
    df["infrastructure_failure"] = df["infrastructure_failure_reason"].notna()


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

    key_cols = [c for c in AGENTIC_ATTEMPT_KEY_FIELDS if c in df.columns]
    if "attempt_id" not in key_cols:
        raise ValueError("Agentic rows require attempt_id for attempt-level collapse.")

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

    Input must contain current-format ``row_kind='attempt'`` rows only.

    Steps applied in order:

    1. Rename raw CSV column names (``producer_lane`` → ``lane``, etc.).
    2. Normalize lane values to canonical ``llm`` / ``agentic``.
    3. Compute ``validated_success`` and ``produced_change``.
    4. Validate one globally unique row per ``attempt_id``.
    5. Add ``candidate_key`` and ``repository_key``.
    6. Coerce numeric and boolean columns.
    """
    if df.empty:
        return df.copy()

    if "row_kind" not in df.columns or not df["row_kind"].eq("attempt").all():
        raise ValueError("normalize_attempts requires only row_kind='attempt' rows.")

    out = rename_raw_columns(df)
    _apply_assertion_result_semantics(out)
    _validate_pinned_provenance(out)

    # 2. Normalize lane
    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    _assign_attempt_ids(out)

    # 3. Computed columns (must run before split so lane is available)
    _add_outcome_flags(out)
    _add_producer_and_infrastructure(out)
    out["produced_change"] = _compute_produced_change(out)
    out["impact_attribution"] = "attempt_level"

    if "generated_test_count" not in out.columns:
        out["generated_test_count"] = 0
    if out["attempt_id"].duplicated().any():
        duplicates = sorted(out.loc[out["attempt_id"].duplicated(False), "attempt_id"].unique())
        raise ValueError(f"Attempt CSV contains duplicate attempt_id values: {duplicates[:5]}.")

    # 7. Keys
    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)
    out["repository_revision_key"] = out["repository_key"]
    out["repository_family_key"] = out.apply(make_repository_family_key, axis=1)

    # 8. Coerce types
    _coerce_numeric(out)
    _invalidate_inconsistent_measurements(out)
    _coerce_bool(out)

    # 9. Parse smell strings into structured columns
    _expand_smell_columns(out, "baseline_test_smells", "baseline_test_smell_count", "baseline_test_smell_types")
    _expand_smell_columns(out, "generated_test_smells", "generated_test_smell_count", "generated_test_smell_types")

    # 10. Lane-fair token cost
    out["token_usage_valid"] = _validate_token_rows(out)
    out["effective_input_tokens"] = _compute_effective_component(out, "input_tokens")
    out["effective_output_tokens"] = _compute_effective_component(out, "output_tokens")
    out["effective_tokens"] = _compute_effective_tokens(out)

    return out


def _compute_effective_tokens(df: pd.DataFrame) -> pd.Series:
    """Tokens attributable to producing each attempt's result, made lane-comparable.

    LLM attempts are repair *steps* in a chain, so ``total_tokens`` is per-step;
    ``cumulative_tokens`` carries the running chain total and is the cost that is
    comparable to an agentic run. Agentic attempts are self-contained runs whose
    cost is ``total_tokens`` (their ``cumulative_tokens`` is not populated).

    Rule: agentic -> ``total_tokens``; LLM -> ``cumulative_tokens`` (fallback to
    ``total_tokens`` when cumulative is missing/zero).

    WARNING: do not ``sum`` attempt-level ``effective_tokens`` across an LLM chain
    -- cumulative double-counts. Use the candidate-level ``effective_tokens`` from
    :func:`build_candidate_summary` for per-lane / per-model totals and rates.
    """
    index = df.index
    total = (
        pd.to_numeric(df["total_tokens"], errors="coerce")
        if "total_tokens" in df.columns
        else pd.Series(pd.NA, index=index, dtype="Float64")
    )
    eff = total.astype("Float64").copy()
    if "cumulative_tokens" in df.columns and "lane" in df.columns:
        cum = pd.to_numeric(df["cumulative_tokens"], errors="coerce")
        use_cum = df["lane"].eq(LANE_LLM) & cum.notna() & cum.gt(0)
        eff[use_cum] = cum[use_cum].astype("Float64")
    return eff


def _compute_effective_component(df: pd.DataFrame, component: str) -> pd.Series:
    """Return the lane-fair per-attempt value for one input/output component."""
    index = df.index
    value = (
        pd.to_numeric(df[component], errors="coerce")
        if component in df.columns
        else pd.Series(pd.NA, index=index, dtype="Float64")
    )
    effective = value.astype("Float64").copy()
    cumulative = f"cumulative_{component}"
    if cumulative in df.columns and "lane" in df.columns:
        running = pd.to_numeric(df[cumulative], errors="coerce")
        use_running = df["lane"].eq(LANE_LLM) & running.notna()
        effective[use_running] = running[use_running].astype("Float64")
    return effective


def _validate_token_rows(df: pd.DataFrame) -> pd.Series:
    """Flag rows whose status/components/derived total follow the token contract."""
    def _numbers(column: str) -> pd.Series:
        return (
            pd.to_numeric(df[column], errors="coerce")
            if column in df.columns
            else pd.Series(pd.NA, index=df.index, dtype="Float64")
        )

    status = df.get("usage_status", pd.Series("", index=df.index)).fillna("")
    input_tokens = _numbers("input_tokens")
    output_tokens = _numbers("output_tokens")
    total_tokens = _numbers("total_tokens")
    complete = status.isin({"complete-reported", "complete-estimated"})
    partial = status.eq("partial")
    empty = status.isin({"missing", "not-applicable"})
    valid = pd.Series(False, index=df.index)
    valid.loc[complete] = (
        input_tokens.loc[complete].notna()
        & output_tokens.loc[complete].notna()
        & total_tokens.loc[complete].eq(
            input_tokens.loc[complete] + output_tokens.loc[complete]
        )
    )
    valid.loc[partial] = (
        input_tokens.loc[partial].notna() | output_tokens.loc[partial].notna()
    ) & total_tokens.loc[partial].isna()
    valid.loc[empty] = (
        input_tokens.loc[empty].isna()
        & output_tokens.loc[empty].isna()
        & total_tokens.loc[empty].isna()
    )
    return valid


def _candidate_effective_tokens(group: pd.DataFrame) -> float:
    """Total tokens to produce a candidate's result for one lane (chain-aware).

    LLM -> terminal non-missing ``cumulative_tokens`` over the ordered repair chain;
    agentic -> sum of ``total_tokens`` across the candidate's attempts.
    """
    lane = str(group["lane"].iloc[0]) if "lane" in group.columns and len(group) else ""
    total = (
        pd.to_numeric(group["total_tokens"], errors="coerce")
        if "total_tokens" in group.columns
        else pd.Series(dtype="float64")
    )
    if lane == LANE_LLM and "cumulative_tokens" in group.columns:
        cum = pd.to_numeric(group["cumulative_tokens"], errors="coerce")
        if cum.notna().any():
            return float(cum.dropna().iloc[-1])
    return float(total.sum()) if total.notna().any() else float("nan")


def _candidate_effective_component(group: pd.DataFrame, component: str) -> float:
    lane = str(group["lane"].iloc[0]) if "lane" in group.columns and len(group) else ""
    values = (
        pd.to_numeric(group[component], errors="coerce")
        if component in group.columns
        else pd.Series(pd.NA, index=group.index, dtype="Float64")
    )
    cumulative = f"cumulative_{component}"
    if lane == LANE_LLM and cumulative in group.columns:
        running = pd.to_numeric(group[cumulative], errors="coerce")
        if running.notna().any():
            return float(running.dropna().iloc[-1])
    return float(values.sum()) if values.notna().any() else float("nan")


def _invalidate_inconsistent_measurements(df: pd.DataFrame) -> None:
    """Null out post-attempt metrics that were never validly measured.

    When the generated test breaks the build (or otherwise fails to run), the
    project becomes uncompilable and coverage/mutation cannot be measured. The
    pipeline records a sentinel ``coverage_after = 0`` for these rows even though
    ``coverage_before > 0`` — and stores ``coverage_delta = 0``. Both are
    defaults, not measurements: the honest value is **unknown (NaN)**, not 0
    (a false "coverage collapsed to zero") and not ``before`` (a false "measured,
    unchanged").

    Detection is by internal consistency. A genuine post-measurement satisfies
    ``after == before + delta`` (e.g. an attempt that improved coverage, even one
    that later failed validation for an unrelated reason, stays consistent and is
    kept). Where ``after`` *contradicts* ``before + delta`` beyond rounding, the
    triple is a failed-measurement sentinel, so both ``after`` and ``delta`` are
    set to NaN and the row drops out of before/after slope charts and metric-
    movement distributions instead of corrupting them.
    """
    pairs = [
        ("coverage_before", "coverage_delta", "coverage_after"),
        ("mutation_score_before", "mutation_score_delta", "mutation_score_after"),
    ]
    for before_col, delta_col, after_col in pairs:
        if not all(c in df.columns for c in (before_col, delta_col, after_col)):
            continue
        before = pd.to_numeric(df[before_col], errors="coerce")
        delta = pd.to_numeric(df[delta_col], errors="coerce")
        after = pd.to_numeric(df[after_col], errors="coerce")
        contradicts = before.notna() & delta.notna() & (after.sub(before + delta).abs() > 1e-6)
        if contradicts.any():
            df.loc[contradicts, after_col] = pd.NA
            df.loc[contradicts, delta_col] = pd.NA


def _coerce_numeric(df: pd.DataFrame) -> None:
    """Coerce expected numeric columns in-place (ignores missing columns)."""
    numeric_cols = [
        "coverage_before", "coverage_after", "coverage_delta",
        "mutation_score_before", "mutation_score_after", "mutation_score_delta",
        "duration_seconds", "input_tokens", "output_tokens", "estimated_prompt_tokens",
        "total_tokens", "cumulative_input_tokens", "cumulative_output_tokens",
        "cumulative_tokens", "generated_test_count",
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
        # Assertion-lineage schema 4.0 / sidecar 1.0
        "assertion_max_depth", "recognized_assertion_count",
        "unrecognized_assertion_count", "traced_assertion_count",
        "trivial_assertion_count", "unresolved_assertion_count",
        "assertion_analysis_duration_ms", "assertion_ordinal",
        "start_line", "start_column", "end_line", "end_column",
        "depth_reached", "generated_test_assertion_summary_id",
        "observation_id", "experiment_run_id", "candidate_method_id",
        "intended_source_member_id", "generated_test_execution_id",
        "tool_attempt_generated_test_id", "test_member_id",
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
        "no_recognized_assertions",
    ]
    for col in bool_cols:
        if col in df.columns:
            df[col] = df[col].map(_to_bool)


# ---------------------------------------------------------------------------
# Generated-test-level dataset
# ---------------------------------------------------------------------------

def build_generated_tests_dataset(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Return a per-generated-test dataset from the raw CSV DataFrame.

    This preserves the canonical row-per-generated-test grain for both lanes.

    Applies column renames, lane normalization, and key columns, but does
    **not** collapse agentic rows.
    """
    if raw_df.empty:
        return raw_df.copy()

    if "row_kind" not in raw_df.columns or not raw_df["row_kind"].eq("generated_test").all():
        raise ValueError("Generated-test normalization requires row_kind='generated_test' rows.")

    out = rename_raw_columns(raw_df)
    _apply_assertion_result_semantics(out)
    _validate_pinned_provenance(out)

    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)
    out["repository_revision_key"] = out["repository_key"]
    out["repository_family_key"] = out.apply(make_repository_family_key, axis=1)
    _assign_attempt_ids(out)
    _add_outcome_flags(out)
    out["producer"] = _compute_producer(out)
    out["impact_attribution"] = "attempt_level"
    out["produced_change"] = _compute_produced_change(out)

    _coerce_numeric(out)
    _invalidate_inconsistent_measurements(out)
    _coerce_bool(out)
    _expand_smell_columns(out, "baseline_test_smells", "baseline_test_smell_count", "baseline_test_smell_types")
    _expand_smell_columns(out, "generated_test_smells", "generated_test_smell_count", "generated_test_smell_types")

    return out


def normalize_assertion_observations(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Normalize schema-1 assertion rows while preserving one row per occurrence."""
    if raw_df.empty:
        return raw_df.copy()

    missing = ASSERTION_SIDECAR_REQUIRED_FIELDS - set(raw_df.columns)
    if missing:
        raise ValueError(
            f"Assertion observation rows are missing required fields: {sorted(missing)}."
        )
    versions = raw_df["assertion_schema_version"].dropna().astype(str).str.strip()
    if versions.empty or not versions.eq(ASSERTION_SIDECAR_SCHEMA_VERSION).all():
        raise ValueError("Assertion observations require assertion_schema_version='1.0'.")

    out = rename_raw_columns(raw_df).rename(columns={
        "policy_version": "assertion_policy_version",
        "max_depth": "assertion_max_depth",
        "ordered_lineage_paths_json": "lineage_paths_json",
    })
    if "lane" in out.columns:
        out["lane"] = out["lane"].fillna("").apply(normalize_lane)

    if "source_member_id" not in out.columns:
        out["source_member_id"] = out["intended_source_member_id"]
    out["candidate_key"] = out.apply(make_candidate_key, axis=1)
    out["repository_key"] = out.apply(make_repository_key, axis=1)
    out["repository_revision_key"] = out["repository_key"]
    out["repository_family_key"] = out.apply(make_repository_family_key, axis=1)

    _coerce_numeric(out)
    out["is_traced"] = out["category"].eq("Traced")
    return out


def build_traced_assertions_dataset(assertions_df: pd.DataFrame) -> pd.DataFrame:
    """Derive the traced-only view while leaving the canonical raw rows intact."""
    if assertions_df.empty:
        return assertions_df.copy()
    if "category" not in assertions_df.columns:
        raise ValueError("Assertion observations require a category column.")
    return assertions_df.loc[
        assertions_df["category"].eq("Traced")
    ].copy().reset_index(drop=True)


def normalize_assertion_measurements(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Normalize additive SQLite measurement rows for downstream joins."""
    if raw_df.empty:
        return raw_df.copy()
    rename = {
        "status": "assertion_measurement_status",
        "failure_code": "assertion_measurement_reason",
        "policy_version": "assertion_policy_version",
        "max_depth": "assertion_max_depth",
        "analysis_duration_ms": "assertion_analysis_duration_ms",
    }
    out = raw_df.rename(
        columns={key: value for key, value in rename.items() if key in raw_df.columns}
    ).copy()
    _coerce_numeric(out)
    return out


# ---------------------------------------------------------------------------
# Candidate-level aggregation
# ---------------------------------------------------------------------------

def _first_attempt(group: pd.DataFrame) -> pd.Series:
    """The attempt with the lowest ``attempt_number`` (the first row when absent)."""
    if "attempt_number" not in group.columns:
        return group.iloc[0]
    order = pd.to_numeric(group["attempt_number"], errors="coerce").fillna(float("inf"))
    return group.loc[order.sort_values(kind="stable").index[0]]


CHAIN_KEY_FIELDS = ["candidate_key", "lane", "producer", "budget_mode"]


def chain_key_columns(df: pd.DataFrame) -> list[str]:
    """The chain identity columns present in *df*: candidate, lane, producer, budget arm."""
    return [c for c in CHAIN_KEY_FIELDS if c in df.columns]


def build_candidate_summary(attempts_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse attempt rows to one row per chain: (candidate_key, lane, producer, budget_mode).

    A producer is one LLM model or one agentic tool, and ``budget_mode`` is the
    experiment arm (for example ``PassAt1`` versus ``PassAt1RepairAt5``, which run
    as separate chains for the same model). Both are part of the grain: pooling a
    lane's producers would credit the lane with a success whenever any one of its
    models or tools succeeded, and pooling arms would merge two first attempts.

    Infrastructure failures (``infrastructure_failure``) are missing data. Outcomes are
    computed over the remaining, evaluable attempts; a producer with none has NA
    outcomes.

    Best attempt selection, among evaluable attempts:
    1. A validated success with positive impact, else any validated success.
    2. Otherwise the attempt with the highest ``coverage_delta``.
    3. Otherwise the first attempt.

    Adds summary columns:
    - ``attempt_count``, ``evaluable_attempt_count``, ``infrastructure_failure_count``
    - ``first_attempt_success``, ``first_attempt_positive_impact``: pass@1, the outcome
      of the lowest ``attempt_number``; NA when that attempt failed on infrastructure
    - ``any_validated_success``, ``any_positive_impact`` and the outcome counts
    - ``best_coverage_delta``, ``best_mutation_delta``
    - ``total_generated_tests``
    - ``effective_tokens`` (LLM terminal cumulative chain; agentic run total)
    """
    if attempts_df.empty:
        return pd.DataFrame()

    if not all(c in attempts_df.columns for c in ("candidate_key", "lane")):
        return pd.DataFrame()

    df = (attempts_df if "producer" in attempts_df.columns
          else attempts_df.assign(producer=_compute_producer(attempts_df)))
    infra_all = (
        _is_true(df["infrastructure_failure"])
        if "infrastructure_failure" in df.columns
        else pd.Series(False, index=df.index)
    )

    records: list[dict] = []

    for _, group in df.groupby(chain_key_columns(df), sort=False, dropna=False):
        infra = infra_all.loc[group.index]
        evaluable = group[~infra]
        pool = evaluable if not evaluable.empty else group

        # Best attempt selection
        succeeded = (_is_true(pool["validated_success"]) if "validated_success" in pool.columns
                     else pd.Series(False, index=pool.index))
        successes = pool[succeeded]
        if not successes.empty:
            positive_successes = (
                successes[_is_true(successes["positive_impact"])]
                if "positive_impact" in successes.columns
                else successes.iloc[0:0]
            )
            best = positive_successes.iloc[0] if not positive_successes.empty else successes.iloc[0]
        elif "coverage_delta" in pool.columns:
            best = pool.loc[pd.to_numeric(pool["coverage_delta"], errors="coerce").fillna(-999).idxmax()]
        else:
            best = pool.iloc[0]

        def _any(column: str) -> object:
            if evaluable.empty:
                return pd.NA
            return bool(_is_true(evaluable[column]).any()) if column in group.columns else False

        def _count(column: str) -> int:
            return int(_is_true(evaluable[column]).sum()) if column in group.columns else 0

        row = best.to_dict()
        row["attempt_count"] = len(group)
        row["evaluable_attempt_count"] = len(evaluable)
        row["infrastructure_failure_count"] = int(infra.sum())
        row["any_validated_success"] = _any("validated_success")
        row["any_positive_impact"] = _any("positive_impact")
        row["validated_evidence_positive_count"] = _count("validated_evidence_positive")
        row["validated_low_impact_count"] = _count("validated_low_impact")
        row["positive_impact_count"] = _count("positive_impact")

        first = _first_attempt(group)
        first_is_infra = bool(infra.loc[first.name])
        for out_col, source_col in (("first_attempt_success", "validated_success"),
                                    ("first_attempt_positive_impact", "positive_impact")):
            value = first.get(source_col, pd.NA)
            row[out_col] = pd.NA if first_is_infra or pd.isna(value) else bool(value)

        row["best_coverage_delta"] = (
            evaluable["coverage_delta"].max() if "coverage_delta" in group.columns else None
        )
        row["best_mutation_delta"] = (
            evaluable["mutation_score_delta"].max()
            if "mutation_score_delta" in group.columns
            else None
        )
        row["total_generated_tests"] = (
            group["generated_test_count"].sum()
            if "generated_test_count" in group.columns
            else None
        )
        row["effective_tokens"] = _candidate_effective_tokens(group)
        row["effective_input_tokens"] = _candidate_effective_component(group, "input_tokens")
        row["effective_output_tokens"] = _candidate_effective_component(group, "output_tokens")
        records.append(row)

    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Repository-level aggregation
# ---------------------------------------------------------------------------

def _known_rate(series: pd.Series) -> float | None:
    """Share of True among non-missing values; None when every value is missing."""
    known = series.dropna()
    return float(_is_true(known).mean()) if len(known) else None


def build_repository_summary(candidates_df: pd.DataFrame) -> pd.DataFrame:
    """Collapse candidate rows to one row per (repository_key, lane) slice.

    Candidate rows are per producer, so ``candidate_count`` counts distinct
    candidates and ``producer_result_count`` counts the (candidate, producer) rows.
    Rates are over producer rows with an evaluable outcome.
    """
    if candidates_df.empty:
        return pd.DataFrame()

    records: list[dict] = []

    for (repo_key, lane), group in candidates_df.groupby(
        ["repository_key", "lane"], sort=False
    ):
        row: dict = {
            "repository_key": repo_key,
            "repository_revision_key": group["repository_revision_key"].iloc[0] if "repository_revision_key" in group.columns else repo_key,
            "repository_family_key": group["repository_family_key"].iloc[0] if "repository_family_key" in group.columns else "",
            "repository_identity": group["repository_identity"].iloc[0] if "repository_identity" in group.columns else "",
            "resolved_commit": group["resolved_commit"].iloc[0] if "resolved_commit" in group.columns else "",
            "target_id": group["target_id"].iloc[0] if "target_id" in group.columns else "",
            "lane": lane,
            "repo_owner": group["repo_owner"].iloc[0] if "repo_owner" in group.columns else "",
            "repo_name": group["repo_name"].iloc[0] if "repo_name" in group.columns else "",
            "commit_hash": group["commit_hash"].iloc[0] if "commit_hash" in group.columns else "",
            "candidate_count": (
                int(group["candidate_key"].nunique()) if "candidate_key" in group.columns
                else len(group)
            ),
            "producer_result_count": len(group),
            "validated_success_count": int(
                _is_true(group["any_validated_success"]).sum()
            ) if "any_validated_success" in group.columns else 0,
            "validated_success_rate": (
                _known_rate(group["any_validated_success"])
            ) if "any_validated_success" in group.columns else None,
            "positive_impact_count": int(
                _is_true(group["any_positive_impact"]).sum()
            ) if "any_positive_impact" in group.columns else 0,
            "positive_impact_rate": (
                _known_rate(group["any_positive_impact"])
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


def build_repository_family_summary(repositories_df: pd.DataFrame) -> pd.DataFrame:
    """Aggregate revision-level rows without conflating distinct commits."""
    if repositories_df.empty or "repository_family_key" not in repositories_df.columns:
        return pd.DataFrame()
    records: list[dict] = []
    for (family, lane), group in repositories_df.groupby(["repository_family_key", "lane"], sort=False):
        records.append({
            "repository_family_key": family,
            "repository_identity": group["repository_identity"].iloc[0] if "repository_identity" in group.columns else family,
            "lane": lane,
            "revision_count": int(group["repository_revision_key"].nunique()),
            "candidate_count": int(group["candidate_count"].sum()),
            "validated_success_count": int(group["validated_success_count"].sum()),
            "positive_impact_count": int(group["positive_impact_count"].sum()),
            "total_generated_tests": int(group["total_generated_tests"].fillna(0).sum()),
        })
    return pd.DataFrame(records)


# ---------------------------------------------------------------------------
# Paired comparison builder
# ---------------------------------------------------------------------------

def build_paired_comparison(
    candidates_df: pd.DataFrame,
    llm_producer: str | None = None,
    agentic_producer: str | None = None,
    outcome_col: str = "any_validated_success",
    llm_budget_mode: str | None = None,
) -> pd.DataFrame:
    """Build a paired table with one row per candidate, comparing LLM vs agentic.

    Candidate rows are per chain, so each lane holds one row per model or tool and
    budget arm. Name ``llm_producer`` and ``agentic_producer`` to pair one model with
    one tool, and ``llm_budget_mode`` when the model ran several arms; otherwise
    each lane must already hold at most one row per candidate.

    *outcome_col* is the binary outcome compared, for example
    ``first_attempt_positive_impact`` for pass@1 VEP. A missing outcome (an
    infrastructure failure) counts as that side being absent.

    Winner labels (``schema.WINNER_LABELS``):
      ``llm_won``, ``agentic_won``, ``tie_success``, ``tie_failure``,
      ``llm_only``, ``agentic_only``, ``no_comparable_result``
    """
    if candidates_df.empty:
        return pd.DataFrame()

    llm = candidates_df[candidates_df["lane"] == LANE_LLM].copy()
    agentic = candidates_df[candidates_df["lane"] == LANE_AGENTIC].copy()
    if llm_producer is not None:
        llm = llm[llm["producer"] == llm_producer]
    if llm_budget_mode is not None:
        llm = llm[llm["budget_mode"] == llm_budget_mode]
    if agentic_producer is not None:
        agentic = agentic[agentic["producer"] == agentic_producer]
    for label, side in (("LLM", llm), ("agentic", agentic)):
        if side["candidate_key"].duplicated().any():
            raise ValueError(
                f"The {label} lane has several rows per candidate; "
                f"name the {label} producer (and budget mode) to pair on."
            )

    llm = llm.set_index("candidate_key")
    agentic = agentic.set_index("candidate_key")

    all_keys = llm.index.union(agentic.index)
    records: list[dict] = []

    def _outcome(r: pd.Series | None) -> bool | None:
        if r is None:
            return None
        value = r.get(outcome_col, pd.NA)
        return None if pd.isna(value) else bool(value)

    for key in all_keys:
        llm_row = llm.loc[key] if key in llm.index else None
        ag_row = agentic.loc[key] if key in agentic.index else None

        llm_outcome = _outcome(llm_row)
        ag_outcome = _outcome(ag_row)
        has_llm = llm_outcome is not None
        has_agentic = ag_outcome is not None
        llm_success = bool(llm_outcome)
        ag_success = bool(ag_outcome)

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
            "outcome": outcome_col,
            "llm_producer": llm_row.get("producer") if llm_row is not None else None,
            "agentic_producer": ag_row.get("producer") if ag_row is not None else None,
            "llm_success": llm_outcome,
            "agentic_success": ag_outcome,
            "llm_attempt_count": (
                int(llm_row["attempt_count"])
                if llm_row is not None and pd.notna(llm_row.get("attempt_count"))
                else None
            ),
            "agentic_attempt_count": (
                int(ag_row["attempt_count"])
                if ag_row is not None and pd.notna(ag_row.get("attempt_count"))
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
