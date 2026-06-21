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

from analysis.db import connect
from analysis.files import ensure_output_dir
from analysis.schema import TRAINING_LABELS, TRAINING_MAPPING_FIELDS


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
        ),
        gap_summary AS (
            SELECT member_id, COUNT(*) AS coverage_gap_count
            FROM coverage_gaps
            GROUP BY member_id
        ),
        mutation_summary AS (
            SELECT
                member_id,
                SUM(CASE WHEN lower(status) = 'survived' THEN 1 ELSE 0 END) AS survived_mutant_count,
                SUM(CASE WHEN lower(status) = 'killed' THEN 1 ELSE 0 END) AS killed_mutant_count,
                CASE
                    WHEN SUM(CASE WHEN lower(status) IN ('survived', 'killed') THEN 1 ELSE 0 END) = 0 THEN NULL
                    ELSE 100.0 * SUM(CASE WHEN lower(status) = 'killed' THEN 1 ELSE 0 END)
                         / SUM(CASE WHEN lower(status) IN ('survived', 'killed') THEN 1 ELSE 0 END)
                END AS mutation_score
            FROM mutants
            WHERE member_id IS NOT NULL
            GROUP BY member_id
        ),
        latest_coverage AS (
            SELECT mc.*
            FROM member_coverages mc
            INNER JOIN (
                SELECT member_id, MAX(coverage_report_id) AS coverage_report_id
                FROM member_coverages
                GROUP BY member_id
            ) latest ON latest.member_id = mc.member_id
                    AND latest.coverage_report_id = mc.coverage_report_id
        )
        SELECT
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
            mc.line_rate               AS source_coverage,
            mc.lines_covered           AS source_covered_lines,
            mc.lines_valid             AS source_total_lines,
            cg.coverage_gap_count,
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
            mtr.mutation_score,
            mtr.survived_mutant_count,
            mtr.killed_mutant_count,
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
        LEFT JOIN latest_coverage mc ON mc.member_id = m_src.id
        LEFT JOIN gap_summary cg ON cg.member_id = m_src.id
        LEFT JOIN mutation_summary mtr ON mtr.member_id = m_src.id
        LEFT JOIN smell_summary ts ON ts.member_id = m_tst.id
    """
    return pd.read_sql_query(sql, conn)


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
