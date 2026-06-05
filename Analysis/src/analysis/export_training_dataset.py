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
        SELECT
            ci.repo_owner,
            ci.repo_name,
            ci.commit_hash,
            p.id          AS project_id,
            m_src.id      AS source_member_id,
            m_src.name    AS source_method_name,
            m_src.signature AS source_method_signature,
            m_src.file_path AS source_file_path,
            m_src.line      AS source_line,
            m_src.visibility AS source_visibility,
            cm2.cyclomatic_complexity AS source_complexity,
            mc.line_coverage           AS source_coverage,
            mc.covered_lines           AS source_covered_lines,
            mc.total_lines             AS source_total_lines,
            cg.gap_count               AS coverage_gap_count,
            m_tst.id       AS test_member_id,
            m_tst.name     AS test_method_name,
            m_tst.file_path AS test_file_path,
            stm.evidence_kind          AS mapping_evidence_kind,
            stm.is_grounded            AS mapping_is_grounded,
            stm.confidence             AS mapping_confidence,
            stm.access_path_strategy   AS access_path_strategy,
            stm.path_length            AS path_length,
            ts.smell_count             AS test_smell_count,
            ts.smell_ids               AS test_smell_ids,
            mtr.mutation_score,
            mtr.survived_mutants       AS survived_mutant_count,
            mtr.killed_mutants         AS killed_mutant_count,
            cand.risk_score            AS candidate_risk_score,
            cand.metric_driven_score,
            cand.test_state,
            cand.recommended_action
        FROM source_test_mappings stm
        JOIN members m_src ON m_src.id = stm.source_member_id
        JOIN members m_tst ON m_tst.id = stm.test_member_id
        LEFT JOIN projects p ON p.id = m_src.project_id
        LEFT JOIN candidate_inventory ci ON ci.id = stm.candidate_inventory_id
        LEFT JOIN candidate_methods cand ON cand.source_member_id = m_src.id
        LEFT JOIN code_metrics cm2 ON cm2.member_id = m_src.id
        LEFT JOIN member_coverages mc ON mc.member_id = m_src.id
        LEFT JOIN coverage_gaps cg ON cg.member_id = m_src.id
        LEFT JOIN mutation_testing_reports mtr ON mtr.member_id = m_src.id
        LEFT JOIN test_smells ts ON ts.member_id = m_tst.id
    """
    try:
        import pandas as pd
        return pd.read_sql_query(sql, conn)
    except Exception as exc:
        print(f"[warn] Could not build mapping rows: {exc}")
        return pd.DataFrame()


def build_candidate_rows(conn) -> pd.DataFrame:
    """Build one row per candidate source method with all feature columns."""
    sql = """
        SELECT
            ci.repo_owner,
            ci.repo_name,
            ci.commit_hash,
            cm.source_member_id,
            cm.project_path,
            cm.risk_score        AS candidate_risk_score,
            cm.metric_driven_score,
            cm.test_state,
            cm.recommended_action,
            cm.baseline_coverage,
            cm.source_complexity,
            cm.source_visibility,
            cm.access_strategy,
            cm.mapping_count,
            cm.setup_binding_count,
            cm.objective
        FROM candidate_methods cm
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
    """
    try:
        import pandas as pd
        return pd.read_sql_query(sql, conn)
    except Exception as exc:
        print(f"[warn] Could not build candidate rows: {exc}")
        return pd.DataFrame()


def build_pair_rows(conn) -> pd.DataFrame:
    """Build one row per (source method, test method) pair candidate."""
    sql = """
        SELECT
            ci.repo_owner,
            ci.repo_name,
            ci.commit_hash,
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
        LEFT JOIN candidate_inventory ci ON ci.id = stm.candidate_inventory_id
    """
    try:
        import pandas as pd
        return pd.read_sql_query(sql, conn)
    except Exception as exc:
        print(f"[warn] Could not build pair rows: {exc}")
        return pd.DataFrame()


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
