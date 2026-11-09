from __future__ import annotations

from pathlib import Path

import pandas as pd
import pytest

from analysis.audit_evaluation_data import (
    audit_assertion_lineage,
    audit_child_provenance,
    audit_pinned_provenance,
)
from analysis.files import read_result_grains
from analysis.normalize import normalize_attempts


FIXTURES = Path(__file__).parent / "fixtures" / "pinned_targets"


def _valid_row(**changes) -> dict:
    row = pd.read_csv(FIXTURES / "valid-schema3.csv").iloc[0].to_dict()
    row.update(changes)
    return row


def test_valid_schema3_builds_distinct_family_and_revision_keys():
    normalized = normalize_attempts(pd.DataFrame([_valid_row()]))
    assert normalized.loc[0, "repository_family_key"] == "owner/repository"
    assert "0123456789abcdef0123456789abcdef01234567" in normalized.loc[0, "repository_revision_key"]
    assert not audit_pinned_provenance(normalized)


@pytest.mark.parametrize(
    "changes",
    [
        {"target_id": ""},
        {"resolved_commit": "89abcdef0123456789abcdef0123456789abcdef"},
        {"commit_hash": "89abcdef0123456789abcdef0123456789abcdef"},
        {"target_manifest_sha256": ""},
    ],
)
def test_normalization_rejects_blocking_provenance(changes):
    with pytest.raises(ValueError):
        normalize_attempts(pd.DataFrame([_valid_row(**changes)]))


def test_audit_rejects_success_with_blocking_integrity():
    row = pd.read_csv(FIXTURES / "blocking-integrity.csv")
    findings = audit_pinned_provenance(row)
    assert "success_with_blocking_integrity" in {finding["check"] for finding in findings}


def test_audit_detects_cohort_and_artifact_cross_revision_collisions():
    other = "89abcdef0123456789abcdef0123456789abcdef"
    rows = pd.DataFrame([
        _valid_row(candidate_cohort_id=7, tool_artifact_path="same/path"),
        _valid_row(
            attempt_id="attempt-other", candidate_cohort_id=7, tool_artifact_path="same/path",
            target_id="dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            requested_commit=other, resolved_commit=other, commit_hash=other,
        ),
    ])
    findings = audit_pinned_provenance(rows)
    checks = {finding["check"] for finding in findings}
    assert "cohort_revision_mismatch" in checks
    assert "cross_revision_artifact_path_collision" in checks


def test_child_rows_must_inherit_parent_provenance():
    parent = pd.DataFrame([_valid_row()])
    child = pd.DataFrame([_valid_row(row_kind="generated_test", target_id="d" * 64)])
    findings = audit_child_provenance(parent, child)
    assert findings[0]["check"] == "child_provenance_mismatch"


def test_reader_rejects_non_schema3_file(tmp_path):
    path = tmp_path / "legacy.csv"
    pd.DataFrame([{"results_schema_version": "2.0", "row_kind": "attempt", "attempt_id": "a"}]).to_csv(path, index=False)
    with pytest.raises(ValueError):
        read_result_grains([str(path)])


def test_assertion_audit_detects_cross_lane_policy_mismatch():
    shared = {
        "results_schema_version": "4.0",
        "candidate_key": "owner/repo|commit|candidate",
        "assertion_measurement_status": "Complete",
        "assertion_measurement_reason": "",
        "assertion_catalog_version": "assertion-catalog-v1",
        "assertion_max_depth": 4,
        "recognized_assertion_count": 0,
        "traced_assertion_count": 0,
        "trivial_assertion_count": 0,
        "unresolved_assertion_count": 0,
    }
    attempts = pd.DataFrame([
        {**shared, "attempt_id": "llm", "lane": "llm",
         "assertion_policy_version": "assertion-lineage-v1"},
        {**shared, "attempt_id": "agent", "lane": "agentic",
         "assertion_policy_version": "assertion-lineage-v2"},
    ])
    findings = audit_assertion_lineage(
        attempts, pd.DataFrame(), pd.DataFrame(), sidecar_present=True
    )
    assert "cross_lane_assertion_policy_mismatch" in {
        finding["check"] for finding in findings
    }
