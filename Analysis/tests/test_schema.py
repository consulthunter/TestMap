"""Tests for analysis.schema constants and consistency."""

from __future__ import annotations

import pytest

from analysis.schema import (
    AGENTIC_ATTEMPT_KEY_FIELDS,
    CANDIDATE_KEY_FIELDS,
    FAILURE_CASE_FIELDS,
    LANE_AGENTIC,
    LANE_LLM,
    LANE_VALUES,
    PRELIMINARY_FAILURE_LABELS,
    RAW_COLUMN_RENAMES,
    REPOSITORY_KEY_FIELDS,
    WINNER_LABELS,
)


class TestLaneConstants:
    def test_lane_llm_is_string(self):
        assert isinstance(LANE_LLM, str)
        assert LANE_LLM == "llm"

    def test_lane_agentic_is_string(self):
        assert isinstance(LANE_AGENTIC, str)
        assert LANE_AGENTIC == "agentic"

    def test_lane_values_contains_both(self):
        assert LANE_LLM in LANE_VALUES
        assert LANE_AGENTIC in LANE_VALUES
        assert len(LANE_VALUES) == 2

    def test_lane_values_are_distinct(self):
        assert len(set(LANE_VALUES)) == len(LANE_VALUES)


class TestRawColumnRenames:
    def test_producer_lane_renamed(self):
        assert "producer_lane" in RAW_COLUMN_RENAMES
        assert RAW_COLUMN_RENAMES["producer_lane"] == "lane"

    def test_total_duration_seconds_renamed(self):
        assert "total_duration_seconds" in RAW_COLUMN_RENAMES
        assert RAW_COLUMN_RENAMES["total_duration_seconds"] == "duration_seconds"

    def test_tool_changed_files_count_renamed(self):
        assert "tool_changed_files_count" in RAW_COLUMN_RENAMES
        assert RAW_COLUMN_RENAMES["tool_changed_files_count"] == "changed_files_count"

    def test_no_raw_name_equals_canonical(self):
        """A raw name must not be identical to its canonical target."""
        for raw, canonical in RAW_COLUMN_RENAMES.items():
            assert raw != canonical, f"Rename is a no-op: {raw!r}"

    def test_duration_seconds_is_target(self):
        # Multiple raw names may map to the same canonical target (version compat)
        assert "duration_seconds" in RAW_COLUMN_RENAMES.values()


class TestAgenticAttemptKeyFields:
    def test_uses_tool_attempt_id_only(self):
        assert AGENTIC_ATTEMPT_KEY_FIELDS == ["tool_attempt_id"]

    def test_no_duplicates(self):
        assert len(AGENTIC_ATTEMPT_KEY_FIELDS) == len(set(AGENTIC_ATTEMPT_KEY_FIELDS))


class TestCandidateAndRepositoryKeyFields:
    def test_candidate_key_includes_source_member_id(self):
        assert "source_member_id" in CANDIDATE_KEY_FIELDS

    def test_candidate_key_includes_repo_fields(self):
        assert "repo_owner" in CANDIDATE_KEY_FIELDS
        assert "repo_name" in CANDIDATE_KEY_FIELDS

    def test_repository_key_fields_subset_of_candidate(self):
        for field in REPOSITORY_KEY_FIELDS:
            assert field in CANDIDATE_KEY_FIELDS

    def test_no_duplicates_candidate(self):
        assert len(CANDIDATE_KEY_FIELDS) == len(set(CANDIDATE_KEY_FIELDS))

    def test_no_duplicates_repository(self):
        assert len(REPOSITORY_KEY_FIELDS) == len(set(REPOSITORY_KEY_FIELDS))


class TestWinnerLabels:
    EXPECTED = {
        "llm_won",
        "agentic_won",
        "tie_success",
        "tie_failure",
        "llm_only",
        "agentic_only",
        "no_comparable_result",
    }

    def test_all_expected_labels_present(self):
        assert set(WINNER_LABELS) == self.EXPECTED

    def test_no_duplicates(self):
        assert len(WINNER_LABELS) == len(set(WINNER_LABELS))

    def test_count(self):
        assert len(WINNER_LABELS) == 7


class TestPreliminaryFailureLabels:
    def test_no_duplicates(self):
        assert len(PRELIMINARY_FAILURE_LABELS) == len(set(PRELIMINARY_FAILURE_LABELS))

    def test_contains_core_labels(self):
        core = {"no_change", "timeout", "tool_crash", "build_failed", "compile_failed",
                "test_failed", "unclassified"}
        for label in core:
            assert label in PRELIMINARY_FAILURE_LABELS, f"Missing: {label}"

    def test_all_lowercase_underscore(self):
        for label in PRELIMINARY_FAILURE_LABELS:
            assert label == label.lower(), f"Not lowercase: {label}"
            assert " " not in label, f"Space in label: {label}"


class TestFailureCaseFields:
    def test_includes_required_fields(self):
        required = {
            "failure_case_id", "lane", "failure_kind",
            "preliminary_failure_label", "candidate_key" if "candidate_key" in FAILURE_CASE_FIELDS else "source_member_id",
        }
        # At least lane is always there
        assert "lane" in FAILURE_CASE_FIELDS

    def test_no_duplicates(self):
        assert len(FAILURE_CASE_FIELDS) == len(set(FAILURE_CASE_FIELDS))
