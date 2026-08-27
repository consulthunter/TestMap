from __future__ import annotations

import json
import sqlite3

import pandas as pd
import pytest

from analysis.audit_evaluation_data import (
    audit_assertion_lineage,
    audit_assertion_persistence,
)
from analysis.build_evaluation_dataset import (
    build_attempts_dataset,
    build_generated_tests_dataset_from_raw,
    run as build_datasets,
    save_datasets,
)
from analysis.db import get_assertion_lineage_bundle
from analysis.files import (
    read_result_grains,
    read_schema4_result_bundle,
)
from analysis.normalize import (
    build_traced_assertions_dataset,
    normalize_assertion_observations,
    normalize_attempts,
)


COMMIT = "0123456789abcdef0123456789abcdef01234567"
TARGET = "a" * 64
MANIFEST = "b" * 64
SOURCE = "c" * 64


def _result_row(row_kind: str = "attempt", **changes) -> dict:
    row = {
        "results_schema_version": "4.0",
        "row_kind": row_kind,
        "attempt_id": "attempt-1",
        "experiment_run_id": 1,
        "experiment_run_uid": "run-1",
        "experiment_series_id": "series-1",
        "producer_lane": "testmap",
        "repo_owner": "owner",
        "repo_name": "repo",
        "repo_url": "https://example.invalid/owner/repo",
        "commit_hash": COMMIT,
        "target_id": TARGET,
        "repository_identity": "owner/repo",
        "requested_commit": COMMIT,
        "resolved_commit": COMMIT,
        "target_manifest_sha256": MANIFEST,
        "target_source_sha256": SOURCE,
        "provenance_policy_version": "pinned-target-v1",
        "workspace_integrity_status": "VerifiedClean",
        "source_member_id": 10,
        "source_method_name": "Compute",
        "source_method_signature": "int Compute()",
        "generation_attempt_id": 11,
        "generated_test_member_id": 20,
        "generated_test_method_name": "Compute_returns_value",
        "failure_kind": "None",
        "assertion_measurement_status": (
            "Complete" if row_kind == "attempt" else "Classified"
        ),
        "assertion_measurement_reason": "",
        "assertion_policy_version": "assertion-lineage-v1",
        "assertion_catalog_version": "assertion-catalog-v1",
        "assertion_max_depth": 4,
        "recognized_assertion_count": 2,
        "unrecognized_assertion_count": 0,
        "traced_assertion_count": 1,
        "trivial_assertion_count": 1,
        "unresolved_assertion_count": 0,
        "no_recognized_assertions": False,
        "assertion_analysis_duration_ms": 2.5,
        "assertion_attribution": (
            "attempt_aggregate" if row_kind == "attempt" else "generated_test"
        ),
        "generated_test_assertion_summary_id": 100,
    }
    row.update(changes)
    return row


def _assertion_row(observation_id: int, category: str, ordinal: int) -> dict:
    traced = category == "Traced"
    terminal = {
        "step_index": 1,
        "step_kind": "ProductionMember" if traced else "Literal",
        "member_id": 10 if traced else None,
        "outcome": "Traced" if traced else "Trivial",
    }
    return {
        "assertion_schema_version": "1.0",
        "observation_id": observation_id,
        "attempt_id": "attempt-1",
        "experiment_run_id": 1,
        "experiment_run_uid": "run-1",
        "experiment_series_id": "series-1",
        "producer_lane": "testmap",
        "model": "model",
        "tool_id": "",
        "attempt_number": 1,
        "repository_identity": "owner/repo",
        "resolved_commit": COMMIT,
        "target_id": TARGET,
        "target_manifest_sha256": MANIFEST,
        "target_source_sha256": SOURCE,
        "candidate_method_id": 9,
        "intended_source_member_id": 10,
        "generated_test_assertion_summary_id": 100,
        "generated_test_execution_id": 30,
        "tool_attempt_generated_test_id": pd.NA,
        "test_member_id": 20,
        "test_member_name": "Compute_returns_value",
        "test_file_path": "tests/ComputeTests.cs",
        "test_member_content_hash": "content-hash",
        "assertion_ordinal": ordinal,
        "start_line": 10 + ordinal,
        "start_column": 8,
        "end_line": 10 + ordinal,
        "end_column": 30,
        "framework": "xunit",
        "assertion_style": "classic",
        "assertion_method": "Xunit.Assert.Equal",
        "recognition_kind": "Semantic",
        "expression_hash": f"expr-{ordinal}",
        "category": category,
        "resolution_code": (
            "ProductionInvocation" if traced else "AllInputsTestLocal"
        ),
        "target_relation": "Candidate" if traced else "NoProduction",
        "depth_reached": 1,
        "resolved_production_member_ids": "10" if traced else "",
        "policy_version": "assertion-lineage-v1",
        "assertion_catalog_version": "assertion-catalog-v1",
        "max_depth": 4,
        "trace_summary": "deterministic trace",
        "ordered_lineage_paths_json": json.dumps([{
            "steps": [
                {
                    "step_index": 0,
                    "step_kind": "AssertionInput",
                    "outcome": "Continue",
                },
                terminal,
            ]
        }]),
        "analyzed_at": "2026-07-25T12:00:00Z",
    }


def _normalized_bundle():
    attempts = build_attempts_dataset(pd.DataFrame([_result_row()]))
    generated = build_generated_tests_dataset_from_raw(pd.DataFrame([
        _result_row("generated_test")
    ]))
    assertions = normalize_assertion_observations(pd.DataFrame([
        _assertion_row(1, "Traced", 0),
        _assertion_row(2, "Trivial", 1),
    ]))
    return attempts, generated, assertions


def test_schema4_reader_is_explicit_and_discovers_assertion_sidecar(tmp_path):
    attempt_path = tmp_path / "results.csv"
    generated_path = tmp_path / "results-generated-tests.csv"
    assertion_path = tmp_path / "results.assertions.csv"
    pd.DataFrame([_result_row()]).to_csv(attempt_path, index=False)
    pd.DataFrame([_result_row("generated_test")]).to_csv(generated_path, index=False)
    pd.DataFrame([
        _assertion_row(1, "Traced", 0),
        _assertion_row(2, "Trivial", 1),
    ]).to_csv(assertion_path, index=False)

    with pytest.raises(ValueError):
        read_result_grains([str(attempt_path)])

    attempts, generated, test_results, assertions = read_schema4_result_bundle(
        [str(attempt_path)]
    )
    assert len(attempts) == 1
    assert len(generated) == 1
    assert test_results.empty
    assert len(assertions) == 2


def test_schema4_reader_requires_sidecar_for_publication_claims(tmp_path):
    path = tmp_path / "results.csv"
    pd.DataFrame([_result_row()]).to_csv(path, index=False)
    with pytest.raises(ValueError, match="sidecar"):
        read_schema4_result_bundle([str(path)])


def test_schema4_dataset_build_uses_sidecar_not_legacy_inventory(tmp_path):
    attempt_path = tmp_path / "results.csv"
    generated_path = tmp_path / "results-generated-tests.csv"
    assertion_path = tmp_path / "results.assertions.csv"
    output = tmp_path / "normalized"
    pd.DataFrame([_result_row()]).to_csv(attempt_path, index=False)
    pd.DataFrame([_result_row("generated_test")]).to_csv(generated_path, index=False)
    pd.DataFrame([
        _assertion_row(1, "Traced", 0),
        _assertion_row(2, "Trivial", 1),
    ]).to_csv(assertion_path, index=False)

    build_datasets(
        results=[str(attempt_path)],
        db_paths=(),
        artifacts_root=None,
        output_dir=str(output),
    )

    attempts = pd.read_csv(output / "evaluation_attempts.csv")
    assertions = pd.read_csv(output / "assertion_observations.csv")
    traced = pd.read_csv(output / "traced_assertions.csv")
    assert attempts.loc[0, "recognized_assertion_count"] == 2
    assert "assertion_count" not in attempts.columns
    assert len(assertions) == 2
    assert len(traced) == 1


def test_schema3_normalization_derives_not_measured_never_zero():
    legacy = _result_row(results_schema_version="3.0")
    for key in list(legacy):
        if key.startswith("assertion_") or key.endswith("_assertion_count"):
            legacy.pop(key)
    legacy.pop("recognized_assertion_count", None)
    legacy.pop("unrecognized_assertion_count", None)
    legacy.pop("traced_assertion_count", None)
    legacy.pop("trivial_assertion_count", None)
    legacy.pop("unresolved_assertion_count", None)
    legacy.pop("no_recognized_assertions", None)

    normalized = normalize_attempts(pd.DataFrame([legacy]))
    assert normalized.loc[0, "assertion_measurement_status"] == "NotMeasured"
    assert normalized.loc[0, "assertion_measurement_reason"] == "HistoricalNotMeasured"
    assert pd.isna(normalized.loc[0, "recognized_assertion_count"])
    assert pd.isna(normalized.loc[0, "traced_assertion_count"])


def test_sidecar_normalization_preserves_raw_and_derives_traced_only_view():
    raw = pd.DataFrame([
        _assertion_row(1, "Traced", 0),
        _assertion_row(2, "Trivial", 1),
        _assertion_row(3, "Unresolved", 2)
        | {"resolution_code": "DepthExceeded"},
    ])
    normalized = normalize_assertion_observations(raw)
    traced = build_traced_assertions_dataset(normalized)

    assert len(normalized) == 3
    assert len(traced) == 1
    assert traced.loc[0, "observation_id"] == 1
    assert normalized.loc[0, "assertion_policy_version"] == "assertion-lineage-v1"
    assert normalized.loc[0, "assertion_max_depth"] == 4


def test_save_datasets_writes_raw_and_traced_assertion_views(tmp_path):
    attempts, generated, assertions = _normalized_bundle()
    traced = build_traced_assertions_dataset(assertions)
    save_datasets(
        attempts,
        pd.DataFrame(),
        pd.DataFrame(),
        generated,
        {},
        tmp_path,
        assertion_observations=assertions,
        traced_assertions=traced,
    )
    assert len(pd.read_csv(tmp_path / "assertion_observations.csv")) == 2
    assert len(pd.read_csv(tmp_path / "traced_assertions.csv")) == 1


def test_sqlite_readers_are_empty_for_legacy_and_read_all_four_grains(tmp_path):
    path = tmp_path / "analysis.db"
    connection = sqlite3.connect(path)
    try:
        assert all(frame.empty for frame in get_assertion_lineage_bundle(connection))
        connection.executescript(
            """
            CREATE TABLE assertion_lineage_measurements (id INTEGER PRIMARY KEY, status TEXT);
            CREATE TABLE generated_test_assertion_summaries (id INTEGER PRIMARY KEY, status TEXT);
            CREATE TABLE assertion_observations (id INTEGER PRIMARY KEY, category TEXT);
            CREATE TABLE assertion_lineage_steps (id INTEGER PRIMARY KEY, step_index INTEGER);
            INSERT INTO assertion_lineage_measurements VALUES (1, 'Complete');
            INSERT INTO generated_test_assertion_summaries VALUES (2, 'Classified');
            INSERT INTO assertion_observations VALUES (3, 'Traced');
            INSERT INTO assertion_lineage_steps VALUES (4, 0);
            """
        )
        assert [len(frame) for frame in get_assertion_lineage_bundle(connection)] == [1, 1, 1, 1]
    finally:
        connection.close()


def test_valid_assertion_bundle_passes_strict_audit():
    attempts, generated, assertions = _normalized_bundle()
    findings = audit_assertion_lineage(
        attempts, generated, assertions, sidecar_present=True
    )
    assert findings == []


@pytest.mark.parametrize(
    ("mutate", "expected_check"),
    [
        (
            lambda a, g, o: o.__setitem__("category", ["Invalid", "Trivial"]),
            "invalid_assertion_category",
        ),
        (
            lambda a, g, o: o.__setitem__(
                "observation_id", [1, 1]
            ),
            "duplicate_assertion_observation_id",
        ),
        (
            lambda a, g, o: o.__setitem__(
                "attempt_id", ["missing-attempt", "attempt-1"]
            ),
            "orphan_assertion_observation",
        ),
        (
            lambda a, g, o: o.__setitem__(
                "resolved_production_member_ids", ["", ""]
            ),
            "traced_assertion_without_production",
        ),
        (
            lambda a, g, o: o.__setitem__(
                "lineage_paths_json", ["[]", o.loc[1, "lineage_paths_json"]]
            ),
            "traced_assertion_without_production_terminal",
        ),
        (
            lambda a, g, o: a.__setitem__(
                "recognized_assertion_count", [99]
            ),
            "assertion_attempt_count_mismatch",
        ),
    ],
)
def test_assertion_audit_blocks_seeded_contract_defects(mutate, expected_check):
    attempts, generated, assertions = _normalized_bundle()
    mutate(attempts, generated, assertions)
    findings = audit_assertion_lineage(
        attempts, generated, assertions, sidecar_present=True
    )
    assert expected_check in {finding["check"] for finding in findings}


def test_assertion_audit_blocks_missing_sidecar_and_historical_zero_conversion():
    attempts, generated, assertions = _normalized_bundle()
    missing = audit_assertion_lineage(
        attempts, generated, pd.DataFrame(), sidecar_present=False
    )
    assert "missing_assertion_sidecar" in {finding["check"] for finding in missing}

    historical = attempts.copy()
    historical["results_schema_version"] = "3.0"
    historical["assertion_measurement_status"] = "Complete"
    historical["recognized_assertion_count"] = 0
    findings = audit_assertion_lineage(
        historical, pd.DataFrame(), pd.DataFrame(), sidecar_present=False
    )
    assert "historical_assertion_zero_conversion" in {
        finding["check"] for finding in findings
    }


def test_persistence_audit_blocks_orphans_and_broken_ordered_paths():
    measurements = pd.DataFrame([{"id": 1}])
    summaries = pd.DataFrame([{
        "id": 2,
        "assertion_lineage_measurement_id": 999,
        "status": "Classified",
        "recognized_assertion_count": 1,
    }])
    observations = pd.DataFrame([{
        "id": 3,
        "generated_test_assertion_summary_id": 2,
        "category": "Traced",
    }])
    steps = pd.DataFrame([{
        "id": 4,
        "assertion_observation_id": 3,
        "input_index": 0,
        "path_index": 0,
        "step_index": 2,
        "step_kind": "ProductionMember",
        "member_id": 10,
        "outcome": "Traced",
    }])

    findings = audit_assertion_persistence(
        measurements, summaries, observations, steps
    )
    checks = {finding["check"] for finding in findings}
    assert "orphan_generated_test_assertion_summary" in checks
    assert "broken_persisted_assertion_path_terminal" in checks
