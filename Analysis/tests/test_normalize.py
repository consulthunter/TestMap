"""Unit tests for analysis.normalize."""

from __future__ import annotations

import pandas as pd
import pytest

from analysis.normalize import (
    _compute_produced_change,
    _compute_validated_success,
    build_candidate_summary,
    build_paired_comparison,
    build_repository_summary,
    collapse_agentic_to_attempts,
    make_candidate_key,
    make_repository_key,
    normalize_attempts,
    normalize_lane,
    rename_raw_columns,
)
from analysis.schema import LANE_AGENTIC, LANE_LLM


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

def _llm_row(**kwargs) -> dict:
    """Minimal LLM attempt row matching pilot CSV structure."""
    defaults = {
        "experiment_run_id": "1",
        "producer_lane": "testmap",
        "tool_id": "",
        "repo_owner": "consulthunter",
        "repo_name": "TestMap-Example",
        "commit_hash": "abc123",
        "source_member_id": "2",
        "source_method_name": "Start",
        "failure_kind": "Runtime",
        "generated_test_passed": "False",
        "generated_test_compiled": "True",
        "generated_test_executed": "True",
        "coverage_before": "0.65",
        "coverage_after": "0.82",
        "coverage_delta": "0.17",
        "mutation_score_before": "0.3",
        "mutation_score_after": "0.3",
        "mutation_score_delta": "0",
        "total_duration_seconds": "12.5",
        "total_tokens": "1000",
        "tool_changed_files_count": "0",
        "generation_attempt_id": "1",
        "attempt_number": "1",
    }
    defaults.update(kwargs)
    return defaults


def _agentic_row(**kwargs) -> dict:
    """Minimal agentic generated-test row matching pilot CSV structure."""
    defaults = {
        "experiment_run_id": "1",
        "producer_lane": "agent-tool",
        "tool_id": "codex",
        "repo_owner": "consulthunter",
        "repo_name": "TestMap-Example",
        "commit_hash": "abc123",
        "source_member_id": "2",
        "source_method_name": "Start",
        "tool_run_status": "Completed",
        "tool_validation_outcome": "Passed",
        "tool_changed_files_count": "4",
        "generated_test_method_name": "TestMethod",
        "generated_test_compiled": "True",
        "generated_test_executed": "True",
        "generated_test_passed": "True",
        "coverage_before": "0.65",
        "coverage_after": "1.0",
        "coverage_delta": "0.35",
        "mutation_score_before": "0.3",
        "mutation_score_after": "0.55",
        "mutation_score_delta": "0.25",
        "total_duration_seconds": "45.0",
        "total_tokens": "5000",
        "failure_kind": "",
        "generation_attempt_id": "0",
    }
    defaults.update(kwargs)
    defaults.setdefault("tool_attempt_id", f"{defaults['tool_id']}-{defaults['source_member_id']}")
    return defaults


def _make_df(rows: list[dict]) -> pd.DataFrame:
    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# normalize_lane
# ---------------------------------------------------------------------------

class TestNormalizeLane:
    def test_testmap_maps_to_llm(self):
        assert normalize_lane("testmap") == LANE_LLM

    def test_agent_tool_maps_to_agentic(self):
        assert normalize_lane("agent-tool") == LANE_AGENTIC

    def test_llm_maps_to_llm(self):
        assert normalize_lane("llm") == LANE_LLM

    def test_agentic_maps_to_agentic(self):
        assert normalize_lane("agentic") == LANE_AGENTIC

    def test_agent_underscore_maps_to_agentic(self):
        assert normalize_lane("agent_tool") == LANE_AGENTIC

    def test_direct_maps_to_llm(self):
        assert normalize_lane("direct") == LANE_LLM

    def test_unknown_passes_through(self):
        result = normalize_lane("unknown_lane")
        assert result == "unknown_lane"

    def test_case_insensitive(self):
        assert normalize_lane("TestMap") == LANE_LLM
        assert normalize_lane("AGENT-TOOL") == LANE_AGENTIC

    def test_whitespace_stripped(self):
        assert normalize_lane("  testmap  ") == LANE_LLM


# ---------------------------------------------------------------------------
# rename_raw_columns
# ---------------------------------------------------------------------------

class TestRenameRawColumns:
    def test_producer_lane_renamed_to_lane(self):
        df = _make_df([{"producer_lane": "testmap", "other_col": 1}])
        result = rename_raw_columns(df)
        assert "lane" in result.columns
        assert "producer_lane" not in result.columns

    def test_total_duration_seconds_renamed(self):
        df = _make_df([{"total_duration_seconds": "10.5"}])
        result = rename_raw_columns(df)
        assert "duration_seconds" in result.columns
        assert "total_duration_seconds" not in result.columns

    def test_tool_changed_files_count_renamed(self):
        df = _make_df([{"tool_changed_files_count": "4"}])
        result = rename_raw_columns(df)
        assert "changed_files_count" in result.columns
        assert "tool_changed_files_count" not in result.columns

    def test_unrelated_columns_preserved(self):
        df = _make_df([{"tool_id": "codex", "source_member_id": "2"}])
        result = rename_raw_columns(df)
        assert "tool_id" in result.columns
        assert "source_member_id" in result.columns

    def test_empty_df_returns_empty(self):
        result = rename_raw_columns(pd.DataFrame())
        assert result.empty

    def test_returns_copy_not_inplace(self):
        df = _make_df([{"producer_lane": "testmap"}])
        result = rename_raw_columns(df)
        assert "producer_lane" in df.columns  # original unchanged
        assert "lane" in result.columns


# ---------------------------------------------------------------------------
# make_candidate_key / make_repository_key
# ---------------------------------------------------------------------------

class TestKeyConstruction:
    def test_candidate_key_is_stable(self):
        row = pd.Series({
            "repo_owner": "owner",
            "repo_name": "repo",
            "commit_hash": "abc",
            "source_member_id": "42",
        })
        assert make_candidate_key(row) == make_candidate_key(row)

    def test_candidate_key_includes_source_member_id(self):
        row1 = pd.Series({"repo_owner": "o", "repo_name": "r", "commit_hash": "c",
                           "source_member_id": "1"})
        row2 = pd.Series({"repo_owner": "o", "repo_name": "r", "commit_hash": "c",
                           "source_member_id": "2"})
        assert make_candidate_key(row1) != make_candidate_key(row2)

    def test_candidate_key_differs_by_repo_name(self):
        row1 = pd.Series({"repo_owner": "o", "repo_name": "r1", "commit_hash": "c",
                           "source_member_id": "1"})
        row2 = pd.Series({"repo_owner": "o", "repo_name": "r2", "commit_hash": "c",
                           "source_member_id": "1"})
        assert make_candidate_key(row1) != make_candidate_key(row2)

    def test_candidate_key_differs_by_commit(self):
        row1 = pd.Series({"repo_owner": "o", "repo_name": "r", "commit_hash": "c1",
                           "source_member_id": "1"})
        row2 = pd.Series({"repo_owner": "o", "repo_name": "r", "commit_hash": "c2",
                           "source_member_id": "1"})
        assert make_candidate_key(row1) != make_candidate_key(row2)

    def test_repository_key_is_stable(self):
        row = pd.Series({"repo_owner": "owner", "repo_name": "repo", "commit_hash": "abc"})
        assert make_repository_key(row) == make_repository_key(row)

    def test_repository_key_differs_by_owner(self):
        row1 = pd.Series({"repo_owner": "o1", "repo_name": "r", "commit_hash": "c"})
        row2 = pd.Series({"repo_owner": "o2", "repo_name": "r", "commit_hash": "c"})
        assert make_repository_key(row1) != make_repository_key(row2)

    def test_missing_fields_handled_gracefully(self):
        row = pd.Series({"repo_name": "r"})  # missing repo_owner, commit_hash, source_member_id
        key = make_candidate_key(row)
        assert isinstance(key, str)


# ---------------------------------------------------------------------------
# _compute_validated_success
# ---------------------------------------------------------------------------

class TestComputeValidatedSuccess:
    def _df_with_lane(self, lane: str, **kwargs) -> pd.DataFrame:
        row = {"lane": lane, **kwargs}
        return pd.DataFrame([row])

    # LLM
    def test_llm_none_string_failure_is_success(self):
        # When "None" survives as a string (not parsed as NaN)
        df = self._df_with_lane(LANE_LLM, failure_kind="None")
        result = _compute_validated_success(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_llm_nan_failure_kind_is_success(self):
        # pandas reads "None" from CSV as NaN; NaN means no failure = success
        import numpy as np
        df = self._df_with_lane(LANE_LLM, failure_kind=np.nan)
        result = _compute_validated_success(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_llm_runtime_failure_is_failure(self):
        df = self._df_with_lane(LANE_LLM, failure_kind="Runtime")
        result = _compute_validated_success(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_llm_compilation_failure_is_failure(self):
        df = self._df_with_lane(LANE_LLM, failure_kind="Compilation")
        result = _compute_validated_success(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_llm_generation_failure_is_failure(self):
        df = self._df_with_lane(LANE_LLM, failure_kind="Generation")
        result = _compute_validated_success(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_llm_missing_failure_kind_is_na(self):
        df = self._df_with_lane(LANE_LLM)  # no failure_kind column
        result = _compute_validated_success(df)
        assert pd.isna(result.iloc[0])

    # Agentic
    def test_agentic_passed_is_success(self):
        df = self._df_with_lane(LANE_AGENTIC, tool_validation_outcome="Passed")
        result = _compute_validated_success(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_agentic_tests_failed_is_failure(self):
        df = self._df_with_lane(LANE_AGENTIC, tool_validation_outcome="TestsFailed")
        result = _compute_validated_success(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_agentic_build_failed_is_failure(self):
        df = self._df_with_lane(LANE_AGENTIC, tool_validation_outcome="BuildFailed")
        result = _compute_validated_success(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_agentic_missing_outcome_is_na(self):
        df = self._df_with_lane(LANE_AGENTIC)  # no tool_validation_outcome column
        result = _compute_validated_success(df)
        assert pd.isna(result.iloc[0])

    # Mixed frame
    def test_mixed_frame_applies_correct_lane(self):
        rows = [
            {"lane": LANE_LLM, "failure_kind": "None", "tool_validation_outcome": "TestsFailed"},
            {"lane": LANE_AGENTIC, "failure_kind": "Runtime", "tool_validation_outcome": "Passed"},
        ]
        df = pd.DataFrame(rows)
        result = _compute_validated_success(df)
        assert result.iloc[0] == True   # LLM: failure_kind == 'None'
        assert result.iloc[1] == True   # Agentic: tool_validation_outcome == 'Passed'


# ---------------------------------------------------------------------------
# _compute_produced_change
# ---------------------------------------------------------------------------

class TestComputeProducedChange:
    def _df(self, lane: str, **kwargs) -> pd.DataFrame:
        return pd.DataFrame([{"lane": lane, **kwargs}])

    def test_llm_runtime_failure_produced_something(self):
        df = self._df(LANE_LLM, failure_kind="Runtime")
        result = _compute_produced_change(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_llm_compilation_failure_produced_something(self):
        df = self._df(LANE_LLM, failure_kind="Compilation")
        result = _compute_produced_change(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_llm_generation_failure_produced_nothing(self):
        df = self._df(LANE_LLM, failure_kind="Generation")
        result = _compute_produced_change(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_llm_none_failure_produced_something(self):
        df = self._df(LANE_LLM, failure_kind="None")
        result = _compute_produced_change(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_agentic_changed_files_positive(self):
        df = self._df(LANE_AGENTIC, changed_files_count="4")
        result = _compute_produced_change(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_agentic_changed_files_zero(self):
        df = self._df(LANE_AGENTIC, changed_files_count="0")
        result = _compute_produced_change(df)
        assert result.iloc[0] == False  # noqa: E712

    def test_agentic_fallback_to_run_status(self):
        df = self._df(LANE_AGENTIC, tool_run_status="Completed")
        result = _compute_produced_change(df)
        assert result.iloc[0] == True  # noqa: E712

    def test_agentic_fallback_run_status_failed(self):
        df = self._df(LANE_AGENTIC, tool_run_status="Timeout")
        result = _compute_produced_change(df)
        assert result.iloc[0] == False  # noqa: E712


# ---------------------------------------------------------------------------
# collapse_agentic_to_attempts
# ---------------------------------------------------------------------------

class TestCollapseAgenticToAttempts:
    def _agentic_attempt(self, tool_id: str, source_member_id: str, n_tests: int = 3,
                         outcome: str = "Passed", tool_attempt_id: str | None = None) -> pd.DataFrame:
        """Build *n_tests* generated-test rows for one agentic attempt."""
        rows = []
        attempt_id = tool_attempt_id or f"{tool_id}-{source_member_id}"
        for i in range(n_tests):
            rows.append({
                "experiment_run_id": "1",
                "tool_attempt_id": attempt_id,
                "tool_id": tool_id,
                "repo_owner": "owner",
                "repo_name": "repo",
                "commit_hash": "abc",
                "source_member_id": source_member_id,
                "lane": LANE_AGENTIC,
                "tool_validation_outcome": outcome,
                "changed_files_count": 4,
                "coverage_delta": 0.3,
                "generated_test_method_name": f"Test{i}",
            })
        return pd.DataFrame(rows)

    def test_five_rows_collapse_to_one(self):
        df = self._agentic_attempt("codex", "2", n_tests=5)
        result = collapse_agentic_to_attempts(df)
        assert len(result) == 1

    def test_generated_test_count_is_correct(self):
        df = self._agentic_attempt("codex", "2", n_tests=5)
        result = collapse_agentic_to_attempts(df)
        assert result["generated_test_count"].iloc[0] == 5

    def test_distinct_tool_attempt_ids_produce_two_rows(self):
        df_codex = self._agentic_attempt("codex", "2", n_tests=5)
        df2 = self._agentic_attempt("codex", "2", n_tests=6, tool_attempt_id="codex-2-repeat")
        combined = pd.concat([df_codex, df2], ignore_index=True)
        result = collapse_agentic_to_attempts(combined)
        assert len(result) == 2

    def test_attempt_level_columns_preserved(self):
        df = self._agentic_attempt("codex", "2", n_tests=5, outcome="Passed")
        result = collapse_agentic_to_attempts(df)
        assert result["tool_validation_outcome"].iloc[0] == "Passed"
        assert result["coverage_delta"].iloc[0] == pytest.approx(0.3)

    def test_empty_df_returns_empty(self):
        result = collapse_agentic_to_attempts(pd.DataFrame())
        assert result.empty

    def test_four_attempts_produce_four_rows(self):
        """Pilot data: 4 attempts (2 tools × 2 source members)."""
        frames = [
            self._agentic_attempt("codex", "2", n_tests=5),
            self._agentic_attempt("codex", "478", n_tests=5),
            self._agentic_attempt("gemini", "2", n_tests=6),
            self._agentic_attempt("gemini", "478", n_tests=5),
        ]
        combined = pd.concat(frames, ignore_index=True)
        result = collapse_agentic_to_attempts(combined)
        assert len(result) == 4

    def test_counts_per_attempt_correct(self):
        frames = [
            self._agentic_attempt("codex", "2", n_tests=5),
            self._agentic_attempt("gemini", "2", n_tests=6),
        ]
        combined = pd.concat(frames, ignore_index=True)
        result = collapse_agentic_to_attempts(combined)
        counts = dict(zip(result["tool_id"], result["generated_test_count"]))
        assert counts["codex"] == 5
        assert counts["gemini"] == 6

    def test_missing_tool_attempt_id_raises(self):
        df = self._agentic_attempt("codex", "2", n_tests=1).drop(columns=["tool_attempt_id"])
        with pytest.raises(ValueError, match="tool_attempt_id"):
            collapse_agentic_to_attempts(df)


# ---------------------------------------------------------------------------
# normalize_attempts (full pipeline)
# ---------------------------------------------------------------------------

class TestNormalizeAttempts:
    def _pilot_df(self) -> pd.DataFrame:
        """Build a small DataFrame that mimics the pilot CSV structure."""
        llm_rows = [
            _llm_row(failure_kind="Runtime", generation_attempt_id="1", attempt_number="1"),
            _llm_row(failure_kind="None", generation_attempt_id="4", attempt_number="4",
                     source_member_id="478", source_method_name="Implement",
                     coverage_delta="0.33", mutation_score_delta="0.25"),
        ]
        agentic_rows = [
            _agentic_row(tool_id="codex", source_member_id="2",
                         generated_test_method_name="TestA",
                         tool_attempt_id="101"),
            _agentic_row(tool_id="codex", source_member_id="2",
                         generated_test_method_name="TestB",
                         tool_attempt_id="101"),
            _agentic_row(tool_id="gemini", source_member_id="2",
                         tool_validation_outcome="TestsFailed",
                         generated_test_method_name="TestC",
                         tool_attempt_id="102"),
        ]
        return pd.DataFrame(llm_rows + agentic_rows)

    def test_lane_column_normalized(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert set(result["lane"].unique()).issubset({LANE_LLM, LANE_AGENTIC})

    def test_agentic_rows_collapsed(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        # 2 LLM rows + 2 agentic attempts (codex/2 and gemini/2)
        agentic = result[result["lane"] == LANE_AGENTIC]
        assert len(agentic) == 2

    def test_llm_row_count_preserved(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        llm = result[result["lane"] == LANE_LLM]
        assert len(llm) == 2

    def test_candidate_key_added(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert "candidate_key" in result.columns
        assert result["candidate_key"].notna().all()

    def test_repository_key_added(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert "repository_key" in result.columns
        assert result["repository_key"].notna().all()

    def test_validated_success_llm_row(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        llm = result[result["lane"] == LANE_LLM]
        # attempt 1: failure_kind=Runtime → False; attempt 4: failure_kind=None → True
        successes = llm[llm["validated_success"] == True]
        assert len(successes) == 1

    def test_validated_success_agentic_row(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        agentic = result[result["lane"] == LANE_AGENTIC]
        codex_row = agentic[agentic["tool_id"] == "codex"]
        assert codex_row["validated_success"].iloc[0] == True

    def test_coverage_delta_is_numeric(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert pd.api.types.is_float_dtype(result["coverage_delta"])

    def test_duration_seconds_column_exists(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert "duration_seconds" in result.columns

    def test_changed_files_count_column_exists(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        assert "changed_files_count" in result.columns

    def test_llm_generated_test_count_is_one(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        llm = result[result["lane"] == LANE_LLM]
        assert (llm["generated_test_count"] == 1).all()

    def test_agentic_generated_test_count_reflects_collapse(self):
        df = self._pilot_df()
        result = normalize_attempts(df)
        codex = result[(result["lane"] == LANE_AGENTIC) & (result["tool_id"] == "codex")]
        assert codex["generated_test_count"].iloc[0] == 2  # 2 codex rows

    def test_empty_df_returns_empty(self):
        result = normalize_attempts(pd.DataFrame())
        assert result.empty


# ---------------------------------------------------------------------------
# normalize_attempts — pilot CSV integration
# ---------------------------------------------------------------------------

class TestNormalizeAttemptsCurrentFixture:
    """Structural invariants for the current result CSV contract."""

    @pytest.fixture(scope="class")
    def normalized(self):
        raw = pd.DataFrame([
            _llm_row(failure_kind="Runtime", generation_attempt_id="1"),
            _llm_row(failure_kind="None", generation_attempt_id="2", source_member_id="478"),
            _agentic_row(tool_attempt_id="101", tool_id="codex", source_member_id="2",
                         generated_test_method_name="TestA"),
            _agentic_row(tool_attempt_id="101", tool_id="codex", source_member_id="2",
                         generated_test_method_name="TestB"),
            _agentic_row(tool_attempt_id="102", tool_id="codex", source_member_id="2",
                         generated_test_method_name="TestC"),
        ])
        return normalize_attempts(raw)

    def test_has_rows(self, normalized):
        assert len(normalized) > 0

    def test_both_lanes_present(self, normalized):
        assert LANE_LLM in normalized["lane"].values
        assert LANE_AGENTIC in normalized["lane"].values

    def test_lane_column_no_raw_values(self, normalized):
        assert "testmap" not in normalized["lane"].values
        assert "agent-tool" not in normalized["lane"].values

    def test_candidate_key_non_null(self, normalized):
        assert normalized["candidate_key"].notna().all()

    def test_duration_seconds_renamed(self, normalized):
        assert "duration_seconds" in normalized.columns
        assert "total_attempt_duration_seconds" not in normalized.columns

    def test_changed_files_count_renamed(self, normalized):
        assert "changed_files_count" in normalized.columns
        assert "tool_changed_files_count" not in normalized.columns

    def test_validated_success_column_present(self, normalized):
        assert "validated_success" in normalized.columns

    def test_llm_generated_test_count_one(self, normalized):
        llm = normalized[normalized["lane"] == LANE_LLM]
        assert (llm["generated_test_count"] == 1).all()

    def test_agentic_unique_per_tool_attempt_id(self, normalized):
        agentic = normalized[normalized["lane"] == LANE_AGENTIC]
        counts = agentic.groupby(["tool_attempt_id"]).size()
        assert (counts == 1).all()


# ---------------------------------------------------------------------------
# build_candidate_summary
# ---------------------------------------------------------------------------

class TestBuildCandidateSummary:
    def _attempts(self) -> pd.DataFrame:
        """Two LLM candidates, one with success."""
        rows = [
            # Source member 2: 3 failed attempts
            {"candidate_key": "k1", "lane": LANE_LLM, "validated_success": False,
             "coverage_delta": 0.1, "mutation_score_delta": 0.0, "generated_test_count": 1,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
            {"candidate_key": "k1", "lane": LANE_LLM, "validated_success": False,
             "coverage_delta": 0.2, "mutation_score_delta": 0.0, "generated_test_count": 1,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
            {"candidate_key": "k1", "lane": LANE_LLM, "validated_success": True,
             "coverage_delta": 0.3, "mutation_score_delta": 0.1, "generated_test_count": 1,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
            # Source member 478: all failed
            {"candidate_key": "k2", "lane": LANE_LLM, "validated_success": False,
             "coverage_delta": 0.05, "mutation_score_delta": 0.0, "generated_test_count": 1,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
            {"candidate_key": "k2", "lane": LANE_LLM, "validated_success": False,
             "coverage_delta": 0.0, "mutation_score_delta": 0.0, "generated_test_count": 1,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
        ]
        return pd.DataFrame(rows)

    def test_two_candidates_produce_two_rows(self):
        result = build_candidate_summary(self._attempts())
        assert len(result) == 2

    def test_attempt_count_correct(self):
        result = build_candidate_summary(self._attempts())
        k1 = result[result["candidate_key"] == "k1"]
        assert k1["attempt_count"].iloc[0] == 3

    def test_any_validated_success_true_when_at_least_one_succeeds(self):
        result = build_candidate_summary(self._attempts())
        k1 = result[result["candidate_key"] == "k1"]
        assert k1["any_validated_success"].iloc[0] == True  # noqa: E712

    def test_any_validated_success_false_when_none_succeed(self):
        result = build_candidate_summary(self._attempts())
        k2 = result[result["candidate_key"] == "k2"]
        assert k2["any_validated_success"].iloc[0] == False  # noqa: E712

    def test_best_coverage_delta_is_max(self):
        result = build_candidate_summary(self._attempts())
        k1 = result[result["candidate_key"] == "k1"]
        assert k1["best_coverage_delta"].iloc[0] == pytest.approx(0.3)

    def test_best_attempt_is_a_success_row(self):
        result = build_candidate_summary(self._attempts())
        k1 = result[result["candidate_key"] == "k1"]
        # The best row is picked from successes first → validated_success == True
        assert k1["validated_success"].iloc[0] == True  # noqa: E712

    def test_total_generated_tests(self):
        result = build_candidate_summary(self._attempts())
        k1 = result[result["candidate_key"] == "k1"]
        assert k1["total_generated_tests"].iloc[0] == 3  # 3 LLM attempts = 3 tests

    def test_empty_df_returns_empty(self):
        result = build_candidate_summary(pd.DataFrame())
        assert result.empty

    def test_missing_group_columns_returns_empty(self):
        df = pd.DataFrame([{"validated_success": True}])
        result = build_candidate_summary(df)
        assert result.empty

    def test_agentic_candidates_grouped_by_lane(self):
        """Agentic attempts from different tools roll up into one candidate row."""
        rows = [
            {"candidate_key": "k1", "lane": LANE_AGENTIC, "tool_id": "codex",
             "validated_success": True, "coverage_delta": 0.35,
             "mutation_score_delta": 0.1, "generated_test_count": 5,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
            {"candidate_key": "k1", "lane": LANE_AGENTIC, "tool_id": "gemini",
             "validated_success": False, "coverage_delta": 0.0,
             "mutation_score_delta": 0.0, "generated_test_count": 6,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c", "repository_key": "rk"},
        ]
        result = build_candidate_summary(pd.DataFrame(rows))
        assert len(result) == 1
        assert result["any_validated_success"].iloc[0] == True  # noqa: E712
        assert result["total_generated_tests"].iloc[0] == 11


# ---------------------------------------------------------------------------
# build_repository_summary
# ---------------------------------------------------------------------------

class TestBuildRepositorySummary:
    def _candidates(self) -> pd.DataFrame:
        return pd.DataFrame([
            {"repository_key": "rk1", "lane": LANE_LLM,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c",
             "any_validated_success": True, "best_coverage_delta": 0.3,
             "best_mutation_delta": 0.1, "total_generated_tests": 3},
            {"repository_key": "rk1", "lane": LANE_LLM,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c",
             "any_validated_success": False, "best_coverage_delta": 0.05,
             "best_mutation_delta": 0.0, "total_generated_tests": 2},
            {"repository_key": "rk1", "lane": LANE_AGENTIC,
             "repo_owner": "o", "repo_name": "r", "commit_hash": "c",
             "any_validated_success": True, "best_coverage_delta": 0.35,
             "best_mutation_delta": 0.2, "total_generated_tests": 11},
        ])

    def test_two_lanes_produce_two_rows(self):
        result = build_repository_summary(self._candidates())
        assert len(result) == 2

    def test_candidate_count(self):
        result = build_repository_summary(self._candidates())
        llm_row = result[result["lane"] == LANE_LLM].iloc[0]
        assert llm_row["candidate_count"] == 2

    def test_validated_success_count(self):
        result = build_repository_summary(self._candidates())
        llm_row = result[result["lane"] == LANE_LLM].iloc[0]
        assert llm_row["validated_success_count"] == 1

    def test_validated_success_rate(self):
        result = build_repository_summary(self._candidates())
        llm_row = result[result["lane"] == LANE_LLM].iloc[0]
        assert llm_row["validated_success_rate"] == pytest.approx(0.5)

    def test_mean_coverage_delta(self):
        result = build_repository_summary(self._candidates())
        llm_row = result[result["lane"] == LANE_LLM].iloc[0]
        assert llm_row["mean_coverage_delta"] == pytest.approx(0.175)

    def test_empty_df_returns_empty(self):
        result = build_repository_summary(pd.DataFrame())
        assert result.empty


# ---------------------------------------------------------------------------
# build_paired_comparison
# ---------------------------------------------------------------------------

class TestBuildPairedComparison:
    def _candidates(self, llm_success: bool, agentic_success: bool) -> pd.DataFrame:
        return pd.DataFrame([
            {"candidate_key": "k1", "lane": LANE_LLM,
             "any_validated_success": llm_success,
             "attempt_count": 3,
             "best_coverage_delta": 0.3 if llm_success else 0.05,
             "best_mutation_delta": 0.1 if llm_success else 0.0},
            {"candidate_key": "k1", "lane": LANE_AGENTIC,
             "any_validated_success": agentic_success,
             "attempt_count": 2,
             "best_coverage_delta": 0.35 if agentic_success else 0.0,
             "best_mutation_delta": 0.2 if agentic_success else 0.0},
        ])

    def test_tie_success(self):
        result = build_paired_comparison(self._candidates(True, True))
        assert result["winner"].iloc[0] == "tie_success"

    def test_tie_failure(self):
        result = build_paired_comparison(self._candidates(False, False))
        assert result["winner"].iloc[0] == "tie_failure"

    def test_llm_won(self):
        result = build_paired_comparison(self._candidates(True, False))
        assert result["winner"].iloc[0] == "llm_won"

    def test_agentic_won(self):
        result = build_paired_comparison(self._candidates(False, True))
        assert result["winner"].iloc[0] == "agentic_won"

    def test_llm_only(self):
        df = pd.DataFrame([{
            "candidate_key": "k1", "lane": LANE_LLM,
            "any_validated_success": True, "attempt_count": 1,
            "best_coverage_delta": 0.3, "best_mutation_delta": 0.1,
        }])
        result = build_paired_comparison(df)
        assert result["winner"].iloc[0] == "llm_only"

    def test_agentic_only(self):
        df = pd.DataFrame([{
            "candidate_key": "k1", "lane": LANE_AGENTIC,
            "any_validated_success": True, "attempt_count": 1,
            "best_coverage_delta": 0.35, "best_mutation_delta": 0.2,
        }])
        result = build_paired_comparison(df)
        assert result["winner"].iloc[0] == "agentic_only"

    def test_winner_is_valid_label(self):
        from analysis.schema import WINNER_LABELS
        result = build_paired_comparison(self._candidates(True, False))
        assert result["winner"].iloc[0] in WINNER_LABELS

    def test_empty_df_returns_empty(self):
        result = build_paired_comparison(pd.DataFrame())
        assert result.empty

    def test_multiple_candidates(self):
        df = pd.DataFrame([
            {"candidate_key": "k1", "lane": LANE_LLM, "any_validated_success": True,
             "attempt_count": 3, "best_coverage_delta": 0.3, "best_mutation_delta": 0.1},
            {"candidate_key": "k1", "lane": LANE_AGENTIC, "any_validated_success": False,
             "attempt_count": 2, "best_coverage_delta": 0.0, "best_mutation_delta": 0.0},
            {"candidate_key": "k2", "lane": LANE_LLM, "any_validated_success": False,
             "attempt_count": 5, "best_coverage_delta": 0.1, "best_mutation_delta": 0.0},
            {"candidate_key": "k2", "lane": LANE_AGENTIC, "any_validated_success": True,
             "attempt_count": 2, "best_coverage_delta": 0.35, "best_mutation_delta": 0.2},
        ])
        result = build_paired_comparison(df)
        assert len(result) == 2
        winners = dict(zip(result["candidate_key"], result["winner"]))
        assert winners["k1"] == "llm_won"
        assert winners["k2"] == "agentic_won"

    def test_coverage_deltas_populated(self):
        result = build_paired_comparison(self._candidates(True, True))
        assert result["llm_best_coverage_delta"].iloc[0] == pytest.approx(0.3)
        assert result["agentic_best_coverage_delta"].iloc[0] == pytest.approx(0.35)
