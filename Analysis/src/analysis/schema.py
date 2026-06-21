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
# Raw CSV column → canonical column renames
# Applied once at load time before any other normalization.
# ---------------------------------------------------------------------------

RAW_COLUMN_RENAMES: dict[str, str] = {
    "producer_lane": "lane",
    # Duration column (name changed between CSV versions; map both)
    "total_duration_seconds": "duration_seconds",
    "total_attempt_duration_seconds": "duration_seconds",
    "tool_changed_files_count": "changed_files_count",
    "tool_production_files_changed": "production_files_changed",
    "tool_test_files_changed": "test_files_changed",
    "tool_project_files_changed": "project_files_changed",
    "tool_deleted_files_count": "deleted_files_count",
}

# ---------------------------------------------------------------------------
# Agentic attempt key fields
# The raw CSV has one row per generated test for agentic attempts.
# These fields identify one logical tool invocation (one attempt).
# ---------------------------------------------------------------------------

AGENTIC_ATTEMPT_KEY_FIELDS: list[str] = [
    "tool_attempt_id",
]

# ---------------------------------------------------------------------------
# Shared attempt-level fields (present in both lanes after normalization)
# ---------------------------------------------------------------------------

SHARED_ATTEMPT_FIELDS: list[str] = [
    "lane",
    "repo_owner",
    "repo_name",
    "repo_url",
    "commit_hash",
    "source_member_id",
    "source_method_name",
    "source_method_signature",
    "attempt_id",
    "tool_attempt_id",
    "candidate_key",
    "repository_key",
    "producer_id",
    "model",
    "provider",
    "tool_id",
    # Outcomes — outcome_classification is the authoritative label; validated_success is derived
    "outcome_classification",
    "validated_success",
    "validated_evidence_positive",
    "validated_low_impact",
    "metric_improved",
    "positive_impact",
    "impact_attribution",
    "produced_change",
    "generated_test_count",
    "generated_test_compiled",
    "generated_test_executed",
    "generated_test_passed",
    "coverage_before",
    "coverage_after",
    "coverage_delta",
    "mutation_score_before",
    "mutation_score_after",
    "mutation_score_delta",
    "mutant_killed",
    "failure_kind",
    "failure_stage",
    "failure_category",
    "failure_summary",
    # Source method characteristics
    "source_method_baseline_coverage",
    "source_method_complexity",
    "source_method_mi",
    "source_method_cc",
    "source_method_coupling",
    "source_method_dit",
    "source_method_sloc",
    "source_method_eloc",
    # Baseline test characteristics
    "baseline_test_state",
    "baseline_test_method",
    "baseline_test_mi",
    "baseline_test_cc",
    "baseline_test_coupling",
    "baseline_test_dit",
    "baseline_test_sloc",
    "baseline_test_eloc",
    "baseline_test_smells",
    "baseline_test_smell_count",
    "baseline_test_smell_types",
    # Generated test characteristics
    "generated_test_method_name",
    "generated_test_mi",
    "generated_test_cc",
    "generated_test_coupling",
    "generated_test_dit",
    "generated_test_sloc",
    "generated_test_eloc",
    "generated_test_smells",
    "generated_test_smell_count",
    "generated_test_smell_types",
    # Cost and timing
    "duration_seconds",
    "generation_duration_seconds",
    "validation_duration_seconds",
    "total_tokens",
    "cumulative_tokens",
    "changed_files_count",
    "test_files_changed",
    "production_files_changed",
    "project_files_changed",
    "deleted_files_count",
    "baseline_test_execution_time_ms",
    "generated_test_execution_time_ms",
]

# Fields that uniquely identify a candidate across lanes
CANDIDATE_KEY_FIELDS: list[str] = [
    "repo_owner",
    "repo_name",
    "commit_hash",
    "source_member_id",
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
    "attempt_number",
    "repair_attempt_number",
    "context_mode",
    "budget_mode",
    "ablation_variant_id",
    "steps_included",
    "source_member_visibility",
    "access_strategy",
    "access_path_member_ids",
    "test_mapping_count",
    "setup_binding_count",
    "candidate_test_intentions_summary",
    "candidate_type_construction_summary",
    "candidate_metadata_json",
    "roslyn_validation_succeeded",
    "roslyn_validation_skipped",
    "roslyn_diagnostics_before_raw_count",
    "roslyn_diagnostics_after_raw_count",
    "new_actionable_roslyn_diagnostics_count",
    "new_roslyn_diagnostics",
    "prompt_version",
]

# ---------------------------------------------------------------------------
# Agentic-lane specific fields
# ---------------------------------------------------------------------------

AGENTIC_SPECIFIC_FIELDS: list[str] = [
    "tool_run_status",
    "tool_validation_outcome",
    "tool_artifact_path",
    "tool_attempt_targeted_baseline_id",
    "tool_post_attempt_test_run_id",
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
    "modified_file_path",
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
