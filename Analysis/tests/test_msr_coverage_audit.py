import sqlite3

import pandas as pd

from analysis.msr_coverage_audit import audit, read_coverage_state


def test_read_coverage_state_preserves_legacy_missingness(tmp_path):
    path = tmp_path / "legacy.db"
    conn = sqlite3.connect(path)
    conn.executescript("""
        CREATE TABLE coverage_reports (
            id INTEGER PRIMARY KEY, line_rate REAL, branch_rate REAL,
            lines_valid INTEGER, version TEXT
        );
        CREATE TABLE object_coverages (id INTEGER PRIMARY KEY);
        CREATE TABLE member_coverages (id INTEGER PRIMARY KEY);
        INSERT INTO coverage_reports VALUES (1, 0.5, 0.2, 10, 'legacy');
    """)
    conn.close()

    state = read_coverage_state(path)

    assert state["corrected_schema"] is False
    assert state["coverage_status"] == ""
    assert state["raw_object_count"] is None
    assert state["reconciliation_valid"] is None


def test_read_coverage_state_validates_corrected_reconciliation_and_counters(tmp_path):
    path = tmp_path / "corrected.db"
    conn = sqlite3.connect(path)
    conn.executescript("""
        CREATE TABLE coverage_reports (
            id INTEGER PRIMARY KEY, line_rate REAL, branch_rate REAL, lines_valid INTEGER,
            version TEXT, measurement_policy_version TEXT, collection_status TEXT,
            has_usable_coverage INTEGER, raw_object_count INTEGER, mapped_object_count INTEGER,
            raw_member_count INTEGER, mapped_member_count INTEGER
        );
        CREATE TABLE object_coverages (
            id INTEGER PRIMARY KEY, coverage_report_id INTEGER, object_id INTEGER,
            attribution_status TEXT, attribution_reason TEXT,
            line_counts_available INTEGER, lines_covered INTEGER, lines_valid INTEGER,
            branch_counts_available INTEGER, branches_covered INTEGER, branches_valid INTEGER
        );
        CREATE TABLE member_coverages (
            id INTEGER PRIMARY KEY, coverage_report_id INTEGER, member_id INTEGER,
            attribution_status TEXT, attribution_reason TEXT,
            line_counts_available INTEGER, lines_covered INTEGER, lines_valid INTEGER,
            branch_counts_available INTEGER, branches_covered INTEGER, branches_valid INTEGER
        );
        INSERT INTO coverage_reports VALUES
            (1, 0.5, 0.5, 2, 'corrected', 'coverage-integrity-v1', 'PartiallyMapped', 1, 2, 1, 2, 1);
        INSERT INTO object_coverages VALUES
            (1, 1, 10, 'Mapped', '', 1, 1, 2, 1, 1, 2),
            (2, 1, NULL, 'OutOfProject', 'external file', 1, 0, 1, 1, 0, 0);
        INSERT INTO member_coverages VALUES
            (1, 1, 20, 'Mapped', '', 1, 1, 2, 1, 1, 2),
            (2, 1, NULL, 'ParentUnmatched', 'external parent', 1, 0, 1, 1, 0, 0);
    """)
    conn.close()

    state = read_coverage_state(path)

    assert state["corrected_schema"] is True
    assert state["coverage_policy"] == "coverage-integrity-v1"
    assert state["has_usable_coverage"] is True
    assert state["reconciliation_valid"] is True
    assert state["pending_attribution_rows"] == 0
    assert state["terminal_rows_missing_reason"] == 0
    assert state["impossible_counter_rows"] == 0


def test_audit_does_not_flag_legacy_rows_with_actual_coverage(tmp_path):
    commit = "a" * 40
    db_dir = tmp_path / "data" / "Output" / "owner" / "repo" / commit
    db_dir.mkdir(parents=True)
    conn = sqlite3.connect(db_dir / "analysis.db")
    conn.executescript("""
        CREATE TABLE coverage_reports (
            id INTEGER PRIMARY KEY, line_rate REAL, branch_rate REAL,
            lines_valid INTEGER, version TEXT
        );
        CREATE TABLE object_coverages (id INTEGER PRIMARY KEY);
        CREATE TABLE member_coverages (id INTEGER PRIMARY KEY);
        INSERT INTO coverage_reports VALUES (1, 0.5, 0.2, 10, 'legacy');
        INSERT INTO member_coverages VALUES (1);
    """)
    conn.close()
    logs = tmp_path / "logs"
    logs.mkdir()
    population = pd.DataFrame([{
        "selected": True,
        "repository": "owner/repo",
        "resolved_commit": commit,
        "HasCoverage": True,
    }])

    result = audit(population, tmp_path / "data", logs).iloc[0]

    assert result["coverage_data_present"] is True or result["coverage_data_present"] == 1
    assert result["false_has_coverage_claim"] is False or result["false_has_coverage_claim"] == 0
    assert result["verdict"] == "coverage_present"
