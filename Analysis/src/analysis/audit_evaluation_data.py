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

from analysis.files import (
    ensure_output_dir,
    find_databases,
    read_result_grains,
    read_schema4_result_bundle,
)
from analysis.build_evaluation_dataset import (
    build_attempts_dataset,
    build_generated_tests_dataset_from_raw,
)
from analysis.normalize import normalize_assertion_observations
from analysis.schema import (
    ASSERTION_CATALOG_VERSION,
    ASSERTION_CATEGORIES,
    ASSERTION_MEASUREMENT_STATUSES,
    ASSERTION_POLICY_VERSION,
    ASSERTION_RESULTS_SCHEMA_VERSION,
    ASSERTION_SIDECAR_SCHEMA_VERSION,
    ASSERTION_SUMMARY_STATUSES,
    ASSERTION_UNAVAILABLE_REASONS,
    ASSERTION_UNRESOLVED_REASONS,
    LEGACY_RESULTS_SCHEMA_VERSION,
)

# Generated-test rows in the results CSV carry the measurement vocabulary: the
# producer maps Classified/NoRecognizedAssertions to Complete, because only
# measurement statuses are valid in the results schema. Earlier schema-4 exports
# wrote the per-test vocabulary, so both are accepted on CSV child rows.
CHILD_ROW_STATUSES = (
    (ASSERTION_MEASUREMENT_STATUSES - {"NotMeasured"}) | ASSERTION_SUMMARY_STATUSES
)
CHILD_ROW_CLASSIFIED = frozenset({"Complete", "Classified", "NoRecognizedAssertions"})


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


def audit_token_usage(df: pd.DataFrame) -> list[dict]:
    """Audit split components, classification metadata, and derived totals."""
    findings: list[dict] = []
    required = {"usage_status", "input_tokens", "output_tokens", "total_tokens"}
    if not required <= set(df.columns):
        return findings

    status = df["usage_status"].fillna("").astype(str)
    input_tokens = pd.to_numeric(df["input_tokens"], errors="coerce")
    output_tokens = pd.to_numeric(df["output_tokens"], errors="coerce")
    total_tokens = pd.to_numeric(df["total_tokens"], errors="coerce")
    complete = status.isin({"complete-reported", "complete-estimated"})
    valid_statuses = {
        "complete-reported", "complete-estimated", "partial", "missing", "not-applicable"
    }
    invalid_status = ~status.isin(valid_statuses)
    if invalid_status.any():
        findings.append({"check": "invalid_token_usage_status", "severity": "error",
                         "count": int(invalid_status.sum()),
                         "detail": "Token usage status is outside the accounting vocabulary."})
    contradictory = complete & total_tokens.ne(input_tokens + output_tokens)
    if contradictory.any():
        findings.append({"check": "contradictory_token_total", "severity": "error",
                         "count": int(contradictory.sum()),
                         "detail": "Complete token totals must equal input plus output."})
    incomplete_complete = complete & (input_tokens.isna() | output_tokens.isna())
    if incomplete_complete.any():
        findings.append({"check": "complete_usage_missing_component", "severity": "error",
                         "count": int(incomplete_complete.sum()),
                         "detail": "Complete usage rows require both token components."})
    partial = status.eq("partial")
    invalid_partial = partial & (
        (input_tokens.isna() & output_tokens.isna()) | total_tokens.notna()
    )
    if invalid_partial.any():
        findings.append({"check": "invalid_partial_token_usage", "severity": "error",
                         "count": int(invalid_partial.sum()),
                         "detail": "Partial usage requires a known component and a null total."})
    empty_status = status.isin({"missing", "not-applicable"})
    invalid_empty = empty_status & (
        input_tokens.notna() | output_tokens.notna() | total_tokens.notna()
    )
    if invalid_empty.any():
        findings.append({"check": "invalid_empty_token_usage", "severity": "error",
                         "count": int(invalid_empty.sum()),
                         "detail": "Missing/not-applicable usage cannot contain canonical tokens."})
    source = df.get("usage_source", pd.Series("", index=df.index)).fillna("").astype(str).str.strip()
    policy = df.get("usage_policy_version", pd.Series("", index=df.index)).fillna("").astype(str).str.strip()
    observed = status.isin({"complete-reported", "complete-estimated", "partial"})
    if (observed & source.eq("")).any():
        mask = observed & source.eq("")
        findings.append({"check": "missing_token_source", "severity": "error",
                         "count": int(mask.sum()), "detail": "Observed usage requires a source."})
    if (observed & policy.eq("")).any():
        mask = observed & policy.eq("")
        findings.append({"check": "missing_token_policy", "severity": "error",
                         "count": int(mask.sum()), "detail": "Observed usage requires a policy version."})
    negative = (input_tokens < 0) | (output_tokens < 0) | (total_tokens < 0)
    if negative.any():
        findings.append({"check": "negative_token_count", "severity": "error",
                         "count": int(negative.sum()), "detail": "Token counts cannot be negative."})
    saturated = (input_tokens > 2_147_483_647) | (output_tokens > 2_147_483_647) | (
        total_tokens > 2_147_483_647
    )
    if saturated.any():
        findings.append({"check": "token_count_out_of_range", "severity": "error",
                         "count": int(saturated.sum()),
                         "detail": "Token counts exceed the persisted integer range."})
    cumulative_fields = {"cumulative_input_tokens", "cumulative_output_tokens", "cumulative_tokens"}
    if cumulative_fields <= set(df.columns):
        cumulative_input = pd.to_numeric(df["cumulative_input_tokens"], errors="coerce")
        cumulative_output = pd.to_numeric(df["cumulative_output_tokens"], errors="coerce")
        cumulative_total = pd.to_numeric(df["cumulative_tokens"], errors="coerce")
        bad_cumulative = cumulative_total.notna() & cumulative_total.ne(cumulative_input + cumulative_output)
        if bad_cumulative.any():
            findings.append({"check": "contradictory_cumulative_token_total", "severity": "error",
                             "count": int(bad_cumulative.sum()),
                             "detail": "Cumulative total must equal cumulative input plus output."})
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


def audit_pinned_provenance(df: pd.DataFrame) -> list[dict]:
    """Return blocking findings for the schema 3.0 reproducibility contract."""
    findings: list[dict] = []
    required = (
        "target_id", "repository_identity", "requested_commit", "resolved_commit",
        "target_manifest_sha256", "target_source_sha256",
        "provenance_policy_version", "workspace_integrity_status",
    )
    for column in required:
        if column not in df.columns:
            findings.append({"check": f"missing_{column}_column", "severity": "error", "count": len(df),
                             "detail": f"Schema 3.0 requires {column}."})
            continue
        blank = df[column].isna() | df[column].astype(str).str.strip().eq("")
        if blank.any():
            findings.append({"check": f"missing_{column}", "severity": "error", "count": int(blank.sum()),
                             "detail": f"{int(blank.sum())} attempts are missing {column}."})

    if {"requested_commit", "resolved_commit"} <= set(df.columns):
        requested = df["requested_commit"].astype(str).str.strip().str.lower()
        resolved = df["resolved_commit"].astype(str).str.strip().str.lower()
        mismatch = ~requested.eq(resolved)
        if mismatch.any():
            findings.append({"check": "requested_resolved_commit_mismatch", "severity": "error",
                             "count": int(mismatch.sum()), "detail": "Requested and resolved commits differ."})
        if "commit_hash" in df.columns:
            alias_mismatch = ~df["commit_hash"].astype(str).str.strip().str.lower().eq(resolved)
            if alias_mismatch.any():
                findings.append({"check": "legacy_commit_alias_mismatch", "severity": "error",
                                 "count": int(alias_mismatch.sum()), "detail": "commit_hash differs from resolved_commit."})

    if {"target_id", "repository_identity", "resolved_commit"} <= set(df.columns):
        identities = df.groupby("target_id", dropna=False)[["repository_identity", "resolved_commit"]].nunique(dropna=False)
        conflicts = identities.gt(1).any(axis=1)
        if conflicts.any():
            findings.append({"check": "target_identity_collision", "severity": "error",
                             "count": int(conflicts.sum()), "detail": "A target_id maps to multiple repository revisions."})

    if {"candidate_cohort_id", "resolved_commit"} <= set(df.columns):
        cohort = df.dropna(subset=["candidate_cohort_id"]).groupby("candidate_cohort_id")["resolved_commit"].nunique()
        conflicts = cohort.gt(1)
        if conflicts.any():
            findings.append({"check": "cohort_revision_mismatch", "severity": "error",
                             "count": int(conflicts.sum()), "detail": "A candidate cohort spans multiple resolved commits."})

    if "workspace_integrity_status" in df.columns:
        verified = {"VerifiedClean", "VerifiedExpectedChanges"}
        validated = df.get("validated_success", pd.Series(False, index=df.index)).fillna(False).astype(bool)
        positive = df.get("positive_impact", pd.Series(False, index=df.index)).fillna(False).astype(bool)
        blocking_success = (validated | positive) & ~df["workspace_integrity_status"].isin(verified)
        if blocking_success.any():
            findings.append({"check": "success_with_blocking_integrity", "severity": "error",
                             "count": int(blocking_success.sum()),
                             "detail": "Validated or positive-impact rows have blocking workspace integrity."})

    if {"tool_artifact_path", "resolved_commit"} <= set(df.columns):
        nonblank = df[df["tool_artifact_path"].fillna("").astype(str).str.strip().ne("")]
        collisions = nonblank.groupby("tool_artifact_path")["resolved_commit"].nunique().gt(1)
        if collisions.any():
            findings.append({"check": "cross_revision_artifact_path_collision", "severity": "error",
                             "count": int(collisions.sum()), "detail": "An artifact path is shared by multiple revisions."})
    return findings


def audit_child_provenance(attempts: pd.DataFrame, children: pd.DataFrame) -> list[dict]:
    """Ensure generated-test rows inherit their parent attempt provenance exactly."""
    if attempts.empty or children.empty or "attempt_id" not in attempts.columns or "attempt_id" not in children.columns:
        return []
    fields = [
        "target_id", "repository_identity", "requested_commit", "resolved_commit",
        "target_manifest_sha256", "target_source_sha256", "provenance_policy_version",
        "workspace_integrity_status",
    ]
    available = [field for field in fields if field in attempts.columns and field in children.columns]
    parent = attempts.drop_duplicates("attempt_id").set_index("attempt_id")[available]
    joined = children.join(parent, on="attempt_id", rsuffix="_parent")
    mismatch = pd.Series(False, index=joined.index)
    for field in available:
        mismatch |= joined[field].fillna("").astype(str).ne(joined[f"{field}_parent"].fillna("").astype(str))
    if mismatch.any():
        return [{"check": "child_provenance_mismatch", "severity": "error", "count": int(mismatch.sum()),
                 "detail": "Generated-test rows do not inherit parent attempt provenance."}]
    return []


def _assertion_finding(check: str, count: int, detail: str) -> dict:
    return {"check": check, "severity": "error", "count": int(count), "detail": detail}


def _blank(series: pd.Series) -> pd.Series:
    return series.isna() | series.astype(str).str.strip().isin({"", "nan", "<NA>"})


def _json_paths(value: object) -> list[list[dict]] | None:
    if value is None:
        return None
    if not isinstance(value, (list, dict)):
        try:
            if bool(pd.isna(value)):
                return None
        except (TypeError, ValueError):
            pass
    try:
        decoded = value if isinstance(value, (list, dict)) else json.loads(str(value))
    except (TypeError, ValueError, json.JSONDecodeError):
        return None
    if isinstance(decoded, dict):
        decoded = decoded.get("paths", decoded.get("Paths"))
    if not isinstance(decoded, list):
        return None
    if not decoded:
        return []
    if all(isinstance(item, dict) for item in decoded):
        # Accept either [{steps:[...]}] or a single flat step list.
        if all("steps" in item or "Steps" in item for item in decoded):
            return [
                item.get("steps", item.get("Steps", []))
                for item in decoded
            ]
        # The producer's ordered_lineage_paths_json is one flat list holding every
        # step of every path, keyed by input and path index. Read as a single path,
        # a second path's steps restart at 0 and look non-contiguous.
        keyed = [
            (_step_value(step, "input_index", "InputIndex"),
             _step_value(step, "path_index", "PathIndex"))
            for step in decoded
        ]
        if all(key != (None, None) for key in keyed):
            grouped: dict[tuple, list[dict]] = {}
            for key, step in zip(keyed, decoded):
                grouped.setdefault(key, []).append(step)
            return [
                sorted(steps, key=lambda s: _step_value(s, "step_index", "StepIndex") or 0)
                for _, steps in sorted(grouped.items(), key=lambda item: str(item[0]))
            ]
        return [decoded]
    if all(isinstance(item, list) for item in decoded):
        return decoded
    return None


def _step_value(step: dict, *names: str) -> object:
    for name in names:
        if name in step:
            return step[name]
    return None


def _audit_serialized_paths(assertions: pd.DataFrame) -> list[dict]:
    if assertions.empty or "lineage_paths_json" not in assertions.columns:
        return []
    broken = 0
    traced_without_terminal = 0
    trivial_with_bad_terminal = 0
    for row in assertions.itertuples(index=False):
        paths = _json_paths(getattr(row, "lineage_paths_json", None))
        category = str(getattr(row, "category", ""))
        if paths is None:
            broken += 1
            continue
        has_production = False
        has_unresolved = False
        for path in paths:
            if not isinstance(path, list) or not path:
                broken += 1
                continue
            indexes = [
                _step_value(step, "step_index", "StepIndex")
                for step in path if isinstance(step, dict)
            ]
            try:
                normalized_indexes = [int(index) for index in indexes]
            except (TypeError, ValueError):
                broken += 1
                continue
            if normalized_indexes != list(range(len(path))):
                broken += 1
            terminal = path[-1]
            outcome = str(_step_value(terminal, "outcome", "Outcome") or "")
            if outcome not in {"Traced", "Trivial", "Unresolved"}:
                broken += 1
            step_kind = str(_step_value(terminal, "step_kind", "StepKind") or "")
            member_id = _step_value(terminal, "member_id", "MemberId")
            if step_kind == "ProductionMember" and outcome == "Traced" and not pd.isna(member_id):
                has_production = True
            if outcome == "Unresolved":
                has_unresolved = True
        if category == "Traced" and not has_production:
            traced_without_terminal += 1
        if category == "Trivial" and (has_production or has_unresolved):
            trivial_with_bad_terminal += 1

    findings: list[dict] = []
    if broken:
        findings.append(_assertion_finding(
            "broken_assertion_path_terminal",
            broken,
            "Assertion lineage paths must have contiguous steps and one recognized terminal.",
        ))
    if traced_without_terminal:
        findings.append(_assertion_finding(
            "traced_assertion_without_production_terminal",
            traced_without_terminal,
            "Traced assertions require a terminal resolved ProductionMember step.",
        ))
    if trivial_with_bad_terminal:
        findings.append(_assertion_finding(
            "trivial_assertion_with_nontrivial_terminal",
            trivial_with_bad_terminal,
            "Trivial assertions cannot contain production or unresolved terminals.",
        ))
    return findings


def audit_assertion_lineage(
    attempts: pd.DataFrame,
    generated_tests: pd.DataFrame,
    assertions: pd.DataFrame,
    *,
    sidecar_present: bool | None = None,
) -> list[dict]:
    """Enforce schema-4 assertion publication and schema-3 missingness semantics."""
    findings: list[dict] = []
    if attempts.empty:
        return findings

    versions = attempts.get(
        "results_schema_version", pd.Series("", index=attempts.index)
    ).astype(str).str.strip()
    legacy = versions.eq(LEGACY_RESULTS_SCHEMA_VERSION)
    schema4 = versions.eq(ASSERTION_RESULTS_SCHEMA_VERSION)

    if legacy.any():
        statuses = attempts.get(
            "assertion_measurement_status", pd.Series(pd.NA, index=attempts.index)
        )
        count_columns = [
            "recognized_assertion_count", "traced_assertion_count",
            "trivial_assertion_count", "unresolved_assertion_count",
        ]
        converted = ~statuses.astype(str).eq("NotMeasured")
        for column in count_columns:
            if column in attempts.columns:
                converted |= attempts[column].notna()
        if converted[legacy].any():
            findings.append(_assertion_finding(
                "historical_assertion_zero_conversion",
                int(converted[legacy].sum()),
                "Schema-3 rows must derive NotMeasured with null assertion counts.",
            ))

    if not schema4.any():
        return findings
    current = attempts.loc[schema4].copy()

    required_columns = [
        "assertion_measurement_status", "assertion_measurement_reason",
        "assertion_policy_version", "assertion_catalog_version",
        "assertion_max_depth", "recognized_assertion_count",
        "traced_assertion_count", "trivial_assertion_count",
        "unresolved_assertion_count",
    ]
    for column in required_columns:
        if column not in current.columns:
            findings.append(_assertion_finding(
                f"missing_{column}_column", len(current),
                f"Schema 4 requires {column}.",
            ))
    if any(column not in current.columns for column in required_columns):
        return findings

    invalid_status = ~current["assertion_measurement_status"].isin(
        ASSERTION_MEASUREMENT_STATUSES - {"NotMeasured"}
    )
    if invalid_status.any():
        findings.append(_assertion_finding(
            "invalid_assertion_measurement_status", int(invalid_status.sum()),
            "Schema-4 attempt rows contain an invalid assertion measurement status.",
        ))

    missing_policy = (
        _blank(current["assertion_policy_version"])
        | ~current["assertion_policy_version"].eq(ASSERTION_POLICY_VERSION)
        | _blank(current["assertion_catalog_version"])
        | ~current["assertion_catalog_version"].eq(ASSERTION_CATALOG_VERSION)
        | pd.to_numeric(current["assertion_max_depth"], errors="coerce").le(0)
    )
    if missing_policy.any():
        findings.append(_assertion_finding(
            "missing_or_unsupported_assertion_policy", int(missing_policy.sum()),
            "Schema-4 assertion evidence requires supported policy, catalog, and depth.",
        ))

    needs_reason = current["assertion_measurement_status"].isin(
        {"Partial", "Unavailable"}
    )
    missing_reason = needs_reason & _blank(current["assertion_measurement_reason"])
    if missing_reason.any():
        findings.append(_assertion_finding(
            "missing_assertion_measurement_reason", int(missing_reason.sum()),
            "Partial and unavailable assertion measurements require a stable reason.",
        ))
    invalid_reason = (
        current["assertion_measurement_status"].eq("Unavailable")
        & ~current["assertion_measurement_reason"].isin(ASSERTION_UNAVAILABLE_REASONS)
    )
    if invalid_reason.any():
        findings.append(_assertion_finding(
            "invalid_assertion_measurement_reason", int(invalid_reason.sum()),
            "Unavailable measurements require a stable unavailable reason code.",
        ))

    count_columns = [
        "recognized_assertion_count", "traced_assertion_count",
        "trivial_assertion_count", "unresolved_assertion_count",
    ]
    numeric = {
        column: pd.to_numeric(current[column], errors="coerce")
        for column in count_columns
    }
    unavailable = current["assertion_measurement_status"].eq("Unavailable")
    unavailable_with_counts = unavailable & pd.concat(
        [numeric[column].notna() for column in count_columns], axis=1
    ).any(axis=1)
    if unavailable_with_counts.any():
        findings.append(_assertion_finding(
            "unavailable_assertion_counts_not_null", int(unavailable_with_counts.sum()),
            "Unavailable assertion counts must be null, never zero.",
        ))
    # Only measured rows carry counts. NotApplicable rows hold nulls by contract, and
    # NaN != NaN would otherwise report every one of them as a mismatch.
    reconcile = (
        numeric["recognized_assertion_count"]
        != numeric["traced_assertion_count"]
        + numeric["trivial_assertion_count"]
        + numeric["unresolved_assertion_count"]
    ) & current["assertion_measurement_status"].isin({"Complete", "Partial"})
    if reconcile.any():
        findings.append(_assertion_finding(
            "assertion_attempt_count_mismatch", int(reconcile.sum()),
            "Attempt recognized counts do not reconcile to category counts.",
        ))

    comparable_fields = {
        "candidate_key", "lane", "assertion_policy_version",
        "assertion_catalog_version", "assertion_max_depth",
    }
    if comparable_fields <= set(current.columns):
        policy_counts = current.groupby("candidate_key", dropna=False).agg(
            lanes=("lane", "nunique"),
            policies=("assertion_policy_version", "nunique"),
            catalogs=("assertion_catalog_version", "nunique"),
            depths=("assertion_max_depth", "nunique"),
        )
        mismatches = (
            policy_counts["lanes"].gt(1)
            & policy_counts[["policies", "catalogs", "depths"]].gt(1).any(axis=1)
        )
        if mismatches.any():
            findings.append(_assertion_finding(
                "cross_lane_assertion_policy_mismatch", int(mismatches.sum()),
                "Comparable lane rows use different assertion policies.",
            ))

    if not generated_tests.empty:
        child = generated_tests[
            generated_tests.get(
                "results_schema_version",
                pd.Series("", index=generated_tests.index),
            ).astype(str).eq(ASSERTION_RESULTS_SCHEMA_VERSION)
        ].copy()
        if "assertion_measurement_status" in child.columns:
            invalid_child_status = ~child["assertion_measurement_status"].isin(
                CHILD_ROW_STATUSES
            )
            if invalid_child_status.any():
                findings.append(_assertion_finding(
                    "invalid_assertion_summary_status",
                    int(invalid_child_status.sum()),
                    "Generated-test rows contain an invalid assertion summary status.",
                ))
        if set(count_columns) <= set(child.columns):
            child_numbers = {
                column: pd.to_numeric(child[column], errors="coerce")
                for column in count_columns
            }
            classified = child.get(
                "assertion_measurement_status",
                pd.Series("", index=child.index),
            ).isin(CHILD_ROW_CLASSIFIED)
            child_bad = classified & (
                child_numbers["recognized_assertion_count"]
                != child_numbers["traced_assertion_count"]
                + child_numbers["trivial_assertion_count"]
                + child_numbers["unresolved_assertion_count"]
            )
            if child_bad.any():
                findings.append(_assertion_finding(
                    "assertion_child_count_mismatch", int(child_bad.sum()),
                    "Generated-test recognized counts do not reconcile.",
                ))
            if "attempt_id" in child.columns:
                summed = child.loc[classified].groupby("attempt_id")[
                    count_columns
                ].sum(min_count=1)
                parents = current.set_index("attempt_id")
                joined = parents[count_columns].join(
                    summed, how="left", rsuffix="_children"
                )
                bad_parent = pd.Series(False, index=joined.index)
                for column in count_columns:
                    child_column = f"{column}_children"
                    bad_parent |= pd.to_numeric(
                        joined[column], errors="coerce"
                    ).ne(pd.to_numeric(joined[child_column], errors="coerce"))
                complete_or_partial = parents[
                    "assertion_measurement_status"
                ].isin({"Complete", "Partial"})
                bad_parent &= complete_or_partial.reindex(joined.index).fillna(False)
                if bad_parent.any():
                    findings.append(_assertion_finding(
                        "assertion_attempt_child_count_mismatch",
                        int(bad_parent.sum()),
                        "Attempt assertion aggregates do not equal classified child sums.",
                    ))

    claimed = pd.to_numeric(
        current["recognized_assertion_count"], errors="coerce"
    ).fillna(0).gt(0)
    if sidecar_present is False and claimed.any():
        findings.append(_assertion_finding(
            "missing_assertion_sidecar", int(claimed.sum()),
            "Schema-4 rows claim assertion evidence but the assertion sidecar is missing.",
        ))
        return findings

    if assertions.empty:
        if claimed.any():
            findings.append(_assertion_finding(
                "missing_assertion_sidecar_rows", int(claimed.sum()),
                "Schema-4 rows claim recognized assertions but no sidecar rows exist.",
            ))
        return findings

    invalid_category = ~assertions["category"].isin(ASSERTION_CATEGORIES)
    if invalid_category.any():
        findings.append(_assertion_finding(
            "invalid_assertion_category", int(invalid_category.sum()),
            "Assertion sidecar contains categories outside the complete vocabulary.",
        ))
    invalid_sidecar_version = ~assertions["assertion_schema_version"].astype(str).eq(
        ASSERTION_SIDECAR_SCHEMA_VERSION
    )
    if invalid_sidecar_version.any():
        findings.append(_assertion_finding(
            "invalid_assertion_sidecar_version", int(invalid_sidecar_version.sum()),
            "Assertion sidecar version is unsupported.",
        ))
    sidecar_policy = (
        ~assertions["assertion_policy_version"].eq(ASSERTION_POLICY_VERSION)
        | ~assertions["assertion_catalog_version"].eq(ASSERTION_CATALOG_VERSION)
    )
    if sidecar_policy.any():
        findings.append(_assertion_finding(
            "missing_or_unsupported_assertion_observation_policy",
            int(sidecar_policy.sum()),
            "Assertion observations require supported policy and catalog versions.",
        ))

    # Observation and summary ids are per-database autoincrement values. A results
    # file that combines several targets - each its own experiment run - repeats
    # them legitimately, so identity is scoped by the run that produced the row.
    run_key = ["experiment_run_uid"] if "experiment_run_uid" in assertions.columns else []
    duplicate_ids = assertions.duplicated(run_key + ["observation_id"], keep=False)
    if duplicate_ids.any():
        findings.append(_assertion_finding(
            "duplicate_assertion_observation_id", int(duplicate_ids.sum()),
            "Assertion observation IDs must be unique within an experiment run.",
        ))
    if {"generated_test_assertion_summary_id", "assertion_ordinal"} <= set(assertions.columns):
        duplicate_identity = assertions.duplicated(
            run_key + ["generated_test_assertion_summary_id", "assertion_ordinal"],
            keep=False,
        )
        if duplicate_identity.any():
            findings.append(_assertion_finding(
                "duplicate_assertion_summary_ordinal", int(duplicate_identity.sum()),
                "One logical assertion is duplicated within a generated test summary.",
            ))

    orphan_attempt = ~assertions["attempt_id"].isin(current["attempt_id"])
    if orphan_attempt.any():
        findings.append(_assertion_finding(
            "orphan_assertion_observation", int(orphan_attempt.sum()),
            "Assertion observations must reference an exported attempt.",
        ))

    traced = assertions["category"].eq("Traced")
    missing_production = traced & _blank(assertions["resolved_production_member_ids"])
    if missing_production.any():
        findings.append(_assertion_finding(
            "traced_assertion_without_production", int(missing_production.sum()),
            "Traced assertions require at least one resolved production member.",
        ))
    unresolved = assertions["category"].eq("Unresolved")
    bad_unresolved_reason = unresolved & ~assertions["resolution_code"].isin(
        ASSERTION_UNRESOLVED_REASONS
    )
    if bad_unresolved_reason.any():
        findings.append(_assertion_finding(
            "missing_or_invalid_unresolved_reason", int(bad_unresolved_reason.sum()),
            "Unresolved assertions require a stable unresolved reason.",
        ))

    by_attempt = assertions.groupby("attempt_id").size()
    exported_counts = current.set_index("attempt_id")["recognized_assertion_count"]
    sidecar_count_mismatch = pd.to_numeric(
        exported_counts, errors="coerce"
    ).fillna(-1).ne(by_attempt.reindex(exported_counts.index, fill_value=0))
    sidecar_count_mismatch &= current.set_index("attempt_id")[
        "assertion_measurement_status"
    ].isin({"Complete", "Partial"})
    if sidecar_count_mismatch.any():
        findings.append(_assertion_finding(
            "assertion_sidecar_count_mismatch", int(sidecar_count_mismatch.sum()),
            "Attempt recognized counts do not equal assertion sidecar row counts.",
        ))

    if (
        not generated_tests.empty
        and "generated_test_assertion_summary_id" in generated_tests.columns
    ):
        summary_ids = set(
            pd.to_numeric(
                generated_tests["generated_test_assertion_summary_id"],
                errors="coerce",
            ).dropna().astype(int)
        )
        observation_summary_ids = pd.to_numeric(
            assertions["generated_test_assertion_summary_id"], errors="coerce"
        )
        orphan_summary = observation_summary_ids.notna() & ~observation_summary_ids.isin(
            summary_ids
        )
        if orphan_summary.any():
            findings.append(_assertion_finding(
                "orphan_assertion_summary", int(orphan_summary.sum()),
                "Assertion observations must reference an exported generated-test summary.",
            ))

    if (
        not generated_tests.empty
        and {"attempt_id", "generated_test_member_id",
             "recognized_assertion_count"} <= set(generated_tests.columns)
        and {"attempt_id", "test_member_id"} <= set(assertions.columns)
    ):
        child = generated_tests.dropna(
            subset=["attempt_id", "generated_test_member_id"]
        ).copy()
        observed = assertions.dropna(subset=["attempt_id", "test_member_id"]).copy()
        child["_member_key"] = pd.to_numeric(
            child["generated_test_member_id"], errors="coerce"
        )
        observed["_member_key"] = pd.to_numeric(
            observed["test_member_id"], errors="coerce"
        )
        child_keys = set(zip(child["attempt_id"], child["_member_key"]))
        observed_keys = list(zip(observed["attempt_id"], observed["_member_key"]))
        orphan_child = sum(key not in child_keys for key in observed_keys)
        if orphan_child:
            findings.append(_assertion_finding(
                "orphan_assertion_generated_test",
                orphan_child,
                "Assertion observations must reference an exported generated test.",
            ))
        observed_counts = observed.groupby(
            ["attempt_id", "_member_key"]
        ).size()
        child_counts = pd.to_numeric(
            child["recognized_assertion_count"], errors="coerce"
        )
        actual_counts = pd.Series(
            [
                observed_counts.get((attempt_id, member_id), 0)
                for attempt_id, member_id in zip(
                    child["attempt_id"], child["_member_key"]
                )
            ],
            index=child.index,
        )
        classified = child.get(
            "assertion_measurement_status",
            pd.Series("", index=child.index),
        ).isin(CHILD_ROW_CLASSIFIED)
        child_sidecar_mismatch = classified & child_counts.ne(actual_counts)
        if child_sidecar_mismatch.any():
            findings.append(_assertion_finding(
                "assertion_child_sidecar_count_mismatch",
                int(child_sidecar_mismatch.sum()),
                "Generated-test recognized counts do not equal owned sidecar rows.",
            ))

    findings += _audit_serialized_paths(assertions)
    return findings


def audit_assertion_persistence(
    measurements: pd.DataFrame,
    summaries: pd.DataFrame,
    observations: pd.DataFrame,
    steps: pd.DataFrame,
) -> list[dict]:
    """Audit owner links and ordered paths in one SQLite evidence bundle."""
    findings: list[dict] = []
    grains = {
        "measurement": measurements,
        "summary": summaries,
        "observation": observations,
        "step": steps,
    }
    for name, frame in grains.items():
        if not frame.empty and "id" in frame.columns:
            duplicates = frame["id"].duplicated(keep=False)
            if duplicates.any():
                findings.append(_assertion_finding(
                    f"duplicate_assertion_{name}_id",
                    int(duplicates.sum()),
                    f"Assertion {name} IDs must be unique.",
                ))

    if not summaries.empty and "assertion_lineage_measurement_id" in summaries.columns:
        owners = set(measurements.get("id", pd.Series(dtype=int)))
        orphan = ~summaries["assertion_lineage_measurement_id"].isin(owners)
        if orphan.any():
            findings.append(_assertion_finding(
                "orphan_generated_test_assertion_summary",
                int(orphan.sum()),
                "Generated-test assertion summaries require a measurement owner.",
            ))

    if not observations.empty and "generated_test_assertion_summary_id" in observations.columns:
        owners = set(summaries.get("id", pd.Series(dtype=int)))
        orphan = ~observations["generated_test_assertion_summary_id"].isin(owners)
        if orphan.any():
            findings.append(_assertion_finding(
                "orphan_persisted_assertion_observation",
                int(orphan.sum()),
                "Persisted assertion observations require a generated-test summary.",
            ))

    if not steps.empty and "assertion_observation_id" in steps.columns:
        owners = set(observations.get("id", pd.Series(dtype=int)))
        orphan = ~steps["assertion_observation_id"].isin(owners)
        if orphan.any():
            findings.append(_assertion_finding(
                "orphan_assertion_lineage_step",
                int(orphan.sum()),
                "Assertion lineage steps require an assertion observation owner.",
            ))

    step_key = [
        "assertion_observation_id", "input_index", "path_index", "step_index",
    ]
    if not steps.empty and set(step_key) <= set(steps.columns):
        duplicates = steps.duplicated(step_key, keep=False)
        if duplicates.any():
            findings.append(_assertion_finding(
                "duplicate_assertion_lineage_step",
                int(duplicates.sum()),
                "Assertion path step identities must be unique.",
            ))
        broken = 0
        for _, path in steps.groupby(step_key[:-1], dropna=False):
            ordered = path.sort_values("step_index")
            indexes = pd.to_numeric(ordered["step_index"], errors="coerce").tolist()
            if indexes != list(range(len(ordered))):
                broken += 1
                continue
            terminal = ordered.iloc[-1]
            terminal_count = int(
                ordered["outcome"].isin({"Traced", "Trivial", "Unresolved"}).sum()
            )
            if terminal_count != 1 or terminal.get("outcome") not in {
                "Traced", "Trivial", "Unresolved",
            }:
                broken += 1
        if broken:
            findings.append(_assertion_finding(
                "broken_persisted_assertion_path_terminal",
                broken,
                "Persisted assertion paths require contiguous steps and one terminal.",
            ))

    if (
        not observations.empty
        and not steps.empty
        and {"id", "category"} <= set(observations.columns)
        and {"assertion_observation_id", "step_kind", "outcome", "member_id"} <= set(steps.columns)
    ):
        production_terminals = set(steps.loc[
            steps["step_kind"].eq("ProductionMember")
            & steps["outcome"].eq("Traced")
            & steps["member_id"].notna(),
            "assertion_observation_id",
        ])
        traced_ids = set(observations.loc[
            observations["category"].eq("Traced"), "id"
        ])
        missing = traced_ids - production_terminals
        if missing:
            findings.append(_assertion_finding(
                "persisted_traced_assertion_without_production",
                len(missing),
                "Persisted traced assertions require a resolved production terminal.",
            ))

    if (
        not summaries.empty
        and not observations.empty
        and {"id", "status", "recognized_assertion_count"} <= set(summaries.columns)
    ):
        actual = observations.groupby(
            "generated_test_assertion_summary_id"
        ).size()
        classified = summaries["status"].isin(
            {"Classified", "NoRecognizedAssertions"}
        )
        expected = pd.to_numeric(
            summaries["recognized_assertion_count"], errors="coerce"
        )
        observed = summaries["id"].map(actual).fillna(0)
        mismatch = classified & expected.ne(observed)
        if mismatch.any():
            findings.append(_assertion_finding(
                "persisted_assertion_summary_count_mismatch",
                int(mismatch.sum()),
                "Persisted summary counts do not equal owned observations.",
            ))
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
    results_schema_version: str = ASSERTION_RESULTS_SCHEMA_VERSION,
) -> bool:
    """Entry point for the ``audit`` CLI command."""
    out = ensure_output_dir(output_dir)

    if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION:
        raw, generated, _, raw_assertions = read_schema4_result_bundle(
            list(results), require_assertion_sidecar=False
        )
    elif results_schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        raw, generated, _ = read_result_grains(
            list(results),
            expected_schema_version=LEGACY_RESULTS_SCHEMA_VERSION,
        )
        raw_assertions = pd.DataFrame()
    else:
        raise ValueError(
            f"Unsupported results_schema_version: {results_schema_version}."
        )
    df = build_attempts_dataset(raw, generated)
    normalized_generated = build_generated_tests_dataset_from_raw(generated)
    assertions = normalize_assertion_observations(raw_assertions)

    findings: list[dict] = []
    findings += audit_lane_labels(df)
    findings += audit_missing_coverage(df)
    findings += audit_missing_mutation(df)
    findings += audit_missing_tokens(df)
    findings += audit_token_usage(df)
    findings += audit_duplicate_rows(df)
    findings += audit_agentic_identity_and_artifacts(df)
    findings += audit_agentic_no_post_attempt(df)
    findings += audit_generated_test_links(df)
    findings += audit_outcome_classification(df)
    findings += audit_missing_repo_metadata(df)
    findings += audit_pinned_provenance(df)
    findings += audit_child_provenance(raw, generated)
    findings += audit_assertion_lineage(
        df,
        normalized_generated,
        assertions,
        sidecar_present=not raw_assertions.empty,
    )
    if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION:
        from analysis.db import connect, get_assertion_lineage_bundle

        for db_path in find_databases(list(db_paths)):
            with connect(db_path) as connection:
                findings += audit_assertion_persistence(
                    *get_assertion_lineage_bundle(connection)
                )

    report = build_audit_report(findings, df)

    with open(out / "audit_report.json", "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2, default=str)

    pd.DataFrame(findings).to_csv(out / "audit_report.csv", index=False)
    _write_markdown_report(report, out / "audit_report.md")

    status = "PASS" if report["summary"]["pass"] else "FAIL"
    print(f"Audit {status}: {report['summary']['error_count']} errors, "
          f"{report['summary']['warning_count']} warnings. "
          f"Report written to {out}")
    return bool(report["summary"]["pass"])
