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
            ci.repo_owner,
            ci.repo_name,
            ci.repo_url,
            ci.commit_hash
        FROM candidate_methods cm
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
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
            cm.project_path,
            cm.objective,
            ci.repo_owner,
            ci.repo_name,
            ci.repo_url,
            ci.commit_hash
        FROM generation_attempts ga
        LEFT JOIN candidate_methods cm ON cm.id = ga.candidate_method_id
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
    """
    return _query(conn, sql)


def build_tool_attempt_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return one row per agentic tool attempt enriched with candidate and repo metadata."""
    sql = """
        SELECT
            ta.*,
            cm.source_member_id,
            cm.project_path,
            cm.objective,
            ci.repo_owner,
            ci.repo_name,
            ci.repo_url,
            ci.commit_hash
        FROM tool_attempts ta
        LEFT JOIN candidate_methods cm ON cm.id = ta.candidate_method_id
        LEFT JOIN candidate_inventory ci ON ci.id = cm.candidate_inventory_id
    """
    return _query(conn, sql)


def build_coverage_delta_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return coverage before/after per attempt where available."""
    sql = """
        SELECT
            ga.id AS attempt_id,
            'llm' AS lane,
            pre.line_coverage  AS coverage_before,
            post.line_coverage AS coverage_after,
            post.line_coverage - pre.line_coverage AS coverage_delta
        FROM generation_attempts ga
        LEFT JOIN coverage_reports pre  ON pre.id = ga.baseline_coverage_report_id
        LEFT JOIN coverage_reports post ON post.id = ga.post_attempt_coverage_report_id
    """
    return _query(conn, sql)


def build_mutation_delta_rows(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return mutation score before/after per attempt where available."""
    sql = """
        SELECT
            ga.id AS attempt_id,
            'llm' AS lane,
            pre.mutation_score  AS mutation_score_before,
            post.mutation_score AS mutation_score_after,
            post.mutation_score - pre.mutation_score AS mutation_score_delta
        FROM generation_attempts ga
        LEFT JOIN mutation_testing_reports pre  ON pre.id = ga.baseline_mutation_report_id
        LEFT JOIN mutation_testing_reports post ON post.id = ga.post_attempt_mutation_report_id
    """
    return _query(conn, sql)
