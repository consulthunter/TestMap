"""Export ML/training-ready source-test mapping datasets from a TestMap SQLite database.

Row grains:
  mapping   One row per source-test mapping (default).
  candidate One row per candidate source method.
  pair      One row per (source method, test method) pair candidate.

Outputs depend on --grain:
  training_mappings.csv
  training_candidates.csv
  training_pairs.csv
"""

from __future__ import annotations

from pathlib import Path

import pandas as pd

from analysis.db import connect, _has_table, _has_column, _optional_table, usable_member_line_coverage
from analysis.files import ensure_output_dir
from analysis.schema import TRAINING_LABELS, TRAINING_MAPPING_FIELDS


def _initial_reports(conn, table: str) -> pd.DataFrame:
    """Select one report from the first persisted test run of each project.

    Mutation additionally requires a solution baseline. Without test-run
    provenance, only a single legacy report per project is safe to select.
    """
    reports = _optional_table(conn, table)
    if reports.empty:
        return reports
    if "project_id" not in reports:
        reports["project_id"] = pd.NA
    runs = _optional_table(conn, "test_runs")
    if not runs.empty and {"project_id", "test_run_id"} <= set(reports):
        first = runs.sort_values("id").drop_duplicates("project_id")
        reports = reports.merge(first[["project_id", "id"]].rename(
            columns={"id": "test_run_id"}), on=["project_id", "test_run_id"], validate="many_to_one")
    else:
        single = reports.groupby("project_id", dropna=False)["id"].transform("size").eq(1)
        reports = reports[single]
    if table == "mutation_testing_reports":
        if "is_baseline" in reports:
            reports = reports[reports["is_baseline"].eq(1)]
        if "scope_kind" in reports:
            reports = reports[reports["scope_kind"].eq("Solution")]
    return reports.sort_values("id").drop_duplicates("project_id")


def _scoped_rows(conn, table: str, report_column: str, reports: pd.DataFrame,
                 report_table: str) -> pd.DataFrame:
    if not _has_table(conn, table):
        return pd.DataFrame()
    if not reports.empty:
        if not _has_column(conn, table, report_column):
            return pd.DataFrame()
        ids = [int(x) for x in reports["id"]]
    elif not _has_table(conn, report_table) and _has_column(conn, table, report_column):
        # A legacy database with one identifiable snapshot can be used, but
        # several snapshots without headers must not be silently pooled.
        ids = [r[0] for r in conn.execute(
            f'SELECT DISTINCT "{report_column}" FROM "{table}" WHERE "{report_column}" IS NOT NULL')]
        if len(ids) != 1:
            return pd.DataFrame()
    elif not _has_table(conn, report_table) and not _has_column(conn, table, report_column):
        return pd.read_sql_query(f'SELECT * FROM "{table}"', conn)
    else:
        return pd.DataFrame()
    marks = ",".join("?" for _ in ids)
    return pd.read_sql_query(f'SELECT * FROM "{table}" WHERE "{report_column}" IN ({marks})',
                             conn, params=ids)


def _attach_initial_measurements(conn, rows: pd.DataFrame) -> pd.DataFrame:
    """Attach unique, explicitly scoped measurements without expanding mappings."""
    coverage_reports = _initial_reports(conn, "coverage_reports")
    mutation_reports = _initial_reports(conn, "mutation_testing_reports")
    out = rows.copy()
    out["measurement_snapshot_policy"] = (
        "initial-test-run-v1" if _has_table(conn, "test_runs") else "legacy-single-snapshot")
    for reports, prefix in ((coverage_reports, "coverage"), (mutation_reports, "mutation")):
        for source, suffix in (("id", "report_id"), ("test_run_id", "test_run_id")):
            out[f"{prefix}_{suffix}"] = (out["project_id"].map(reports.set_index("project_id")[source])
                                         if not reports.empty and source in reports else pd.NA)

    mc = _scoped_rows(conn, "member_coverages", "coverage_report_id", coverage_reports, "coverage_reports")
    modern = _has_table(conn, "coverage_reports")
    usable = usable_member_line_coverage(mc, coverage_reports) if modern else mc
    cov_rows = []
    for member, group in mc.groupby("member_id") if not mc.empty else []:
        valid = usable[usable["member_id"].eq(member)]
        measures = valid[["line_rate", "lines_covered", "lines_valid"]].drop_duplicates()
        known = len(measures) == 1 and measures.notna().all(axis=None)
        record = {"source_member_id": member,
                  "coverage_observation_status": ("Measured" if known else
                                                   "ConflictingObservations" if len(measures) > 1 else "Unavailable"),
                  "source_coverage": pd.NA, "source_covered_lines": pd.NA, "source_total_lines": pd.NA}
        if known:
            record.update(zip(("source_coverage", "source_covered_lines", "source_total_lines"), measures.iloc[0]))
        cov_rows.append(record)
    if cov_rows:
        out = out.merge(pd.DataFrame(cov_rows), on="source_member_id", how="left", validate="many_to_one")
    else:
        for column in ("coverage_observation_status", "source_coverage", "source_covered_lines", "source_total_lines"):
            out[column] = pd.NA
    out["coverage_observation_status"] = out["coverage_observation_status"].fillna("Unavailable")

    gaps = _scoped_rows(conn, "coverage_gaps", "coverage_report_id", coverage_reports, "coverage_reports")
    out["coverage_gap_count"] = pd.NA
    gaps_scoped = not modern or (
        not coverage_reports.empty and _has_column(conn, "coverage_gaps", "coverage_report_id"))
    if _has_table(conn, "coverage_gaps") and gaps_scoped:
        counts = (gaps.groupby("member_id")["line_number"].nunique()
                  if not gaps.empty and "line_number" in gaps else
                  gaps.groupby("member_id").size() if not gaps.empty else pd.Series(dtype="Int64"))
        measured = out["coverage_observation_status"].eq("Measured")
        out.loc[measured, "coverage_gap_count"] = out.loc[measured, "source_member_id"].map(counts).fillna(0)

    # Aggregate in SQL: the pilot can contain hundreds of thousands of mutants.
    mutant_condition = "0"
    params = []
    if not mutation_reports.empty and _has_column(conn, "mutants", "mutation_testing_report_id"):
        params = [int(x) for x in mutation_reports["id"]]
        mutant_condition = "mutation_testing_report_id IN (" + ",".join("?" for _ in params) + ")"
    elif not _has_table(conn, "mutation_testing_reports"):
        if _has_column(conn, "mutants", "mutation_testing_report_id"):
            ids = [r[0] for r in conn.execute("SELECT DISTINCT mutation_testing_report_id FROM mutants")]
            if len(ids) == 1 and ids[0] is not None:
                mutant_condition, params = "mutation_testing_report_id = ?", ids
        else:
            mutant_condition = "1"
    mutation = pd.read_sql_query(f"""
        SELECT member_id AS source_member_id,
               SUM(lower(status) = 'killed') AS killed_mutant_count,
               SUM(lower(status) = 'survived') AS survived_mutant_count
        FROM mutants WHERE member_id IS NOT NULL AND {mutant_condition}
        GROUP BY member_id
    """, conn, params=params)
    mutation["mutation_score"] = 100 * mutation["killed_mutant_count"] / (
        mutation["killed_mutant_count"] + mutation["survived_mutant_count"]).replace(0, float("nan"))
    return out.merge(mutation, on="source_member_id", how="left", validate="many_to_one")


def build_mapping_rows(conn) -> pd.DataFrame:
    """Build one row per source-test mapping enriched with coverage, mutation, and candidate data."""
    sql = """
        WITH smell_summary AS (
            SELECT
                member_id,
                COUNT(*) AS test_smell_count,
                GROUP_CONCAT(DISTINCT smell_id) AS test_smell_ids
            FROM test_smells
            WHERE member_id IS NOT NULL
            GROUP BY member_id
        )
        SELECT
            stm.id AS mapping_id,
            p.owner       AS repo_owner,
            p.repo_name,
            p.last_analyzed_commit AS commit_hash,
            p.id          AS project_id,
            m_src.id      AS source_member_id,
            m_src.name    AS source_method_name,
            m_src.full_string AS source_method_signature,
            f_src.file_path AS source_file_path,
            m_src.start_line_number AS source_line,
            m_src.modifiers AS source_visibility,
            cm2.cyclomatic_complexity AS source_complexity,
            m_tst.id       AS test_member_id,
            m_tst.name     AS test_method_name,
            f_tst.file_path AS test_file_path,
            stm.evidence_kind          AS mapping_evidence_kind,
            stm.is_grounded            AS mapping_is_grounded,
            stm.confidence             AS mapping_confidence,
            stm.access_path_strategy   AS access_path_strategy,
            stm.path_length            AS path_length,
            ts.test_smell_count,
            ts.test_smell_ids,
            ci.risk_score              AS candidate_risk_score,
            ci.metric_driven_score,
            ci.test_state,
            ci.recommended_action,
            CASE WHEN stm.id IS NOT NULL THEN 1 ELSE 0 END AS has_mapped_test,
            stm.is_grounded AS is_grounded_mapping
        FROM source_test_mappings stm
        JOIN members m_src ON m_src.id = stm.source_member_id
        JOIN members m_tst ON m_tst.id = stm.test_member_id
        LEFT JOIN objects o_src ON o_src.id = m_src.object_id
        LEFT JOIN files f_src ON f_src.id = o_src.file_id
        LEFT JOIN objects o_tst ON o_tst.id = m_tst.object_id
        LEFT JOIN files f_tst ON f_tst.id = o_tst.file_id
        LEFT JOIN projects p ON p.id = stm.project_id
        LEFT JOIN candidate_inventory ci ON ci.source_test_mapping_id = stm.id
        LEFT JOIN code_metrics cm2 ON cm2.entity_id = m_src.id AND lower(cm2.entity_type) = 'member'
        LEFT JOIN smell_summary ts ON ts.member_id = m_tst.id
    """
    rows = pd.read_sql_query(sql, conn)
    if rows["mapping_id"].duplicated().any():
        raise ValueError("Static mapping joins are not unique by mapping_id.")
    return _attach_initial_measurements(conn, rows)


def build_candidate_rows(conn) -> pd.DataFrame:
    """Build one row per candidate source method with all feature columns."""
    sql = """
        SELECT
            p.owner AS repo_owner,
            p.repo_name,
            p.last_analyzed_commit AS commit_hash,
            cm.source_member_id,
            cm.source_method_name,
            cm.source_method_signature,
            cm.initial_coverage,
            cm.initial_covered_lines,
            cm.initial_total_lines,
            COALESCE(cm.metric_driven_score, ci.metric_driven_score) AS metric_driven_score,
            COALESCE(ci.risk_score, NULL) AS candidate_risk_score,
            COALESCE(cm.test_state, ci.test_state) AS test_state,
            COALESCE(cm.recommended_action, ci.recommended_action) AS recommended_action,
            ci.complexity_score AS source_complexity,
            ci.access_path_strategy,
            ci.context_evidence_kind,
            ci.has_grounded_test_context,
            er.objective,
            CASE WHEN ci.source_test_mapping_id IS NOT NULL THEN 1 ELSE 0 END AS has_mapped_test
        FROM candidate_methods cm
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
        LEFT JOIN projects p ON p.id = ci.project_id
        LEFT JOIN experiment_runs er ON er.id = cm.experiment_run_id
    """
    return pd.read_sql_query(sql, conn)


def build_pair_rows(conn) -> pd.DataFrame:
    """Build one row per (source method, test method) pair candidate."""
    sql = """
        SELECT
            p.owner AS repo_owner,
            p.repo_name,
            p.last_analyzed_commit AS commit_hash,
            m_src.id   AS source_member_id,
            m_src.name AS source_method_name,
            m_tst.id   AS test_member_id,
            m_tst.name AS test_method_name,
            stm.evidence_kind,
            stm.is_grounded,
            stm.confidence,
            stm.path_length
        FROM source_test_mappings stm
        JOIN members m_src ON m_src.id = stm.source_member_id
        JOIN members m_tst ON m_tst.id = stm.test_member_id
        LEFT JOIN projects p ON p.id = stm.project_id
    """
    return pd.read_sql_query(sql, conn)


def run(db_path: str, output_dir: str, grain: str = "mapping") -> None:
    """Entry point for the ``export-training`` CLI command."""
    out = ensure_output_dir(output_dir)

    with connect(db_path) as conn:
        if grain == "mapping":
            df = build_mapping_rows(conn)
            out_file = out / "training_mappings.csv"
        elif grain == "candidate":
            df = build_candidate_rows(conn)
            out_file = out / "training_candidates.csv"
        elif grain == "pair":
            df = build_pair_rows(conn)
            out_file = out / "training_pairs.csv"
        else:
            raise ValueError(f"Unknown grain: {grain!r}")

    df.to_csv(out_file, index=False)
    print(f"Training export written to {out_file} ({len(df):,} rows, grain={grain})")
