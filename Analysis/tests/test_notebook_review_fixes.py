"""Regressions for the exploratory-notebook review: producer grain, infrastructure
failures, generated-test rollup, failure-case export, and small-sample statistics."""

from __future__ import annotations

import pandas as pd
import pytest

from analysis.build_evaluation_dataset import build_attempts_dataset
from analysis.export_failure_cases import (
    CASE_MARKDOWN_TEMPLATE,
    build_failure_cases,
    sample_cases,
    write_case_dir,
)
from analysis.normalize import (
    build_candidate_summary,
    build_paired_comparison,
    build_repository_summary,
    normalize_attempts,
)
from analysis.statistics import fisher_min_p_value, wilson_ci
from tests.test_build_evaluation_dataset import _agentic_row, _llm_row


def _llm(attempt_id: str, number: int, model: str = "glm", **kwargs) -> dict:
    return _llm_row(attempt_id=attempt_id, generation_attempt_id=attempt_id.split("-")[-1],
                    attempt_number=str(number), model=model, **kwargs)


def _failed_llm(attempt_id: str, number: int, summary: str, model: str = "glm",
                kind: str = "Generation", **kwargs) -> dict:
    return _llm(attempt_id, number, model=model, failure_kind=kind,
                failure_stage=kind.lower(), failure_summary=summary,
                generated_test_passed="False", coverage_delta="0", mutation_score_delta="0",
                **kwargs)


# ---------------------------------------------------------------------------
# Producer and infrastructure failures
# ---------------------------------------------------------------------------

def test_producer_is_model_for_llm_and_tool_for_agentic():
    attempts = normalize_attempts(pd.DataFrame([
        _llm("llm-1", 1, model="gpt"),
        _agentic_row(attempt_id="agent-1", tool_attempt_id="1", tool_id="codex", model="gpt"),
    ]))
    assert dict(zip(attempts["lane"], attempts["producer"])) == {"llm": "gpt", "agentic": "codex"}


@pytest.mark.parametrize("summary, reason", [
    ("The security token included in the request is invalid.", "provider_auth"),
    ("Service request failed.\r\nStatus: 400 (Bad Request)\r\n", "provider_http_error"),
    ("Retry failed after 4 tries. (The operation was cancelled because it exceeded "
     "the configured timeout of 0:01:40.", "provider_timeout"),
    ("No previous attempt available for repair.", "repair_input_missing"),
])
def test_llm_infrastructure_failures_are_classified(summary, reason):
    attempts = normalize_attempts(pd.DataFrame([_failed_llm("llm-1", 1, summary)]))
    assert attempts["infrastructure_failure"].iloc[0] == True  # noqa: E712
    assert attempts["infrastructure_failure_reason"].iloc[0] == reason


def test_model_output_failures_are_not_infrastructure():
    attempts = normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "A helper method in the patch did not parse as a valid C# method declaration."),
        _failed_llm("llm-2", 2, "Docker build validation failed before test execution.", kind="Compilation"),
    ]))
    assert not attempts["infrastructure_failure"].any()


def test_agentic_container_start_failure_is_infrastructure():
    attempts = normalize_attempts(pd.DataFrame([
        _agentic_row(attempt_id="agent-1", tool_attempt_id="1", tool_run_status="ToolCrashed",
                     tool_validation_outcome="", failure_kind="ToolExecution",
                     failure_summary="Tool 'aider-custom-glm' exited with code 125."),
        _agentic_row(attempt_id="agent-2", tool_attempt_id="2", tool_run_status="TimedOut",
                     tool_validation_outcome="TimedOut", failure_kind="ToolExecution",
                     failure_summary="Tool 'x' timed out after 2700 seconds."),
    ]))
    by_id = attempts.set_index("attempt_id")
    assert by_id.loc["agent-1", "infrastructure_failure_reason"] == "container_start_failed"
    # A tool that ran out of time did run; the timeout is the tool's outcome.
    assert by_id.loc["agent-2", "infrastructure_failure"] == False  # noqa: E712


def test_overview_counts_infrastructure_failures():
    from analysis.summaries import build_overview
    attempts = normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "The security token included in the request is invalid."),
        _llm("llm-2", 1, model="gpt"),
    ]))
    outcomes = build_overview(attempts)["outcomes"]
    assert outcomes["infrastructure_failure_attempts"] == 1
    assert outcomes["infrastructure_failure_llm"] == 1


# ---------------------------------------------------------------------------
# Candidate grain
# ---------------------------------------------------------------------------

def test_candidate_rows_are_per_producer_with_pass_at_one():
    attempts = normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "A helper method in the patch did not parse.", model="glm"),
        _llm("llm-2", 2, model="glm"),
        _llm("llm-3", 1, model="gpt"),
    ]))
    candidates = build_candidate_summary(attempts).set_index("producer")
    assert len(candidates) == 2
    assert candidates.loc["glm", "attempt_count"] == 2
    assert candidates.loc["glm", "first_attempt_success"] == False  # noqa: E712
    assert candidates.loc["glm", "any_validated_success"] == True  # noqa: E712
    assert candidates.loc["gpt", "first_attempt_success"] == True  # noqa: E712


def test_budget_arms_are_separate_chains():
    attempts = normalize_attempts(pd.DataFrame([
        _llm("llm-1", 1, model="glm", budget_mode="PassAt1"),
        _failed_llm("llm-2", 1, "A helper method in the patch did not parse.", model="glm",
                     budget_mode="PassAt1RepairAt5"),
        _llm("llm-3", 2, model="glm", budget_mode="PassAt1RepairAt5"),
    ]))
    candidates = build_candidate_summary(attempts).set_index("budget_mode")
    assert candidates.loc["PassAt1", "first_attempt_success"] == True  # noqa: E712
    assert candidates.loc["PassAt1RepairAt5", "first_attempt_success"] == False  # noqa: E712
    assert candidates.loc["PassAt1RepairAt5", "attempt_count"] == 2
    paired = build_paired_comparison(
        pd.concat([build_candidate_summary(attempts), pd.DataFrame([{
            "candidate_key": attempts["candidate_key"].iloc[0], "lane": "agentic",
            "producer": "openhands-glm", "first_attempt_success": False}])]),
        llm_producer="glm", agentic_producer="openhands-glm", llm_budget_mode="PassAt1",
        outcome_col="first_attempt_success")
    assert paired["winner"].tolist() == ["llm_won"]


def test_agentic_validation_outcome_sets_the_label():
    attempts = normalize_attempts(pd.DataFrame([
        _agentic_row(attempt_id="agent-1", tool_attempt_id="1", tool_validation_outcome="BuildFailed"),
        _agentic_row(attempt_id="agent-2", tool_attempt_id="2", tool_validation_outcome="TestsFailed"),
    ]))
    cases = build_failure_cases(attempts, db_paths=(), artifacts_root=None).set_index("attempt_id")
    assert cases.loc["agent-1", "preliminary_failure_label"] == "build_failed"
    assert cases.loc["agent-2", "preliminary_failure_label"] == "test_failed"


def test_infrastructure_only_producer_has_no_outcome():
    attempts = normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "The security token included in the request is invalid.", model="haiku"),
        _failed_llm("llm-2", 2, "No previous attempt available for repair.", model="haiku"),
    ]))
    row = build_candidate_summary(attempts).iloc[0]
    assert row["infrastructure_failure_count"] == 2
    assert row["evaluable_attempt_count"] == 0
    assert pd.isna(row["any_validated_success"])
    assert pd.isna(row["first_attempt_success"])


def test_infrastructure_first_attempt_leaves_pass_at_one_missing():
    attempts = normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "Service request failed.\r\nStatus: 400 (Bad Request)", model="kimi"),
        _llm("llm-2", 2, model="kimi"),
    ]))
    row = build_candidate_summary(attempts).iloc[0]
    assert pd.isna(row["first_attempt_success"])
    assert row["any_validated_success"] == True  # noqa: E712


def test_repository_summary_counts_distinct_candidates_and_skips_missing_outcomes():
    candidates = pd.DataFrame([
        {"repository_key": "rk", "lane": "llm", "candidate_key": "k1", "any_validated_success": True},
        {"repository_key": "rk", "lane": "llm", "candidate_key": "k1", "any_validated_success": False},
        {"repository_key": "rk", "lane": "llm", "candidate_key": "k2", "any_validated_success": pd.NA},
    ])
    row = build_repository_summary(candidates).iloc[0]
    assert row["candidate_count"] == 2
    assert row["producer_result_count"] == 3
    assert row["validated_success_rate"] == pytest.approx(0.5)


def _producer_candidates() -> pd.DataFrame:
    return pd.DataFrame([
        {"candidate_key": "k1", "lane": "llm", "producer": "glm", "first_attempt_positive_impact": True},
        {"candidate_key": "k1", "lane": "llm", "producer": "gpt", "first_attempt_positive_impact": False},
        {"candidate_key": "k1", "lane": "agentic", "producer": "openhands-glm", "first_attempt_positive_impact": False},
        {"candidate_key": "k2", "lane": "llm", "producer": "glm", "first_attempt_positive_impact": pd.NA},
        {"candidate_key": "k2", "lane": "agentic", "producer": "openhands-glm", "first_attempt_positive_impact": True},
    ])


def test_paired_comparison_pairs_named_producers():
    paired = build_paired_comparison(_producer_candidates(), llm_producer="glm",
                                     agentic_producer="openhands-glm",
                                     outcome_col="first_attempt_positive_impact").set_index("candidate_key")
    assert paired.loc["k1", "winner"] == "llm_won"
    # A missing pass@1 outcome (infrastructure) is not a comparable pair.
    assert paired.loc["k2", "winner"] == "agentic_only"
    assert paired.loc["k1", "llm_producer"] == "glm"


def test_paired_comparison_rejects_pooled_producers():
    with pytest.raises(ValueError, match="name the LLM producer"):
        build_paired_comparison(_producer_candidates())


# ---------------------------------------------------------------------------
# Generated-test rollup
# ---------------------------------------------------------------------------

def test_agentic_attempt_takes_smells_and_metrics_from_child_tests():
    raw = pd.DataFrame([_agentic_row(attempt_id="agent-1", tool_attempt_id="1")])
    children = pd.DataFrame([
        _agentic_row(row_kind="generated_test", attempt_id="agent-1", tool_attempt_id="1",
                     generated_test_method_name="A", generated_test_smells="Magic Number=1; Eager Test=2",
                     generated_test_cc="1", generated_test_sloc="10"),
        _agentic_row(row_kind="generated_test", attempt_id="agent-1", tool_attempt_id="1",
                     generated_test_method_name="B", generated_test_smells="Magic Number=1",
                     generated_test_cc="3", generated_test_sloc="20"),
    ])
    row = build_attempts_dataset(raw, children).iloc[0]
    assert row["generated_test_count"] == 2
    assert row["generated_test_smell_count"] == 4
    assert row["generated_test_smell_types"] == '["Eager Test", "Magic Number"]'
    assert row["generated_test_cc"] == pytest.approx(2.0)
    assert row["generated_test_sloc"] == pytest.approx(15.0)


# ---------------------------------------------------------------------------
# Failure-case export
# ---------------------------------------------------------------------------

def _failure_attempts() -> pd.DataFrame:
    return normalize_attempts(pd.DataFrame([
        _failed_llm("llm-1", 1, "The security token included in the request is invalid.", model="haiku"),
        _failed_llm("llm-2", 1, "A helper method in the patch did not parse.", model="glm"),
        _llm("llm-3", 2, model="glm"),
        _failed_llm("llm-4", 1, "Docker build validation failed before test execution.",
                    model="gpt", kind="Compilation"),
    ]))


def test_failure_cases_exclude_infrastructure_by_default():
    cases = build_failure_cases(_failure_attempts(), db_paths=(), artifacts_root=None)
    assert set(cases["attempt_id"]) == {"llm-2", "llm-4"}
    with_infra = build_failure_cases(_failure_attempts(), db_paths=(), artifacts_root=None,
                                     include_infrastructure=True)
    assert "infrastructure" in set(with_infra["preliminary_failure_label"])


def test_failure_cases_label_generation_and_record_chain_outcome():
    cases = build_failure_cases(_failure_attempts(), db_paths=(), artifacts_root=None).set_index("attempt_id")
    assert cases.loc["llm-2", "preliminary_failure_label"] == "generation_failed"
    assert cases.loc["llm-4", "preliminary_failure_label"] == "build_failed"
    assert cases.loc["llm-2", "chain_succeeded"] == True  # noqa: E712
    assert cases.loc["llm-4", "chain_succeeded"] == False  # noqa: E712
    assert cases.loc["llm-2", "is_first_attempt"] == True  # noqa: E712


def test_stratified_sample_is_reproducible_and_keeps_label_column():
    cases = pd.DataFrame({"preliminary_failure_label": list("aaaabbbc"), "x": range(8)})
    first = sample_cases(cases, strategy="stratified-label", top_n=2, seed=7)
    second = sample_cases(cases, strategy="stratified-label", top_n=2, seed=7)
    assert first.equals(second)
    assert first["preliminary_failure_label"].value_counts().to_dict() == {"a": 2, "b": 2, "c": 1}


def test_high_severity_is_safety_labels_only():
    cases = pd.DataFrame({"preliminary_failure_label": ["build_failed", "production_code_modified", "timeout"]})
    severe = sample_cases(cases, strategy="high-severity")
    assert severe["preliminary_failure_label"].tolist() == ["production_code_modified"]


def test_case_markdown_carries_summary_and_producer(tmp_path):
    case = build_failure_cases(_failure_attempts(), db_paths=(), artifacts_root=None).iloc[0]
    text = (write_case_dir(case, tmp_path) / "case.md").read_text(encoding="utf-8")
    assert "**Producer:** glm" in text
    assert "did not parse" in text
    assert "{" not in text.split("## Artifacts")[0]
    assert "outcome_summary" not in CASE_MARKDOWN_TEMPLATE


def test_tool_files_fall_back_to_the_tool_family_name(tmp_path):
    from analysis.export_failure_cases import _tool_file
    (tmp_path / "openhands.events.jsonl").write_text("{}", encoding="utf-8")
    assert _tool_file(tmp_path, "openhands-custom-kimi", ".events.jsonl") == "openhands.events.jsonl"
    assert _tool_file(tmp_path, "openhands-custom-kimi", ".stderr.log") == "openhands-custom-kimi.stderr.log"


def test_moved_artifact_and_log_paths_are_re_rooted(tmp_path):
    from analysis.export_failure_cases import _relocate, _relocate_log
    commit_dir = tmp_path / "Output" / "owner" / "repo" / "abc123"
    (commit_dir / "artifacts" / "run").mkdir(parents=True)
    recorded = r"D:\Old\TestMap\Output\owner\repo\abc123\artifacts\run"
    assert _relocate(recorded, commit_dir) == str(commit_dir / "artifacts" / "run")

    log = tmp_path / "logs" / "2026-09-07" / "run.log"
    log.parent.mkdir(parents=True)
    log.write_text("", encoding="utf-8")
    assert _relocate_log(r"D:\Old\TestMap\Logs\2026-09-07\run.log", tmp_path / "logs") == str(log)
    assert _relocate_log(r"D:\Old\TestMap\Logs\2026-09-07\missing.log", tmp_path / "logs").endswith("missing.log")


# ---------------------------------------------------------------------------
# Before/after metric diffs
# ---------------------------------------------------------------------------

def _diff_sets():
    member = 7
    gaps = {(1, member): {10, 11, 12}, (2, member): {12}}
    covered = {(1, member), (2, member)}
    survived = {(1, member): {"a", "b", "c"}, (2, member): {"b"}}
    # "c" timed out after the attempt; "a" was killed; "b" still survives.
    detected = {(1, member): set(), (2, member): {"a", "c", "z"}}
    mutated = {(1, member), (2, member)}
    return member, gaps, covered, survived, detected, mutated


def test_before_after_diff_counts_closed_gaps_and_detected_survivors():
    from analysis.build_evaluation_dataset import before_after_diff
    member, gaps, covered, survived, detected, mutated = _diff_sets()
    assert before_after_diff(1, 2, member, gaps, covered, survived, detected, mutated) == (2, 2)


def test_before_after_diff_is_missing_when_the_after_run_did_not_measure_the_member():
    from analysis.build_evaluation_dataset import before_after_diff
    member, gaps, covered, survived, detected, mutated = _diff_sets()
    # Run 3 recorded neither coverage nor mutants for the member: nothing was closed or killed,
    # and nothing is known.
    assert before_after_diff(1, 3, member, gaps, covered, survived, detected, mutated) == (None, None)


def test_before_after_diff_does_not_count_a_vanished_survivor_as_killed():
    from analysis.build_evaluation_dataset import before_after_diff
    member, gaps, covered, survived, detected, mutated = _diff_sets()
    detected[(2, member)] = set()
    survived[(2, member)] = set()
    assert before_after_diff(1, 2, member, gaps, covered, survived, detected, mutated)[1] == 0


# ---------------------------------------------------------------------------
# Small-sample statistics
# ---------------------------------------------------------------------------

def test_wilson_interval_is_not_degenerate_at_all_successes():
    low, high = wilson_ci(4, 4)
    assert high == pytest.approx(1.0)
    assert low < 0.6


def test_fisher_min_p_value_for_four_versus_four():
    assert fisher_min_p_value(4, 4) == pytest.approx(0.0286, abs=1e-3)
