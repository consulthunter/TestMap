"""Consolidate per-repository ``analysis.db`` files into MSR validation frames.

TestMap writes one SQLite database per repository revision. This module reads
those databases and produces one long-format frame per mined construct, keyed by
``repo_key`` so that per-database autoincrement ids never collide.

Frames produced:

``entities``        one row per member and per object (the attribution spine)
``code_metrics``    one row per persisted metric row
``test_smells``     one row per detected smell
``coverage``        one row per member/object coverage row
``mutants``         one row per (member, mutation report) mutant aggregate
``mappings``        one row per source-to-test mapping

Source text (``members.full_string`` / ``objects.full_string``) is deliberately
excluded from the consolidated frames: it is large, and sampling re-reads the
originating database when it materializes rater artifacts.
"""

from __future__ import annotations

import json
import sqlite3
from pathlib import Path
from typing import Iterator, Optional

import pandas as pd

from analysis.files import glob_paths


CONSTRUCTS = ("code_metrics", "test_smells", "coverage", "mutants", "mappings")

MUTANT_STATUSES = ("Killed", "Survived", "NoCoverage", "CompileError", "Ignored", "Timeout")


# ---------------------------------------------------------------------------
# Database discovery and identity
# ---------------------------------------------------------------------------

def find_databases(patterns: list[str] | tuple[str, ...]) -> list[Path]:
    """Resolve ``analysis.db`` paths from directories, globs, or explicit files.

    A directory is searched recursively, which avoids relying on the caller's
    shell to leave a glob pattern unexpanded.
    """
    found: list[Path] = []
    for pattern in patterns:
        candidate = Path(pattern)
        if candidate.is_dir():
            found.extend(sorted(candidate.rglob("analysis.db")))
        elif candidate.is_file():
            found.append(candidate)
        else:
            found.extend(p for p in glob_paths([pattern]) if p.is_file())

    seen: set[Path] = set()
    unique: list[Path] = []
    for path in found:
        resolved = path.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique.append(path)
    return unique


def connect_ro(db_path: str | Path) -> sqlite3.Connection:
    """Open a read-only connection."""
    path = Path(db_path)
    if not path.exists():
        raise FileNotFoundError(f"Database not found: {path}")
    return sqlite3.connect(f"file:{path}?mode=ro", uri=True)


def _has_table(conn: sqlite3.Connection, table: str) -> bool:
    row = conn.execute(
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?", (table,)
    ).fetchone()
    return row is not None


def _query(conn: sqlite3.Connection, sql: str) -> pd.DataFrame:
    return pd.read_sql_query(sql, conn)


def read_identity(conn: sqlite3.Connection, db_path: Path) -> dict:
    """Return repository identity from the ``projects`` table.

    The identity comes from the database rather than the directory layout.
    Output folders nest as ``<owner>/<repo>/<sha>/analysis.db``, so the parent
    directory name is a commit SHA and cannot stand in for a repository key.
    """
    if not _has_table(conn, "projects"):
        return {
            "repo_key": str(db_path.parent),
            "owner": "",
            "repo_name": "",
            "commit_hash": "",
            "project_id": None,
            "db_path": str(db_path),
        }

    df = _query(
        conn,
        "SELECT id, owner, repo_name, last_analyzed_commit FROM projects ORDER BY id LIMIT 1",
    )
    if df.empty:
        return {
            "repo_key": str(db_path.parent),
            "owner": "",
            "repo_name": "",
            "commit_hash": "",
            "project_id": None,
            "db_path": str(db_path),
        }

    row = df.iloc[0]
    owner = str(row["owner"] or "")
    repo = str(row["repo_name"] or "")
    commit = str(row["last_analyzed_commit"] or "")
    return {
        "repo_key": f"{owner}/{repo}@{commit[:12]}",
        "owner": owner,
        "repo_name": repo,
        "commit_hash": commit,
        "project_id": int(row["id"]),
        "db_path": str(db_path),
    }


# ---------------------------------------------------------------------------
# Per-construct readers
# ---------------------------------------------------------------------------

def load_files(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return the file table, referenced by ``file_id`` from the entity spine."""
    if not _has_table(conn, "files"):
        return pd.DataFrame()
    return _query(conn, """
        SELECT id AS file_id, csharp_project_id, file_path, content_hash
        FROM files
    """)


def load_entities(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return the attribution spine: one row per member and per object.

    ``start_line``/``end_line`` are stored zero-based by TestMap. Callers that
    compare against a one-based line number from a report must adjust.

    File paths and content hashes are left out and reached through ``file_id``
    (see :func:`load_files`); at corpus scale they dominate the frame size, and
    sampling re-reads the originating database for source text anyway.
    """
    members = _query(conn, """
        SELECT 'member' AS entity_kind,
               m.id            AS entity_id,
               m.name          AS name,
               m.kind          AS kind,
               m.is_test_member AS is_test,
               m.is_generated  AS is_generated,
               m.start_line_number AS start_line,
               m.end_line_number   AS end_line,
               LENGTH(m.full_string) AS source_chars,
               o.id            AS parent_object_id,
               o.is_test_object AS parent_is_test,
               o.file_id       AS file_id
        FROM members m
        JOIN objects o ON o.id = m.object_id
    """)

    objects = _query(conn, """
        SELECT 'object' AS entity_kind,
               o.id            AS entity_id,
               o.name          AS name,
               o.kind          AS kind,
               o.is_test_object AS is_test,
               0               AS is_generated,
               o.start_line_number AS start_line,
               o.end_line_number   AS end_line,
               LENGTH(o.full_string) AS source_chars,
               NULL            AS parent_object_id,
               o.is_test_object AS parent_is_test,
               o.file_id       AS file_id
        FROM objects o
    """)

    return pd.concat([members, objects], ignore_index=True)


def load_code_metrics(conn: sqlite3.Connection) -> pd.DataFrame:
    if not _has_table(conn, "code_metrics"):
        return pd.DataFrame()
    return _query(conn, """
        SELECT id AS observation_id,
               entity_type AS entity_kind,
               entity_id,
               maintainability_index,
               cyclomatic_complexity,
               class_coupling,
               depth_of_inheritance,
               source_lines_of_code,
               executable_lines_of_code
        FROM code_metrics
    """)


def load_test_smells(conn: sqlite3.Connection) -> pd.DataFrame:
    if not _has_table(conn, "test_smells"):
        return pd.DataFrame()
    return _query(conn, """
        SELECT id AS observation_id,
               member_id,
               object_id,
               smell_id,
               smell_name,
               file_path AS claimed_file_path,
               line      AS claimed_line,
               column    AS claimed_column,
               containing_type_name,
               test_method_name,
               LENGTH(message) AS message_chars
        FROM test_smells
    """)


def load_coverage(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return member and object coverage rows in one frame."""
    frames: list[pd.DataFrame] = []

    if _has_table(conn, "member_coverages"):
        frames.append(_query(conn, """
            SELECT mc.id AS observation_id,
                   'member' AS entity_kind,
                   mc.member_id AS entity_id,
                   mc.coverage_report_id,
                   mc.line_rate, mc.branch_rate,
                   mc.lines_covered, mc.lines_valid,
                   mc.branches_covered, mc.branches_valid,
                   mc.complexity
            FROM member_coverages mc
        """))

    if _has_table(conn, "object_coverages"):
        frames.append(_query(conn, """
            SELECT oc.id AS observation_id,
                   'object' AS entity_kind,
                   oc.object_id AS entity_id,
                   oc.coverage_report_id,
                   oc.line_rate, oc.branch_rate,
                   oc.lines_covered, oc.lines_valid,
                   oc.branches_covered, oc.branches_valid,
                   oc.complexity
            FROM object_coverages oc
        """))

    if not frames:
        return pd.DataFrame()
    return pd.concat(frames, ignore_index=True)


def load_mutants(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return one row per (member, report) with mutant status counts.

    Mutants are aggregated rather than exported per row: a single repository can
    hold tens of thousands of them, and the exploratory pass needs per-entity
    distributions. Sampling for the manual attribution sheet re-reads individual
    mutants from the originating database.

    ``unattributed_mutants`` is reported separately by the caller, since rows
    with a NULL ``member_id`` cannot be grouped onto an entity.
    """
    if not _has_table(conn, "mutants"):
        return pd.DataFrame()

    status_cols = ",\n".join(
        f"SUM(CASE WHEN status = '{s}' THEN 1 ELSE 0 END) AS {s.lower()}"
        for s in MUTANT_STATUSES
    )
    return _query(conn, f"""
        SELECT member_id AS entity_id,
               'member'  AS entity_kind,
               mutation_testing_report_id,
               COUNT(*)  AS mutants_total,
               {status_cols},
               SUM(CASE WHEN is_static = 1 THEN 1 ELSE 0 END) AS static_mutants,
               COUNT(DISTINCT mutator_name) AS distinct_mutators
        FROM mutants
        WHERE member_id IS NOT NULL
        GROUP BY member_id, mutation_testing_report_id
    """)


def load_raw_mutant_locations(conn: sqlite3.Connection) -> pd.DataFrame:
    """Return per-mutant id, member, and parsed location lines.

    Used by the containment check. ``location`` is stored as a JSON object,
    e.g. ``{"StartLineNumber":14,"EndLineNumber":27,...}``.
    """
    if not _has_table(conn, "mutants"):
        return pd.DataFrame()

    df = _query(conn, "SELECT id, member_id, status, location FROM mutants")
    if df.empty:
        return df

    def _line(raw, key):
        try:
            return json.loads(raw).get(key)
        except (TypeError, ValueError, AttributeError):
            return None

    df["claimed_start_line"] = df["location"].map(lambda r: _line(r, "StartLineNumber"))
    df["claimed_end_line"] = df["location"].map(lambda r: _line(r, "EndLineNumber"))
    return df.drop(columns=["location"])


def load_mappings(conn: sqlite3.Connection) -> pd.DataFrame:
    if not _has_table(conn, "source_test_mappings"):
        return pd.DataFrame()

    mappings = _query(conn, """
        SELECT id AS observation_id,
               source_member_id,
               test_member_id,
               evidence_kind,
               is_grounded,
               access_path_strategy,
               path_length,
               confidence,
               resolver_version
        FROM source_test_mappings
    """)
    if mappings.empty or not _has_table(conn, "source_test_mapping_trace_steps"):
        mappings["trace_steps"] = 0 if not mappings.empty else None
        return mappings

    steps = _query(conn, """
        SELECT source_test_mapping_id AS observation_id,
               COUNT(*) AS trace_steps,
               COUNT(DISTINCT relationship_kind) AS distinct_relationship_kinds,
               COUNT(DISTINCT edge_source) AS distinct_edge_sources
        FROM source_test_mapping_trace_steps
        GROUP BY source_test_mapping_id
    """)
    merged = mappings.merge(steps, on="observation_id", how="left")
    merged["trace_steps"] = merged["trace_steps"].fillna(0).astype(int)
    return merged


def load_report_headers(conn: sqlite3.Connection) -> dict:
    """Return report-level counts used for availability reporting."""
    out = {"coverage_reports": 0, "mutation_reports": 0, "test_runs": 0, "files": 0}
    for table, key in (
        ("coverage_reports", "coverage_reports"),
        ("mutation_testing_reports", "mutation_reports"),
        ("test_runs", "test_runs"),
        ("files", "files"),
    ):
        if _has_table(conn, table):
            out[key] = int(conn.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0])
    return out


# ---------------------------------------------------------------------------
# Whole-repository read
# ---------------------------------------------------------------------------

def read_repository(db_path: str | Path) -> dict:
    """Read one database and return identity plus every construct frame."""
    path = Path(db_path)
    conn = connect_ro(path)
    try:
        identity = read_identity(conn, path)
        bundle = {
            "identity": identity,
            "files": load_files(conn),
            "entities": load_entities(conn),
            "code_metrics": load_code_metrics(conn),
            "test_smells": load_test_smells(conn),
            "coverage": load_coverage(conn),
            "mutants": load_mutants(conn),
            "mutant_locations": load_raw_mutant_locations(conn),
            "mappings": load_mappings(conn),
            "reports": load_report_headers(conn),
        }
    finally:
        conn.close()

    repo_key = bundle["identity"]["repo_key"]
    for name, frame in bundle.items():
        if isinstance(frame, pd.DataFrame) and not frame.empty:
            frame.insert(0, "repo_key", repo_key)
    return bundle


def iter_repositories(
    patterns: list[str] | tuple[str, ...],
    limit: Optional[int] = None,
    progress_every: int = 25,
) -> Iterator[dict]:
    """Yield one bundle per database matching *patterns*."""
    paths = find_databases(patterns)
    if limit:
        paths = paths[:limit]
    total = len(paths)
    print(f"Reading {total:,} database(s)")

    for index, path in enumerate(paths, start=1):
        try:
            yield read_repository(path)
        except Exception as exc:  # a corrupt or partial database must not stop the sweep
            print(f"[warn] Could not read {path}: {exc}")
            continue
        if progress_every and index % progress_every == 0:
            print(f"  {index:,}/{total:,}")
