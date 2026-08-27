"""Structural checks over the MSR validation frames.

Every check is a rate with an explicit numerator and denominator, computed per
repository. None of them need an oracle or a human rater, so they run before any
manual sampling: a construct with a systematic fault should be fixed rather than
sampled, and several of the checks (unattributed mutants, unattributed smells)
are themselves reportable attribution results.

Line-base note: TestMap stores entity spans zero-based, while report-derived line
numbers (test smells, Stryker locations) are one-based. Containment checks adjust
for that. Because member spans are multi-line, containment cannot detect an
off-by-one; ``*_containment_base_ambiguous`` counts the rows where the two
conventions disagree, which is the only place that error becomes visible here.
"""

from __future__ import annotations

import pandas as pd


CHECK_SEVERITY = {
    # A defect: the row cannot be correct as stored.
    "defect": (
        "metrics_orphan_entity",
        "metrics_duplicate_entity",
        "metrics_mi_out_of_range",
        "smells_orphan_member",
        "smells_containment_violation",
        "coverage_orphan_entity",
        "coverage_rate_out_of_range",
        "coverage_rate_inconsistent",
        "mutants_orphan_member",
        "mutants_containment_violation",
        "mappings_orphan_source",
        "mappings_orphan_test",
        "mappings_self_reference",
        "mappings_duplicate_pair",
    ),
    # Missing data: the row is absent rather than wrong.
    "coverage_gap": (
        "members_missing_metrics",
        "objects_missing_metrics",
        "members_missing_coverage",
        "members_missing_mutants",
        "smells_unattributed",
        "mutants_unattributed",
        "smells_line_missing",
        "mutants_location_unparsed",
    ),
    # Worth a look, not necessarily wrong.
    "suspect": (
        "metrics_cc_zero_on_method",
        "metrics_sloc_zero_on_method",
        "smells_on_non_test_member",
        "smells_containment_base_ambiguous",
        "coverage_lines_valid_zero",
        "coverage_counts_missing_with_rate",
        "mutants_containment_base_ambiguous",
        "mappings_path_length_zero",
        "mappings_trace_steps_zero",
        "mappings_source_is_test_member",
        "mappings_test_not_test_member",
    ),
}

_SEVERITY_BY_CHECK = {
    check: severity for severity, checks in CHECK_SEVERITY.items() for check in checks
}


def _record(rows: list[dict], repo_key: str, construct: str, check: str,
            numerator: int, denominator: int) -> None:
    rows.append({
        "repo_key": repo_key,
        "construct": construct,
        "check": check,
        "severity": _SEVERITY_BY_CHECK.get(check, "suspect"),
        "numerator": int(numerator),
        "denominator": int(denominator),
        "rate": (float(numerator) / denominator) if denominator else float("nan"),
    })


def _entity_index(entities: pd.DataFrame) -> pd.DataFrame:
    """Return entities indexed by (entity_kind, entity_id) for joins."""
    if entities.empty:
        return entities
    return entities.set_index(["entity_kind", "entity_id"])


def _known(entities: pd.DataFrame, kind: str) -> set:
    if entities.empty:
        return set()
    return set(entities.loc[entities["entity_kind"] == kind, "entity_id"].dropna().astype(int))


def _contains(start: pd.Series, end: pd.Series, line: pd.Series, one_based: bool) -> pd.Series:
    """Return whether *line* falls inside the ``[start, end]`` span."""
    adjusted = line - 1 if one_based else line
    return adjusted.between(start, end)


# ---------------------------------------------------------------------------
# Per-construct checks
# ---------------------------------------------------------------------------

def check_code_metrics(rows, repo_key, entities, metrics) -> None:
    members = entities[entities["entity_kind"] == "member"]
    objects = entities[entities["entity_kind"] == "object"]

    # A repository with no metric rows at all did not run metric collection.
    # That is an availability fact (msr_repositories.has_metrics), not a gap
    # rate — recording it here would swamp the real missing-row rate.
    if metrics.empty:
        return

    metric_members = set(metrics.loc[metrics["entity_kind"] == "member", "entity_id"].astype(int))
    metric_objects = set(metrics.loc[metrics["entity_kind"] == "object", "entity_id"].astype(int))

    member_ids = _known(entities, "member")
    object_ids = _known(entities, "object")

    _record(rows, repo_key, "code_metrics", "members_missing_metrics",
            len(member_ids - metric_members), len(member_ids))
    _record(rows, repo_key, "code_metrics", "objects_missing_metrics",
            len(object_ids - metric_objects), len(object_ids))
    _record(rows, repo_key, "code_metrics", "metrics_orphan_entity",
            len((metric_members - member_ids) | (metric_objects - object_ids)), len(metrics))
    _record(rows, repo_key, "code_metrics", "metrics_duplicate_entity",
            int(metrics.duplicated(["entity_kind", "entity_id"]).sum()), len(metrics))

    mi = metrics["maintainability_index"]
    _record(rows, repo_key, "code_metrics", "metrics_mi_out_of_range",
            int(((mi < 0) | (mi > 100)).sum()), len(metrics))

    # Zero complexity is legitimate for interface members, abstract declarations,
    # and auto-properties; it is only suspect on a concrete method.
    methods = members[members["kind"] == "method"]
    method_metrics = metrics[
        (metrics["entity_kind"] == "member")
        & (metrics["entity_id"].isin(set(methods["entity_id"].astype(int))))
    ]
    if not method_metrics.empty:
        _record(rows, repo_key, "code_metrics", "metrics_cc_zero_on_method",
                int((method_metrics["cyclomatic_complexity"] == 0).sum()), len(method_metrics))
        _record(rows, repo_key, "code_metrics", "metrics_sloc_zero_on_method",
                int((method_metrics["source_lines_of_code"] == 0).sum()), len(method_metrics))


def check_test_smells(rows, repo_key, entities, smells) -> None:
    if smells.empty:
        return

    total = len(smells)
    member_ids = _known(entities, "member")

    unattributed = smells["member_id"].isna() & smells["object_id"].isna()
    _record(rows, repo_key, "test_smells", "smells_unattributed", int(unattributed.sum()), total)

    attributed = smells.dropna(subset=["member_id"]).copy()
    attributed["member_id"] = attributed["member_id"].astype(int)
    _record(rows, repo_key, "test_smells", "smells_orphan_member",
            int((~attributed["member_id"].isin(member_ids)).sum()), total)
    _record(rows, repo_key, "test_smells", "smells_line_missing",
            int(smells["claimed_line"].isna().sum()), total)

    members = entities[entities["entity_kind"] == "member"]
    joined = attributed.merge(
        members[["entity_id", "start_line", "end_line", "is_test"]],
        left_on="member_id", right_on="entity_id", how="inner",
    ).dropna(subset=["claimed_line"])

    if joined.empty:
        return

    one_based = _contains(joined["start_line"], joined["end_line"], joined["claimed_line"], True)
    zero_based = _contains(joined["start_line"], joined["end_line"], joined["claimed_line"], False)

    _record(rows, repo_key, "test_smells", "smells_containment_violation",
            int((~one_based).sum()), len(joined))
    _record(rows, repo_key, "test_smells", "smells_containment_base_ambiguous",
            int((one_based != zero_based).sum()), len(joined))
    _record(rows, repo_key, "test_smells", "smells_on_non_test_member",
            int((joined["is_test"] == 0).sum()), len(joined))


def check_coverage(rows, repo_key, entities, coverage, has_report: bool) -> None:
    # No coverage rows and no coverage report means coverage was never collected
    # (availability, not a gap). A report with no rows is a real gap.
    if coverage.empty:
        if has_report:
            members = _known(entities, "member")
            _record(rows, repo_key, "coverage", "members_missing_coverage", len(members), len(members))
        return

    total = len(coverage)
    orphans = 0
    for kind in ("member", "object"):
        known = _known(entities, kind)
        subset = coverage[coverage["entity_kind"] == kind]
        if not subset.empty:
            orphans += int((~subset["entity_id"].astype(int).isin(known)).sum())
    _record(rows, repo_key, "coverage", "coverage_orphan_entity", orphans, total)

    _record(rows, repo_key, "coverage", "coverage_lines_valid_zero",
            int((coverage["lines_valid"] == 0).sum()), total)

    # A rate without its underlying counts cannot be re-derived. Persisted rates
    # with zeroed numerator and denominator are a distinct failure from a member
    # that genuinely has no lines.
    counts_missing = (
        (coverage["lines_valid"] == 0)
        & (coverage["lines_covered"] == 0)
        & (coverage["line_rate"] > 0)
    )
    _record(rows, repo_key, "coverage", "coverage_counts_missing_with_rate",
            int(counts_missing.sum()), total)

    rates_bad = (
        (coverage["line_rate"] < 0) | (coverage["line_rate"] > 1)
        | (coverage["branch_rate"] < 0) | (coverage["branch_rate"] > 1)
    )
    _record(rows, repo_key, "coverage", "coverage_rate_out_of_range", int(rates_bad.sum()), total)

    # line_rate should equal lines_covered / lines_valid.
    measurable = coverage[coverage["lines_valid"] > 0]
    if not measurable.empty:
        expected = measurable["lines_covered"] / measurable["lines_valid"]
        drift = (measurable["line_rate"] - expected).abs() > 0.01
        _record(rows, repo_key, "coverage", "coverage_rate_inconsistent",
                int(drift.sum()), len(measurable))

    member_ids = _known(entities, "member")
    covered = set(coverage.loc[coverage["entity_kind"] == "member", "entity_id"].astype(int))
    _record(rows, repo_key, "coverage", "members_missing_coverage",
            len(member_ids - covered), len(member_ids))


def check_mutants(rows, repo_key, entities, mutant_locations, has_report: bool) -> None:
    # Same rule as coverage: absent report means mutation testing never ran.
    if mutant_locations.empty:
        if has_report:
            members = _known(entities, "member")
            _record(rows, repo_key, "mutants", "members_missing_mutants", len(members), len(members))
        return

    total = len(mutant_locations)
    member_ids = _known(entities, "member")

    _record(rows, repo_key, "mutants", "mutants_unattributed",
            int(mutant_locations["member_id"].isna().sum()), total)
    _record(rows, repo_key, "mutants", "mutants_location_unparsed",
            int(mutant_locations["claimed_start_line"].isna().sum()), total)

    attributed = mutant_locations.dropna(subset=["member_id"]).copy()
    attributed["member_id"] = attributed["member_id"].astype(int)
    _record(rows, repo_key, "mutants", "mutants_orphan_member",
            int((~attributed["member_id"].isin(member_ids)).sum()), total)

    members = entities[entities["entity_kind"] == "member"]
    joined = attributed.merge(
        members[["entity_id", "start_line", "end_line"]],
        left_on="member_id", right_on="entity_id", how="inner",
    ).dropna(subset=["claimed_start_line"])

    if not joined.empty:
        one_based = _contains(joined["start_line"], joined["end_line"], joined["claimed_start_line"], True)
        zero_based = _contains(joined["start_line"], joined["end_line"], joined["claimed_start_line"], False)
        _record(rows, repo_key, "mutants", "mutants_containment_violation",
                int((~one_based).sum()), len(joined))
        _record(rows, repo_key, "mutants", "mutants_containment_base_ambiguous",
                int((one_based != zero_based).sum()), len(joined))

    mutated = set(attributed["member_id"])
    _record(rows, repo_key, "mutants", "members_missing_mutants",
            len(member_ids - mutated), len(member_ids))


def check_mappings(rows, repo_key, entities, mappings) -> None:
    if mappings.empty:
        return

    total = len(mappings)
    member_ids = _known(entities, "member")
    members = entities[entities["entity_kind"] == "member"]
    test_flag = dict(zip(members["entity_id"].astype(int), members["is_test"]))

    source = mappings["source_member_id"].astype("Int64")
    test = mappings["test_member_id"].astype("Int64")

    _record(rows, repo_key, "mappings", "mappings_orphan_source",
            int((~source.isin(member_ids)).sum()), total)
    _record(rows, repo_key, "mappings", "mappings_orphan_test",
            int((~test.isin(member_ids)).sum()), total)
    _record(rows, repo_key, "mappings", "mappings_self_reference",
            int((source == test).sum()), total)
    _record(rows, repo_key, "mappings", "mappings_duplicate_pair",
            int(mappings.duplicated(["source_member_id", "test_member_id"]).sum()), total)
    _record(rows, repo_key, "mappings", "mappings_path_length_zero",
            int((mappings["path_length"] == 0).sum()), total)

    if "trace_steps" in mappings:
        _record(rows, repo_key, "mappings", "mappings_trace_steps_zero",
                int((mappings["trace_steps"].fillna(0) == 0).sum()), total)

    # A mapping should run from production code to a test method.
    _record(rows, repo_key, "mappings", "mappings_source_is_test_member",
            int(source.map(test_flag).fillna(0).eq(1).sum()), total)
    _record(rows, repo_key, "mappings", "mappings_test_not_test_member",
            int(test.map(test_flag).fillna(1).eq(0).sum()), total)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def run_checks(bundle: dict) -> pd.DataFrame:
    """Run every structural check for one repository bundle."""
    repo_key = bundle["identity"]["repo_key"]
    entities = bundle["entities"]
    reports = bundle.get("reports", {})
    rows: list[dict] = []

    if entities.empty:
        _record(rows, repo_key, "entities", "entities_empty", 1, 1)
        return pd.DataFrame(rows)

    check_code_metrics(rows, repo_key, entities, bundle["code_metrics"])
    check_test_smells(rows, repo_key, entities, bundle["test_smells"])
    check_coverage(rows, repo_key, entities, bundle["coverage"],
                   has_report=bool(reports.get("coverage_reports", 0)))
    check_mutants(rows, repo_key, entities, bundle["mutant_locations"],
                  has_report=bool(reports.get("mutation_reports", 0)))
    check_mappings(rows, repo_key, entities, bundle["mappings"])

    return pd.DataFrame(rows)


def summarize_repository(bundle: dict) -> dict:
    """Return one row of per-repository counts and data availability."""
    identity = bundle["identity"]
    entities = bundle["entities"]
    reports = bundle.get("reports", {})

    members = entities[entities["entity_kind"] == "member"] if not entities.empty else entities
    objects = entities[entities["entity_kind"] == "object"] if not entities.empty else entities

    mutant_locations = bundle["mutant_locations"]

    return {
        **{k: v for k, v in identity.items()},
        "files": reports.get("files", 0),
        "objects": len(objects),
        "members": len(members),
        "test_members": int(members["is_test"].sum()) if not members.empty else 0,
        "generated_members": int(members["is_generated"].sum()) if not members.empty else 0,
        "code_metrics": len(bundle["code_metrics"]),
        "test_smells": len(bundle["test_smells"]),
        "coverage_rows": len(bundle["coverage"]),
        "mutants": len(mutant_locations),
        "mutant_member_groups": len(bundle["mutants"]),
        "mappings": len(bundle["mappings"]),
        "coverage_reports": reports.get("coverage_reports", 0),
        "mutation_reports": reports.get("mutation_reports", 0),
        "test_runs": reports.get("test_runs", 0),
        "has_metrics": len(bundle["code_metrics"]) > 0,
        "has_smells": len(bundle["test_smells"]) > 0,
        "has_coverage": len(bundle["coverage"]) > 0,
        "has_mutants": len(mutant_locations) > 0,
        "has_mappings": len(bundle["mappings"]) > 0,
    }
