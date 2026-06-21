"""Integration tests for building evaluation datasets from current-format CSV rows."""

from __future__ import annotations

import pandas as pd
import pytest

from analysis.build_evaluation_dataset import (
    build_attempts_dataset,
    build_generated_tests_dataset_from_raw,
    save_datasets,
)
from analysis.normalize import (
    build_candidate_summary,
    build_paired_comparison,
    build_repository_summary,
)
from analysis.schema import LANE_AGENTIC, LANE_LLM


def _llm_row(**kwargs) -> dict:
    row = {
        "experiment_run_id": "1",
        "producer_lane": "testmap",
        "tool_id": "",
        "repo_owner": "owner",
        "repo_name": "repo-a",
        "repo_url": "https://example.invalid/owner/repo-a",
        "commit_hash": "abc",
        "source_member_id": "1",
        "source_method_name": "DoWork",
        "source_method_signature": "void DoWork()",
        "generation_attempt_id": "1",
        "attempt_number": "1",
        "failure_kind": "None",
        "generated_test_passed": "True",
        "generated_test_compiled": "True",
        "generated_test_executed": "True",
        "coverage_before": "0.10",
        "coverage_after": "0.30",
        "coverage_delta": "0.20",
        "mutation_score_before": "10",
        "mutation_score_after": "20",
        "mutation_score_delta": "10",
        "total_duration_seconds": "12",
        "total_tokens": "1000",
    }
    row.update(kwargs)
    return row


def _agentic_row(**kwargs) -> dict:
    row = {
        "experiment_run_id": "1",
        "producer_lane": "agent-tool",
        "tool_attempt_id": "101",
        "tool_id": "codex",
        "repo_owner": "owner",
        "repo_name": "repo-a",
        "repo_url": "https://example.invalid/owner/repo-a",
        "commit_hash": "abc",
        "source_member_id": "1",
        "source_method_name": "DoWork",
        "source_method_signature": "void DoWork()",
        "tool_run_status": "Completed",
        "tool_validation_outcome": "Passed",
        "tool_observed_outcome": "",
        "tool_changed_files_count": "2",
        "tool_post_attempt_test_run_id": "10",
        "generated_test_method_name": "DoWork_CoversBranch",
        "generated_test_passed": "True",
        "generated_test_compiled": "True",
        "generated_test_executed": "True",
        "coverage_before": "0.10",
        "coverage_after": "0.45",
        "coverage_delta": "0.35",
        "mutation_score_before": "10",
        "mutation_score_after": "30",
        "mutation_score_delta": "20",
        "total_duration_seconds": "40",
        "total_tokens": "5000",
        "failure_kind": "",
        "generation_attempt_id": "0",
    }
    row.update(kwargs)
    return row


@pytest.fixture(scope="module")
def raw_df() -> pd.DataFrame:
    return pd.DataFrame([
        _llm_row(generation_attempt_id="1", source_member_id="1"),
        _llm_row(generation_attempt_id="2", source_member_id="2",
                 failure_kind="Runtime", generated_test_passed="False",
                 coverage_delta="0", mutation_score_delta="0"),
        _llm_row(generation_attempt_id="3", repo_name="repo-b", commit_hash="def",
                 source_member_id="3", coverage_delta="0", mutation_score_delta="0"),
        _agentic_row(tool_attempt_id="101", source_member_id="1",
                     generated_test_method_name="TestA"),
        _agentic_row(tool_attempt_id="101", source_member_id="1",
                     generated_test_method_name="TestB"),
        _agentic_row(tool_attempt_id="102", source_member_id="2",
                     generated_test_method_name="TestLowImpact",
                     coverage_delta="0", mutation_score_delta="0"),
        _agentic_row(tool_attempt_id="201", repo_name="repo-b", commit_hash="def",
                     source_member_id="3", tool_run_status="TimedOut",
                     tool_validation_outcome="TimedOut", tool_changed_files_count="0",
                     generated_test_method_name="", coverage_delta="0",
                     mutation_score_delta="0", total_tokens=""),
    ])


@pytest.fixture(scope="module")
def attempts(raw_df):
    return build_attempts_dataset(raw_df)


@pytest.fixture(scope="module")
def candidates(attempts):
    return build_candidate_summary(attempts)


@pytest.fixture(scope="module")
def repositories(candidates):
    return build_repository_summary(candidates)


@pytest.fixture(scope="module")
def paired(candidates):
    return build_paired_comparison(candidates)


@pytest.fixture(scope="module")
def gen_tests(raw_df):
    return build_generated_tests_dataset_from_raw(raw_df)


def test_attempt_dataset_has_both_lanes(attempts):
    assert set(attempts["lane"]) == {LANE_LLM, LANE_AGENTIC}


def test_agentic_multi_test_attempt_collapses_once(attempts):
    agentic = attempts[attempts["tool_attempt_id"] == 101]
    assert len(agentic) == 1
    assert agentic["generated_test_count"].iloc[0] == 2


def test_generated_tests_preserve_raw_grain(gen_tests, raw_df):
    assert len(gen_tests) == len(raw_df)
    rows = gen_tests[gen_tests["tool_attempt_id"] == 101]
    assert len(rows) == 2
    assert set(rows["impact_attribution"]) == {"attempt_level"}


def test_generated_tests_carry_parent_attempt_metrics(gen_tests):
    row = gen_tests[gen_tests["tool_attempt_id"] == 101].iloc[0]
    assert row["coverage_delta"] == pytest.approx(0.35)
    assert row["mutation_score_delta"] == pytest.approx(20)
    assert row["positive_impact"] == True  # noqa: E712


def test_low_impact_pass_is_validated_not_positive(attempts):
    row = attempts[attempts["tool_attempt_id"] == 102].iloc[0]
    assert row["validated_success"] == True  # noqa: E712
    assert row["validated_low_impact"] == True  # noqa: E712
    assert row["positive_impact"] == False  # noqa: E712


def test_candidate_summary_keeps_success_and_impact_separate(candidates):
    row = candidates[
        (candidates["lane"] == LANE_AGENTIC) &
        (candidates["source_member_id"] == 2)
    ].iloc[0]
    assert row["any_validated_success"] == True  # noqa: E712
    assert row["any_positive_impact"] == False  # noqa: E712


def test_repository_summary_has_two_repositories(repositories):
    assert repositories["repository_key"].nunique() == 2
    assert set(repositories["lane"]) == {LANE_LLM, LANE_AGENTIC}


def test_paired_comparison_uses_candidate_grain(paired):
    winners = set(paired["winner"])
    assert "tie_success" in winners
    assert "llm_won" in winners


def test_save_datasets_writes_expected_files(attempts, candidates, repositories, gen_tests, tmp_path):
    from analysis.summaries import build_overview

    overview = build_overview(attempts)
    save_datasets(
        attempts=attempts,
        candidates=candidates,
        repositories=repositories,
        generated_tests=gen_tests,
        overview=overview,
        output_dir=tmp_path,
        tool_generated_test_links=pd.DataFrame([
            {"tool_attempt_id": 101, "member_id": 9001},
        ]),
    )

    expected = [
        "evaluation_attempts.csv",
        "evaluation_candidates.csv",
        "evaluation_repositories.csv",
        "generated_tests.csv",
        "tool_generated_test_links.csv",
        "evaluation_overview.json",
        "evaluation_overview.csv",
    ]
    for name in expected:
        assert (tmp_path / name).exists()


def test_attempts_csv_roundtrip(attempts, candidates, repositories, tmp_path):
    from analysis.summaries import build_overview

    save_datasets(
        attempts,
        candidates,
        repositories,
        pd.DataFrame(),
        build_overview(attempts),
        tmp_path,
    )
    reloaded = pd.read_csv(tmp_path / "evaluation_attempts.csv")
    assert len(reloaded) == len(attempts)
