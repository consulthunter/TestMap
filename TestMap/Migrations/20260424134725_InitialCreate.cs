using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_method_risk_scores",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: true),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    risk_score = table.Column<double>(type: "REAL", nullable: false),
                    factor_scores = table.Column<string>(type: "TEXT", nullable: false),
                    weights = table.Column<string>(type: "TEXT", nullable: false),
                    selection_reason = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_method_risk_scores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "code_metrics",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    entity_id = table.Column<int>(type: "INTEGER", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", nullable: false),
                    maintainability_index = table.Column<int>(type: "INTEGER", nullable: false),
                    cyclomatic_complexity = table.Column<int>(type: "INTEGER", nullable: false),
                    class_coupling = table.Column<int>(type: "INTEGER", nullable: false),
                    depth_of_inheritance = table.Column<int>(type: "INTEGER", nullable: false),
                    source_lines_of_code = table.Column<int>(type: "INTEGER", nullable: false),
                    executable_lines_of_code = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coverage_gaps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    coverage_report_id = table.Column<int>(type: "INTEGER", nullable: false),
                    line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    hits = table.Column<int>(type: "INTEGER", nullable: false),
                    is_branch = table.Column<bool>(type: "INTEGER", nullable: false),
                    condition_coverage = table.Column<string>(type: "TEXT", nullable: false),
                    gap_kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    source_text = table.Column<string>(type: "TEXT", nullable: false),
                    member_content_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coverage_gaps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "coverage_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    line_rate = table.Column<double>(type: "REAL", nullable: false),
                    branch_rate = table.Column<double>(type: "REAL", nullable: false),
                    complexity = table.Column<double>(type: "REAL", nullable: false),
                    version = table.Column<string>(type: "TEXT", nullable: false),
                    timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    lines_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    lines_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coverage_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "csharp_projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    solution_id = table.Column<int>(type: "INTEGER", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    build_targets = table.Column<string>(type: "TEXT", nullable: false),
                    default_build_target = table.Column<string>(type: "TEXT", nullable: false),
                    build_metadata = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_csharp_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "csharp_solutions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_csharp_solutions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "experiment_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    configuration = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    candidate_limit = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    csharp_project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    using_statements = table.Column<string>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flaky_test_rerun_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    test_execution_result_id = table.Column<int>(type: "INTEGER", nullable: false),
                    attempt_number = table.Column<int>(type: "INTEGER", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    duration_ms = table.Column<double>(type: "REAL", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flaky_test_rerun_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "flaky_test_scores",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    test_name = table.Column<string>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    flakiness_score = table.Column<double>(type: "REAL", nullable: false),
                    classification = table.Column<string>(type: "TEXT", nullable: false),
                    factor_scores = table.Column<string>(type: "TEXT", nullable: false),
                    weights = table.Column<string>(type: "TEXT", nullable: false),
                    evidence = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flaky_test_scores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invocations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    invoked_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    is_assertion = table.Column<bool>(type: "INTEGER", nullable: false),
                    full_string = table.Column<string>(type: "TEXT", nullable: false),
                    location = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_coverages",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    coverage_report_id = table.Column<int>(type: "INTEGER", nullable: false),
                    line_rate = table.Column<double>(type: "REAL", nullable: false),
                    branch_rate = table.Column<double>(type: "REAL", nullable: false),
                    lines_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    lines_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    complexity = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_coverages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MemberRelationships",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationshipType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRelationships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    object_id = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    modifiers = table.Column<string>(type: "TEXT", nullable: false),
                    attributes = table.Column<string>(type: "TEXT", nullable: false),
                    doc_string = table.Column<string>(type: "TEXT", nullable: false),
                    full_string = table.Column<string>(type: "TEXT", nullable: false),
                    is_test_member = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_generated = table.Column<bool>(type: "INTEGER", nullable: false),
                    test_categories = table.Column<string>(type: "TEXT", nullable: false),
                    test_intent = table.Column<string>(type: "TEXT", nullable: false),
                    test_metadata_source = table.Column<string>(type: "TEXT", nullable: false),
                    test_metadata_confidence = table.Column<double>(type: "REAL", nullable: true),
                    test_metadata_prompt_version = table.Column<string>(type: "TEXT", nullable: false),
                    start_line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    end_line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    body_start_position = table.Column<int>(type: "INTEGER", nullable: false),
                    body_end_position = table.Column<int>(type: "INTEGER", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mutant_survived_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    mutant_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    stryker_test_id = table.Column<string>(type: "TEXT", nullable: false),
                    test_name = table.Column<string>(type: "TEXT", nullable: false),
                    test_file_path = table.Column<string>(type: "TEXT", nullable: false),
                    location = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mutant_survived_tests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mutants",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    mutation_testing_report_id = table.Column<int>(type: "INTEGER", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    stryker_mutant_id = table.Column<string>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    mutator_name = table.Column<string>(type: "TEXT", nullable: false),
                    replacement = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    status_reason = table.Column<string>(type: "TEXT", nullable: false),
                    is_static = table.Column<bool>(type: "INTEGER", nullable: false),
                    location = table.Column<string>(type: "TEXT", nullable: false),
                    covered_by = table.Column<string>(type: "TEXT", nullable: false),
                    killed_by = table.Column<string>(type: "TEXT", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mutants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mutation_testing_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    schema_version = table.Column<string>(type: "TEXT", nullable: false),
                    project_root = table.Column<string>(type: "TEXT", nullable: false),
                    mutation_score = table.Column<double>(type: "REAL", nullable: false),
                    files = table.Column<string>(type: "TEXT", nullable: false),
                    test_files = table.Column<string>(type: "TEXT", nullable: false),
                    thresholds = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mutation_testing_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "object_coverages",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    object_id = table.Column<int>(type: "INTEGER", nullable: false),
                    coverage_report_id = table.Column<int>(type: "INTEGER", nullable: false),
                    line_rate = table.Column<double>(type: "REAL", nullable: false),
                    branch_rate = table.Column<double>(type: "REAL", nullable: false),
                    lines_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    lines_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    branches_valid = table.Column<int>(type: "INTEGER", nullable: false),
                    complexity = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_object_coverages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ObjectRelationships",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelationshipType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectRelationships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    file_id = table.Column<int>(type: "INTEGER", nullable: false),
                    @namespace = table.Column<string>(name: "namespace", type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    modifiers = table.Column<string>(type: "TEXT", nullable: false),
                    attributes = table.Column<string>(type: "TEXT", nullable: false),
                    doc_string = table.Column<string>(type: "TEXT", nullable: false),
                    full_string = table.Column<string>(type: "TEXT", nullable: false),
                    is_test_object = table.Column<bool>(type: "INTEGER", nullable: false),
                    test_framework = table.Column<string>(type: "TEXT", nullable: false),
                    start_line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    end_line_number = table.Column<int>(type: "INTEGER", nullable: false),
                    body_start_position = table.Column<int>(type: "INTEGER", nullable: false),
                    body_end_position = table.Column<int>(type: "INTEGER", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_objects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    owner = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    repo_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    directory_path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    web_url = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    database_path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    branch = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    last_analyzed_commit = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    content_hash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_execution_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    solution_path = table.Column<string>(type: "TEXT", nullable: false),
                    project_path = table.Column<string>(type: "TEXT", nullable: false),
                    test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    test_name = table.Column<string>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    target_framework = table.Column<string>(type: "TEXT", nullable: false),
                    execution_context = table.Column<string>(type: "TEXT", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    duration_ms = table.Column<double>(type: "REAL", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    error_stack_trace = table.Column<string>(type: "TEXT", nullable: true),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_execution_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    test_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    run_date = table.Column<string>(type: "TEXT", nullable: false),
                    method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_name = table.Column<string>(type: "TEXT", nullable: false),
                    outcome = table.Column<string>(type: "TEXT", nullable: false),
                    duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    stack_trace = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_runs",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    run_id = table.Column<string>(type: "TEXT", nullable: false),
                    run_date = table.Column<string>(type: "TEXT", nullable: false),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    coverage = table.Column<int>(type: "INTEGER", nullable: false),
                    mutation_score = table.Column<double>(type: "REAL", nullable: true),
                    log_path = table.Column<string>(type: "TEXT", nullable: false),
                    failure_analysis = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "test_smells",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    object_id = table.Column<int>(type: "INTEGER", nullable: true),
                    smell_id = table.Column<string>(type: "TEXT", nullable: false),
                    smell_name = table.Column<string>(type: "TEXT", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    line = table.Column<int>(type: "INTEGER", nullable: true),
                    column = table.Column<int>(type: "INTEGER", nullable: true),
                    containing_type_name = table.Column<string>(type: "TEXT", nullable: false),
                    test_method_name = table.Column<string>(type: "TEXT", nullable: false),
                    analyzed_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_smells", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_methods",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    existing_test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    source_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    source_method_signature = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    existing_test_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    initial_coverage = table.Column<double>(type: "REAL", nullable: false),
                    initial_covered_lines = table.Column<int>(type: "INTEGER", nullable: false),
                    initial_total_lines = table.Column<int>(type: "INTEGER", nullable: false),
                    metric_driven_score = table.Column<double>(type: "REAL", nullable: true),
                    expected_metric_delta = table.Column<double>(type: "REAL", nullable: true),
                    metric_confidence = table.Column<double>(type: "REAL", nullable: true),
                    metric_feasibility = table.Column<double>(type: "REAL", nullable: true),
                    metric_estimated_cost = table.Column<double>(type: "REAL", nullable: true),
                    metric_guardrail_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    metric_selection_reason = table.Column<string>(type: "TEXT", nullable: false),
                    test_improvement_score = table.Column<double>(type: "REAL", nullable: true),
                    test_improvement_reason = table.Column<string>(type: "TEXT", nullable: false),
                    test_state = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    recommended_action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    test_state_reason = table.Column<string>(type: "TEXT", nullable: false),
                    selection_time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_methods_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    strategy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    attempt_number = table.Column<int>(type: "INTEGER", nullable: false),
                    is_repair_attempt = table.Column<bool>(type: "INTEGER", nullable: false),
                    parent_attempt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    total_tokens_used = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failure_kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failure_stage = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failure_category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_generation_attempts_candidate_methods_candidate_method_id",
                        column: x => x.candidate_method_id,
                        principalTable: "candidate_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_generation_attempts_generation_attempts_parent_attempt_id",
                        column: x => x.parent_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "generation_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    generation_attempt_id = table.Column<int>(type: "INTEGER", nullable: false),
                    step_name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    prompt = table.Column<string>(type: "TEXT", nullable: false),
                    response = table.Column<string>(type: "TEXT", nullable: false),
                    tokens_used = table.Column<int>(type: "INTEGER", nullable: false),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: false),
                    validation_result = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_generation_steps_generation_attempts_generation_attempt_id",
                        column: x => x.generation_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "test_executions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    generation_attempt_id = table.Column<int>(type: "INTEGER", nullable: false),
                    generated_test_code = table.Column<string>(type: "TEXT", nullable: false),
                    generated_test_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    compilation_succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    compilation_errors = table.Column<string>(type: "TEXT", nullable: false),
                    test_passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    runtime_errors = table.Column<string>(type: "TEXT", nullable: false),
                    assertion_errors = table.Column<string>(type: "TEXT", nullable: false),
                    execution_time_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    final_coverage = table.Column<double>(type: "REAL", nullable: false),
                    final_covered_lines = table.Column<int>(type: "INTEGER", nullable: false),
                    final_total_lines = table.Column<int>(type: "INTEGER", nullable: false),
                    coverage_delta = table.Column<double>(type: "REAL", nullable: false),
                    baseline_mutation_score = table.Column<double>(type: "REAL", nullable: true),
                    mutation_score_after = table.Column<double>(type: "REAL", nullable: true),
                    mutation_score_delta = table.Column<double>(type: "REAL", nullable: true),
                    new_lines_covered = table.Column<int>(type: "INTEGER", nullable: false),
                    test_classification = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    execution_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    structured_errors = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_test_executions_generation_attempts_generation_attempt_id",
                        column: x => x.generation_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_method_risk_scores_candidate_method_id",
                table: "candidate_method_risk_scores",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_method_risk_scores_member_id",
                table: "candidate_method_risk_scores",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_methods_experiment_run_id",
                table: "candidate_methods",
                column: "experiment_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_coverage_gaps_coverage_report_id_member_id_line_number_gap_kind",
                table: "coverage_gaps",
                columns: new[] { "coverage_report_id", "member_id", "line_number", "gap_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_rerun_results_run_id",
                table: "flaky_test_rerun_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_rerun_results_test_execution_result_id",
                table: "flaky_test_rerun_results",
                column: "test_execution_result_id");

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_scores_run_id",
                table: "flaky_test_scores",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_scores_test_member_id",
                table: "flaky_test_scores",
                column: "test_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_scores_test_name_file_path",
                table: "flaky_test_scores",
                columns: new[] { "test_name", "file_path" });

            migrationBuilder.CreateIndex(
                name: "IX_generation_attempts_candidate_method_id",
                table: "generation_attempts",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_generation_attempts_parent_attempt_id",
                table: "generation_attempts",
                column: "parent_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_generation_steps_generation_attempt_id",
                table: "generation_steps",
                column: "generation_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_mutant_survived_tests_content_hash",
                table: "mutant_survived_tests",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mutants_content_hash",
                table: "mutants",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_execution_results_run_id",
                table: "test_execution_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_execution_results_test_member_id",
                table: "test_execution_results",
                column: "test_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_execution_results_test_name_file_path",
                table: "test_execution_results",
                columns: new[] { "test_name", "file_path" });

            migrationBuilder.CreateIndex(
                name: "IX_test_executions_generation_attempt_id",
                table: "test_executions",
                column: "generation_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_test_smells_member_id",
                table: "test_smells",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_smells_object_id",
                table: "test_smells",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_smells_project_id",
                table: "test_smells",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_smells_project_id_member_id_smell_id_file_path_line_column",
                table: "test_smells",
                columns: new[] { "project_id", "member_id", "smell_id", "file_path", "line", "column" });

            migrationBuilder.CreateIndex(
                name: "IX_test_smells_smell_id",
                table: "test_smells",
                column: "smell_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_method_risk_scores");

            migrationBuilder.DropTable(
                name: "code_metrics");

            migrationBuilder.DropTable(
                name: "coverage_gaps");

            migrationBuilder.DropTable(
                name: "coverage_reports");

            migrationBuilder.DropTable(
                name: "csharp_projects");

            migrationBuilder.DropTable(
                name: "csharp_solutions");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "flaky_test_rerun_results");

            migrationBuilder.DropTable(
                name: "flaky_test_scores");

            migrationBuilder.DropTable(
                name: "generation_steps");

            migrationBuilder.DropTable(
                name: "invocations");

            migrationBuilder.DropTable(
                name: "member_coverages");

            migrationBuilder.DropTable(
                name: "MemberRelationships");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "mutant_survived_tests");

            migrationBuilder.DropTable(
                name: "mutants");

            migrationBuilder.DropTable(
                name: "mutation_testing_reports");

            migrationBuilder.DropTable(
                name: "object_coverages");

            migrationBuilder.DropTable(
                name: "ObjectRelationships");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "test_execution_results");

            migrationBuilder.DropTable(
                name: "test_executions");

            migrationBuilder.DropTable(
                name: "test_results");

            migrationBuilder.DropTable(
                name: "test_runs");

            migrationBuilder.DropTable(
                name: "test_smells");

            migrationBuilder.DropTable(
                name: "generation_attempts");

            migrationBuilder.DropTable(
                name: "candidate_methods");

            migrationBuilder.DropTable(
                name: "experiment_runs");
        }
    }
}
