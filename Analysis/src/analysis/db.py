"""SQLite database helpers for reading TestMap output databases.

All functions return pandas DataFrames. Callers are responsible for
opening and closing connections.
"""

from __future__ import annotations

import sqlite3
from pathlib import Path

import pandas as pd


def connect(db_path: str | Path) -> sqlite3.Connection:
    """Open a read-only connection to a TestMap SQLite database."""
    path = Path(db_path)
    if not path.exists():
        raise FileNotFoundError(f"Database not found: {path}")
    conn = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    conn.row_factory = sqlite3.Row
    return conn


def _query(conn: sqlite3.Connection, sql: str, params: tuple = ()) -> pd.DataFrame:
    return pd.read_sql_query(sql, conn, params=params)


def _has_column(conn: sqlite3.Connection, table: str, column: str) -> bool:
    rows = conn.execute(f"PRAGMA table_info({table})").fetchall()
    return any(r[1] == column for r in rows)


# ---------------------------------------------------------------------------
# Raw table reads
# ---------------------------------------------------------------------------

def get_generation_attempts(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return all rows from generation_attempts."""
    return _query(conn, "SELECT * FROM generation_attempts")


def get_tool_attempts(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return all rows from tool_attempts."""
    return _query(conn, "SELECT * FROM tool_attempts")


def get_candidates(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return all candidate_methods rows joined with candidate_inventory."""
    sql = """
        SELECT
            cm.*,
            p.owner AS repo_owner,
            p.repo_name,
            p.web_url AS repo_url,
            p.last_analyzed_commit AS commit_hash
        FROM candidate_methods cm
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
        LEFT JOIN projects p ON p.id = ci.project_id
    """
    return _query(conn, sql)


def get_coverage_reports(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM coverage_reports")


def get_member_coverages(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM member_coverages")


def get_mutation_reports(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM mutation_testing_reports")


def get_mutants(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM mutants")


def get_test_smells(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM test_smells")


def get_source_test_mappings(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM source_test_mappings")


def get_members(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM members")


def get_generated_test_executions(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM generated_test_executions")


def get_tool_attempt_generated_tests(conn: sqlite3.Connection) -> pd.DataFrame:
    return _query(conn, "SELECT * FROM tool_attempt_generated_tests")


# ---------------------------------------------------------------------------
# Joined datasets
# ---------------------------------------------------------------------------

def build_llm_attempt_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return one row per LLM generation attempt enriched with candidate and repo metadata."""
    sql = """
        SELECT
            ga.*,
            cm.source_member_id,
            p.directory_path AS project_path,
            ga.objective,
            p.owner AS repo_owner,
            p.repo_name,
            p.web_url AS repo_url,
            p.last_analyzed_commit AS commit_hash
        FROM generation_attempts ga
        LEFT JOIN candidate_methods cm ON cm.id = ga.candidate_method_id
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
        LEFT JOIN projects p ON p.id = ci.project_id
    """
    return _query(conn, sql)


def build_tool_attempt_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return one row per agentic tool attempt enriched with candidate and repo metadata."""
    sql = """
        SELECT
            ta.*,
            cm.source_member_id,
            p.directory_path AS project_path,
            w.objective,
            p.owner AS repo_owner,
            p.repo_name,
            p.web_url AS repo_url,
            p.last_analyzed_commit AS commit_hash
        FROM tool_attempts ta
        LEFT JOIN candidate_methods cm ON cm.id = ta.candidate_method_id
        LEFT JOIN experiment_matrix_work_items w ON w.id = ta.matrix_work_item_id
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
        LEFT JOIN projects p ON p.id = ci.project_id
    """
    return _query(conn, sql)


def build_coverage_delta_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return coverage before/after per attempt where available."""
    sql = """
        SELECT
            ga.id AS attempt_id,
            'llm' AS lane,
            gte.final_coverage - gte.coverage_delta AS coverage_before,
            gte.final_coverage AS coverage_after,
            gte.coverage_delta
        FROM generation_attempts ga
        LEFT JOIN generated_test_executions gte ON gte.generation_attempt_id = ga.id
    """
    return _query(conn, sql)


def build_mutation_delta_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return mutation score before/after per attempt where available."""
    sql = """
        SELECT
            ga.id AS attempt_id,
            'llm' AS lane,
            gte.baseline_mutation_score AS mutation_score_before,
            gte.mutation_score_after,
            gte.mutation_score_delta
        FROM generation_attempts ga
        LEFT JOIN generated_test_executions gte ON gte.generation_attempt_id = ga.id
    """
    return _query(conn, sql)


def get_members_with_source(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return member id, name, containing class full_string, and file path."""
    sql = """
        SELECT
            m.id,
            m.name,
            o.full_string AS class_source,
            f.file_path
        FROM members m
        JOIN objects o ON o.id = m.object_id
        JOIN files f   ON f.id = o.file_id
    """
    return _query(conn, sql)


def get_existing_tests_by_source_member(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return source_member_id → existing test class full_string.

    Joins candidate_inventory.existing_test_member_id → members → objects to
    get the full containing test class used as context for the generation attempt.
    """
    sql = """
        SELECT
            ci.source_member_id,
            o.full_string AS existing_test_class_source,
            f.file_path   AS existing_test_file_path
        FROM candidate_inventory ci
        JOIN members m ON m.id = ci.existing_test_member_id
        JOIN objects o ON o.id = m.object_id
        JOIN files f   ON f.id = o.file_id
        WHERE ci.existing_test_member_id IS NOT NULL
    """
    return _query(conn, sql)


def get_llm_final_prompts(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return the FinalTest step prompt for each generation attempt.

    The FinalTest prompt contains the full chained conversation history and is
    the most complete record of what was sent to the LLM.  Falls back to the
    highest-order step with a non-null prompt when FinalTest is absent.
    """
    sql = """
        SELECT
            gs.generation_attempt_id AS attempt_id,
            gs.prompt
        FROM generation_steps gs
        INNER JOIN (
            SELECT
                generation_attempt_id,
                MAX(step_order) AS max_order
            FROM generation_steps
            WHERE prompt IS NOT NULL AND length(prompt) > 0
            GROUP BY generation_attempt_id
        ) last ON last.generation_attempt_id = gs.generation_attempt_id
               AND last.max_order = gs.step_order
        WHERE gs.prompt IS NOT NULL AND length(gs.prompt) > 0
    """
    return _query(conn, sql)


def get_llm_attempt_artifacts(conn: sqlite3.Connection) -> pd.DataFrame:
    """Fetch artifact content for LLM failure case enrichment.

    Joins generation_attempts → generated_test_executions → test_runs.
    The test_run FK on generated_test_executions is new; this function detects
    its presence at runtime and falls back to NULL log_path when absent.

    Returns columns: attempt_id, modified_file_contents, modified_file_path,
                     generated_test_code, log_path
    """
    if _has_column(conn, "generated_test_executions", "test_run"):
        sql = """
            SELECT
                ga.id                  AS attempt_id,
                ga.patch_json,
                ga.modified_file_contents,
                ga.modified_file_path,
                gte.generated_test_code,
                tr.log_path
            FROM generation_attempts ga
            LEFT JOIN generated_test_executions gte ON gte.generation_attempt_id = ga.id
            LEFT JOIN test_runs tr ON tr.id = gte.test_run
        """
    else:
        sql = """
            SELECT
                ga.id                  AS attempt_id,
                ga.patch_json,
                ga.modified_file_contents,
                ga.modified_file_path,
                gte.generated_test_code,
                NULL AS log_path
            FROM generation_attempts ga
            LEFT JOIN generated_test_executions gte ON gte.generation_attempt_id = ga.id
        """
    return _query(conn, sql)
