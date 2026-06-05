"""Column definitions and shared constants for evaluation datasets.

All dataset-building code imports field lists from here so column names
stay consistent across scripts and notebooks.
"""

from __future__ import annotations

# ---------------------------------------------------------------------------
# Lane constants
# ---------------------------------------------------------------------------

LANE_LLM = "llm"
LANE_AGENTIC = "agentic"
LANE_VALUES = [LANE_LLM, LANE_AGENTIC]

# ---------------------------------------------------------------------------
# Shared attempt-level fields (present in both lanes after normalization)
# ---------------------------------------------------------------------------

SHARED_ATTEMPT_FIELDS: list[str] = [
    "lane",
    "repo_owner",
    "repo_name",
    "repo_url",
    "commit_hash",
    "project_path",
    "source_member_id",
    "source_method_name",
    "source_method_signature",
    "attempt_id",
    "candidate_key",
    "repository_key",
    "producer_id",
    "model",
    "provider",
    "tool_id",
    "validated_success",
    "produced_change",
    "generated_test_count",
    "coverage_before",
    "coverage_after",
    "coverage_delta",
    "mutation_score_before",
    "mutation_score_after",
    "mutation_score_delta",
    "mutant_killed",
    "duration_seconds",
    "total_tokens",
    "changed_files_count",
    "test_files_changed",
    "production_files_changed",
    "project_files_changed",
    "deleted_files_count",
    "failure_kind",
    "failure_stage",
    "failure_category",
    "validation_outcome",
    "observed_outcome",
]

# Fields that uniquely identify a candidate across lanes
CANDIDATE_KEY_FIELDS: list[str] = [
    "repo_owner",
    "repo_name",
    "commit_hash",
    "source_member_id",
    "project_path",
]

# Fields that uniquely identify a repository slice
REPOSITORY_KEY_FIELDS: list[str] = [
    "repo_owner",
    "repo_name",
    "commit_hash",
]

# ---------------------------------------------------------------------------
# LLM-lane specific fields
# ---------------------------------------------------------------------------

LLM_SPECIFIC_FIELDS: list[str] = [
    "compile_success",
    "execution_success",
    "pass_rate",
    "acceptance_rate",
    "repair_attempt_count",
    "repair_recovered",
    "roslyn_diagnostic_count",
    "roslyn_regression",
    "generation_step",
    "prompt_tokens",
    "completion_tokens",
]

# ---------------------------------------------------------------------------
# Agentic-lane specific fields
# ---------------------------------------------------------------------------

AGENTIC_SPECIFIC_FIELDS: list[str] = [
    "run_status",
    "tool_run_status",
    "tool_validation_outcome",
    "tool_observed_outcome",
    "constraint_violation_summary",
    "jsonl_log_available",
    "usage_available",
    "usage_source",
    "input_tokens",
    "output_tokens",
    "estimated_prompt_tokens",
    "targeted_baseline_id",
    "post_attempt_test_run_id",
    "notes",
]

# ---------------------------------------------------------------------------
# Preliminary failure labels (agentic + LLM)
# ---------------------------------------------------------------------------

PRELIMINARY_FAILURE_LABELS: list[str] = [
    "no_change",
    "timeout",
    "tool_crash",
    "build_failed",
    "compile_failed",
    "test_failed",
    "assertion_failed",
    "runtime_exception",
    "roslyn_regression",
    "invalid_test_target",
    "missing_context",
    "bad_mock_or_setup",
    "accessibility_issue",
    "dependency_or_environment_issue",
    "low_impact",
    "overbroad_change",
    "production_code_modified",
    "project_file_modified",
    "unclassified",
]

# ---------------------------------------------------------------------------
# Paired comparison winner labels
# ---------------------------------------------------------------------------

WINNER_LABELS: list[str] = [
    "llm_won",
    "agentic_won",
    "tie_success",
    "tie_failure",
    "llm_only",
    "agentic_only",
    "no_comparable_result",
]

# ---------------------------------------------------------------------------
# Training export feature fields
# ---------------------------------------------------------------------------

TRAINING_MAPPING_FIELDS: list[str] = [
    "repo_owner",
    "repo_name",
    "commit_hash",
    "project_id",
    "source_member_id",
    "source_method_name",
    "source_method_signature",
    "source_file_path",
    "source_line",
    "source_visibility",
    "source_complexity",
    "source_coverage",
    "source_covered_lines",
    "source_total_lines",
    "coverage_gap_count",
    "test_member_id",
    "test_method_name",
    "test_file_path",
    "mapping_evidence_kind",
    "mapping_is_grounded",
    "mapping_confidence",
    "access_path_strategy",
    "path_length",
    "test_smell_count",
    "test_smell_ids",
    "mutation_score",
    "survived_mutant_count",
    "killed_mutant_count",
    "candidate_risk_score",
    "metric_driven_score",
    "test_state",
    "recommended_action",
    "generation_succeeded",
    "coverage_improved",
    "mutation_improved",
    "accepted_generated_test",
]

TRAINING_LABELS: list[str] = [
    "has_mapped_test",
    "is_grounded_mapping",
    "generation_succeeded",
    "validated_success",
    "coverage_improved",
    "mutation_improved",
    "mutant_killed",
    "accepted_generated_test",
]

# ---------------------------------------------------------------------------
# Failure case export fields
# ---------------------------------------------------------------------------

FAILURE_CASE_FIELDS: list[str] = [
    "failure_case_id",
    "repo_owner",
    "repo_name",
    "repo_url",
    "commit_hash",
    "project_path",
    "source_member_id",
    "source_method_name",
    "source_method_signature",
    "source_file_path",
    "source_line",
    "lane",
    "producer_id",
    "model",
    "provider",
    "tool_id",
    "attempt_id",
    "generation_attempt_id",
    "tool_attempt_id",
    "test_execution_id",
    "run_date",
    "failure_kind",
    "failure_stage",
    "failure_category",
    "validation_outcome",
    "observed_outcome",
    "preliminary_failure_label",
    "outcome_summary",
    "changed_files_count",
    "test_files_changed",
    "production_files_changed",
    "project_files_changed",
    "deleted_files_count",
    "logs_excerpt",
    "prompt_excerpt",
    "response_excerpt",
    "generated_code_excerpt",
    "diagnostics_excerpt",
    "artifact_path",
    "raw_log_path",
    "case_markdown_path",
    "qualitative_notes",
    "open_code_1",
    "open_code_2",
    "open_code_3",
    "coder",
    "coded_at",
]
