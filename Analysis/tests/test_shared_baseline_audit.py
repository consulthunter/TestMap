"""Both lanes compare a candidate against one targeted baseline measured before either runs."""

from __future__ import annotations

import sqlite3

import pandas as pd

from analysis.audit_evaluation_data import audit_shared_baselines
from analysis.db import get_shared_baseline_bundle


def _llm(rows):
    return pd.DataFrame(rows, columns=["candidate_method_id", "baseline_test_run_id"])


def _tool(rows):
    return pd.DataFrame(rows, columns=["candidate_method_id", "targeted_baseline_id"])


def test_one_baseline_per_candidate_passes():
    llm = _llm([(1, 10), (1, 10), (2, 20)])
    tool = _tool([(1, 10), (2, 20)])

    assert audit_shared_baselines(llm, tool, shared_baseline_producer=True) == []


def test_lane_mismatch_is_an_error_for_the_shared_baseline_producer():
    llm = _llm([(1, 10), (2, 21)])
    tool = _tool([(1, 10), (2, 20)])

    findings = audit_shared_baselines(llm, tool, shared_baseline_producer=True)

    assert [f["check"] for f in findings] == ["candidate_baseline_mismatch"]
    assert findings[0]["severity"] == "error"
    assert findings[0]["count"] == 1
    assert "[2]" in findings[0]["detail"]


def test_per_attempt_baselines_in_legacy_databases_are_info():
    llm = _llm([(1, 10), (1, 11)])
    tool = _tool([(1, 12)])

    findings = audit_shared_baselines(llm, tool, shared_baseline_producer=False)

    assert [(f["check"], f["severity"]) for f in findings] == [("legacy_per_attempt_baselines", "info")]


def test_missing_baseline_ids_are_not_counted_as_a_second_baseline():
    # Attempts that never reached measurement record no baseline id.
    llm = _llm([(1, 10), (1, None)])
    tool = _tool([(1, None)])

    assert audit_shared_baselines(llm, tool, shared_baseline_producer=True) == []


def test_bundle_reads_both_lanes_and_detects_the_shared_baseline_table():
    conn = sqlite3.connect(":memory:")
    conn.executescript(
        """
        CREATE TABLE generation_attempts (id INTEGER, candidate_method_id INTEGER);
        CREATE TABLE generated_test_executions (generation_attempt_id INTEGER, baseline_test_run_id INTEGER);
        CREATE TABLE tool_attempts (candidate_method_id INTEGER, targeted_baseline_id INTEGER);
        INSERT INTO generation_attempts VALUES (100, 1);
        INSERT INTO generated_test_executions VALUES (100, 10);
        INSERT INTO tool_attempts VALUES (1, 10);
        """
    )

    llm, tool, shared = get_shared_baseline_bundle(conn)

    assert llm.to_dict("records") == [{"candidate_method_id": 1, "baseline_test_run_id": 10}]
    assert tool.to_dict("records") == [{"candidate_method_id": 1, "targeted_baseline_id": 10}]
    assert shared is False

    conn.execute("CREATE TABLE candidate_baselines (id INTEGER)")
    assert get_shared_baseline_bundle(conn)[2] is True
