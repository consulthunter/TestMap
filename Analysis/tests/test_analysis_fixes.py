"""Focused tests for the analysis fix-plan semantics."""

from __future__ import annotations

import sqlite3

import pandas as pd
import pytest

from analysis.audit_evaluation_data import (
    audit_agentic_identity_and_artifacts,
    audit_agentic_no_post_attempt,
    audit_generated_test_links,
    audit_outcome_classification,
)
from analysis.export_failure_cases import build_failure_cases
from analysis.export_training_dataset import build_mapping_rows
from analysis.normalize import normalize_attempts
from analysis.statistics import mcnemar_test
from analysis.summaries import build_overview


def _raw_rows() -> pd.DataFrame:
    return pd.DataFrame([
        {
            "experiment_run_id": "1",
            "producer_lane": "testmap",
            "repo_owner": "owner",
            "repo_name": "repo",
            "commit_hash": "abc",
            "source_member_id": "1",
            "generation_attempt_id": "10",
            "failure_kind": "None",
            "coverage_delta": "0.2",
            "mutation_score_delta": "0",
            "total_duration_seconds": "5",
        },
        {
            "experiment_run_id": "1",
            "producer_lane": "agent-tool",
            "repo_owner": "owner",
            "repo_name": "repo",
            "commit_hash": "abc",
            "source_member_id": "2",
            "tool_attempt_id": "20",
            "tool_id": "codex",
            "tool_run_status": "Completed",
            "tool_validation_outcome": "Passed",
            "tool_changed_files_count": "1",
            "tool_post_attempt_test_run_id": "",
            "generated_test_method_name": "LowImpact",
            "coverage_delta": "0",
            "mutation_score_delta": "0",
            "total_duration_seconds": "30",
        },
        {
            "experiment_run_id": "1",
            "producer_lane": "agent-tool",
            "repo_owner": "owner",
            "repo_name": "repo",
            "commit_hash": "abc",
            "source_member_id": "3",
            "tool_attempt_id": "21",
            "tool_id": "codex",
            "tool_run_status": "TimedOut",
            "tool_validation_outcome": "TimedOut",
            "tool_changed_files_count": "0",
            "generated_test_method_name": "",
            "coverage_delta": "0",
            "mutation_score_delta": "0",
            "total_duration_seconds": "90",
        },
    ])


def test_overview_counts_every_normalized_outcome_category():
    attempts = normalize_attempts(_raw_rows())
    overview = build_overview(attempts)
    outcomes = overview["outcomes"]

    assert outcomes["validated_successes"] == 2
    assert outcomes["positive_impact_attempts"] == 1
    assert outcomes["validated_low_impact_attempts"] == 1
    assert outcomes["outcome_validatedevidencepositive_total"] == 1
    assert outcomes["outcome_validatedlowimpact_agentic"] == 1
    assert outcomes["outcome_timedout_agentic"] == 1


def test_failure_case_export_contains_only_failures():
    attempts = normalize_attempts(_raw_rows())
    cases = build_failure_cases(attempts, db_paths=(), artifacts_root=None)

    # Only non-validated attempts should appear
    assert cases["validated_success"].fillna(False).any() == False  # noqa: E712
    # The timed-out agentic attempt is a failure and must be included
    labels = set(cases["preliminary_failure_label"])
    assert "timeout" in labels
    # Low-impact passing attempts must NOT be included
    assert "low_impact" not in labels


def test_audit_checks_agentic_contract_and_links():
    attempts = pd.DataFrame([
        {
            "lane": "agentic",
            "tool_attempt_id": "",
            "tool_run_status": "Completed",
            "tool_post_attempt_test_run_id": pd.NA,
            "tool_artifact_path": "",
            "produced_change": True,
            "generated_test_count": 0,
            "outcome_classification": pd.NA,
        }
    ])

    findings = []
    findings += audit_agentic_identity_and_artifacts(attempts)
    findings += audit_agentic_no_post_attempt(attempts)
    findings += audit_generated_test_links(attempts)
    findings += audit_outcome_classification(attempts)

    checks = {f["check"] for f in findings}
    assert "missing_tool_attempt_id" in checks
    assert "agentic_missing_artifact_path" in checks
    assert "agentic_missing_post_attempt_measurement" in checks
    assert "agentic_missing_generated_test_links" in checks
    assert "unclassified_attempts" in checks


def test_exact_mcnemar_uses_binomial_discordant_pairs():
    result = mcnemar_test(
        pd.Series([True, True, True, False, False]),
        pd.Series([False, False, False, False, False]),
    )

    assert result.n == 5
    assert result.p_value == pytest.approx(0.25)
    assert "n01=0" in result.note
    assert "n10=3" in result.note


def test_training_mapping_export_runs_against_current_minimal_schema(tmp_path):
    db_path = tmp_path / "fixture.db"
    conn = sqlite3.connect(db_path)
    try:
        conn.executescript(
            """
            CREATE TABLE projects (
                id INTEGER PRIMARY KEY,
                owner TEXT,
                repo_name TEXT,
                last_analyzed_commit TEXT
            );
            CREATE TABLE files (
                id INTEGER PRIMARY KEY,
                file_path TEXT
            );
            CREATE TABLE objects (
                id INTEGER PRIMARY KEY,
                file_id INTEGER,
                full_string TEXT
            );
            CREATE TABLE members (
                id INTEGER PRIMARY KEY,
                object_id INTEGER,
                name TEXT,
                full_string TEXT,
                start_line_number INTEGER,
                modifiers TEXT
            );
            CREATE TABLE source_test_mappings (
                id INTEGER PRIMARY KEY,
                project_id INTEGER,
                source_member_id INTEGER,
                test_member_id INTEGER,
                evidence_kind TEXT,
                is_grounded INTEGER,
                confidence REAL,
                access_path_strategy TEXT,
                path_length INTEGER
            );
            CREATE TABLE candidate_inventory (
                id INTEGER PRIMARY KEY,
                project_id INTEGER,
                source_member_id INTEGER,
                source_test_mapping_id INTEGER,
                risk_score REAL,
                metric_driven_score REAL,
                test_state TEXT,
                recommended_action TEXT
            );
            CREATE TABLE code_metrics (
                id INTEGER PRIMARY KEY,
                entity_id INTEGER,
                entity_type TEXT,
                cyclomatic_complexity INTEGER
            );
            CREATE TABLE member_coverages (
                id INTEGER PRIMARY KEY,
                member_id INTEGER,
                coverage_report_id INTEGER,
                line_rate REAL,
                lines_covered INTEGER,
                lines_valid INTEGER
            );
            CREATE TABLE coverage_gaps (
                id INTEGER PRIMARY KEY,
                member_id INTEGER
            );
            CREATE TABLE mutants (
                id INTEGER PRIMARY KEY,
                member_id INTEGER,
                status TEXT
            );
            CREATE TABLE test_smells (
                id INTEGER PRIMARY KEY,
                member_id INTEGER,
                smell_id TEXT
            );

            INSERT INTO projects VALUES (1, 'owner', 'repo', 'abc');
            INSERT INTO files VALUES (1, 'src/Foo.cs'), (2, 'tests/FooTests.cs');
            INSERT INTO objects VALUES (1, 1, 'class Foo {}'), (2, 2, 'class FooTests {}');
            INSERT INTO members VALUES
                (1, 1, 'DoWork', 'void DoWork()', 10, '["public"]'),
                (2, 2, 'DoWork_CoversBranch', 'void DoWork_CoversBranch()', 20, '["public"]');
            INSERT INTO source_test_mappings VALUES (1, 1, 1, 2, 'StaticCall', 1, 0.9, 'Direct', 1);
            INSERT INTO candidate_inventory VALUES (1, 1, 1, 1, 0.8, 0.7, 'HasTests', 'Expand');
            INSERT INTO code_metrics VALUES (1, 1, 'Member', 4);
            INSERT INTO member_coverages VALUES (1, 1, 1, 0.5, 5, 10);
            INSERT INTO coverage_gaps VALUES (1, 1), (2, 1);
            INSERT INTO mutants VALUES (1, 1, 'Killed'), (2, 1, 'Survived');
            INSERT INTO test_smells VALUES (1, 2, 'AssertionRoulette');
            """
        )

        df = build_mapping_rows(conn)
    finally:
        conn.close()

    assert len(df) == 1
    row = df.iloc[0]
    assert row["repo_owner"] == "owner"
    assert row["test_smell_count"] == 1
    assert row["coverage_gap_count"] == 2
    assert row["mutation_score"] == pytest.approx(50.0)
