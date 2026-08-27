import sqlite3

import pandas as pd

from analysis.msr_frames import load_coverage


def test_load_coverage_exports_corrected_raw_and_mapped_observation_grain(tmp_path):
    path = tmp_path / "corrected.db"
    conn = sqlite3.connect(path)
    conn.executescript("""
        CREATE TABLE coverage_reports (
            id INTEGER PRIMARY KEY, run_id TEXT, test_run_id INTEGER,
            collection_status TEXT, collection_reason TEXT, successful_collector TEXT,
            has_usable_coverage INTEGER, measurement_policy_version TEXT,
            raw_object_count INTEGER, mapped_object_count INTEGER,
            raw_member_count INTEGER, mapped_member_count INTEGER
        );
        CREATE TABLE object_coverages (
            id INTEGER PRIMARY KEY, object_id INTEGER, coverage_report_id INTEGER,
            source_ordinal INTEGER, package_name TEXT, name TEXT, filename TEXT,
            attribution_status TEXT, attribution_reason TEXT,
            line_counts_available INTEGER, branch_counts_available INTEGER,
            line_rate REAL, branch_rate REAL, lines_covered INTEGER, lines_valid INTEGER,
            branches_covered INTEGER, branches_valid INTEGER, complexity REAL
        );
        CREATE TABLE member_coverages (
            id INTEGER PRIMARY KEY, member_id INTEGER, coverage_report_id INTEGER,
            object_coverage_id INTEGER, source_ordinal INTEGER, name TEXT, signature TEXT,
            attribution_status TEXT, attribution_reason TEXT,
            line_counts_available INTEGER, branch_counts_available INTEGER,
            line_rate REAL, branch_rate REAL, lines_covered INTEGER, lines_valid INTEGER,
            branches_covered INTEGER, branches_valid INTEGER, complexity REAL
        );
        INSERT INTO coverage_reports VALUES
            (1, 'run-1', 5, 'PartiallyMapped', '', 'XPlat Code Coverage', 1,
             'coverage-integrity-v1', 1, 1, 2, 1);
        INSERT INTO object_coverages VALUES
            (10, 100, 1, 0, 'Fixture', 'Fixture.Widget', 'src/Widget.cs', 'Mapped', '',
             1, 1, 0.5, 0.5, 1, 2, 1, 2, 1);
        INSERT INTO member_coverages VALUES
            (20, 200, 1, 10, 0, 'Run', 'System.Void()', 'Mapped', '',
             1, 1, 1, 1, 1, 1, 0, 0, 1),
            (21, NULL, 1, 10, 1, 'Missing', 'System.Void()', 'Unmatched', 'not found',
             1, 1, 0, 0, 0, 1, 0, 0, 1);
    """)

    frame = load_coverage(conn)
    conn.close()

    assert len(frame) == 3
    unmatched = frame[frame["attribution_status"] == "Unmatched"].iloc[0]
    assert pd.isna(unmatched["entity_id"])
    assert unmatched["raw_name"] == "Missing"
    assert unmatched["coverage_run_id"] == "run-1"
    assert unmatched["measurement_policy_version"] == "coverage-integrity-v1"
    assert unmatched["line_counts_available"] == 1


def test_load_coverage_marks_legacy_counters_unavailable(tmp_path):
    path = tmp_path / "legacy.db"
    conn = sqlite3.connect(path)
    conn.executescript("""
        CREATE TABLE member_coverages (
            id INTEGER PRIMARY KEY, member_id INTEGER, coverage_report_id INTEGER,
            line_rate REAL, branch_rate REAL, lines_covered INTEGER, lines_valid INTEGER,
            branches_covered INTEGER, branches_valid INTEGER, complexity REAL
        );
        CREATE TABLE object_coverages (
            id INTEGER PRIMARY KEY, object_id INTEGER, coverage_report_id INTEGER,
            line_rate REAL, branch_rate REAL, lines_covered INTEGER, lines_valid INTEGER,
            branches_covered INTEGER, branches_valid INTEGER, complexity REAL
        );
        INSERT INTO member_coverages VALUES (1, 7, 1, 0.5, 0, 0, 0, 0, 0, 1);
    """)

    frame = load_coverage(conn)
    conn.close()

    assert len(frame) == 1
    assert frame.iloc[0]["collection_status"] == "LegacyNotMeasured"
    assert pd.isna(frame.iloc[0]["line_counts_available"])
    assert frame.iloc[0]["measurement_policy_version"] == ""
