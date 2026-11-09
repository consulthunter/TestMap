"""Audit behaviour found wrong on the first multi-target schema-4 run (the pilot).

The pilot combined four targets into one results file, each its own experiment run,
and shipped lineage paths as the producer's flat ordered step list.
"""

from __future__ import annotations

import json

import pandas as pd

from analysis.audit_evaluation_data import _audit_serialized_paths, audit_assertion_lineage
from tests.test_assertion_lineage import _normalized_bundle


def _checks(attempts, generated, assertions) -> set[str]:
    findings = audit_assertion_lineage(attempts, generated, assertions, sidecar_present=True)
    return {finding["check"] for finding in findings}


def test_observation_ids_repeat_legitimately_across_runs():
    attempts, generated, assertions = _normalized_bundle()
    assertions["observation_id"] = [1, 1]
    assertions["experiment_run_uid"] = ["run-1", "run-2"]
    assert "duplicate_assertion_observation_id" not in _checks(attempts, generated, assertions)

    assertions["experiment_run_uid"] = ["run-1", "run-1"]
    assert "duplicate_assertion_observation_id" in _checks(attempts, generated, assertions)


def test_not_applicable_attempt_with_null_counts_reconciles():
    attempts, generated, assertions = _normalized_bundle()
    not_applicable = attempts.copy()
    not_applicable["attempt_id"] = "attempt-2"
    not_applicable["assertion_measurement_status"] = "NotApplicable"
    for column in ("recognized_assertion_count", "traced_assertion_count",
                   "trivial_assertion_count", "unresolved_assertion_count"):
        not_applicable[column] = float("nan")
    combined = pd.concat([attempts, not_applicable], ignore_index=True)
    assert "assertion_attempt_count_mismatch" not in _checks(combined, generated, assertions)


def test_child_rows_accept_the_measurement_vocabulary():
    # The producer maps Classified to Complete on generated-test result rows.
    attempts, generated, assertions = _normalized_bundle()
    generated["assertion_measurement_status"] = "Complete"
    checks = _checks(attempts, generated, assertions)
    assert "invalid_assertion_summary_status" not in checks
    assert "assertion_attempt_child_count_mismatch" not in checks


def test_canonical_attempt_id_matches_the_producer_key_with_resolved_commit():
    import hashlib
    import sqlite3

    from analysis.db import get_canonical_attempt_identities

    commit = "0123456789abcdef0123456789abcdef01234567"
    conn = sqlite3.connect(":memory:")
    conn.executescript(f"""
        CREATE TABLE projects (id INTEGER PRIMARY KEY, owner TEXT, repo_name TEXT);
        CREATE TABLE experiment_runs (id INTEGER PRIMARY KEY, project_id INTEGER,
            experiment_series_id TEXT, run_uid TEXT, resolved_commit TEXT);
        CREATE TABLE experiment_matrix_work_items (id INTEGER PRIMARY KEY, stable_key TEXT);
        CREATE TABLE candidate_methods (id INTEGER PRIMARY KEY, experiment_run_id INTEGER);
        CREATE TABLE generation_attempts (id INTEGER PRIMARY KEY, candidate_method_id INTEGER,
            experiment_matrix_work_item_id INTEGER, attempt_number INTEGER);
        CREATE TABLE tool_attempts (id INTEGER PRIMARY KEY, experiment_run_id INTEGER,
            matrix_work_item_id INTEGER, attempt_number INTEGER);
        INSERT INTO projects VALUES (1, 'Owner', 'Repo');
        INSERT INTO experiment_runs VALUES (1, 1, '', 'run-uid', '{commit.upper()}');
        INSERT INTO experiment_matrix_work_items VALUES (1, 'key-1');
        INSERT INTO candidate_methods VALUES (1, 1);
        INSERT INTO generation_attempts VALUES (1, 1, 1, 2);
        INSERT INTO tool_attempts VALUES (1, 1, 1, 1);
    """)
    ids = get_canonical_attempt_identities(conn).set_index("lane")["attempt_id"]

    def producer_key(lane: str, attempt: int) -> str:
        # AttemptKeyFactory.Create: repo | commit | series | run uid | lane | stable key | attempt
        material = "|".join(["owner/repo", commit, "", "run-uid", lane, "key-1", str(attempt)])
        return hashlib.sha256(material.encode("utf-8")).hexdigest()

    assert ids["llm"] == producer_key("testmap", 2)
    assert ids["agentic"] == producer_key("agent-tool", 1)


def _step(input_index, path_index, step_index, kind, outcome, member_id=None):
    return {"InputIndex": input_index, "PathIndex": path_index, "StepIndex": step_index,
            "StepKind": kind, "Outcome": outcome, "MemberId": member_id}


def test_flat_step_list_is_grouped_into_paths():
    # Two inputs, each a contiguous path from step 0 with one terminal.
    flat = [
        _step(0, 0, 0, "AssertionInput", "Continue"),
        _step(0, 0, 1, "ProductionMember", "Traced", member_id=10),
        _step(1, 0, 0, "AssertionInput", "Continue"),
        _step(1, 0, 1, "Literal", "Trivial"),
    ]
    observations = pd.DataFrame([{
        "category": "Traced", "lineage_paths_json": json.dumps(flat),
    }])
    assert _audit_serialized_paths(observations) == []


def test_flat_step_list_still_reports_a_real_gap():
    flat = [
        _step(0, 0, 0, "AssertionInput", "Continue"),
        _step(0, 0, 2, "ProductionMember", "Traced", member_id=10),
    ]
    observations = pd.DataFrame([{
        "category": "Traced", "lineage_paths_json": json.dumps(flat),
    }])
    checks = {f["check"] for f in _audit_serialized_paths(observations)}
    assert "broken_assertion_path_terminal" in checks
