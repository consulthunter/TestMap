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
                    objective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    candidate_selection_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    configuration = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    results_file_path = table.Column<string>(type: "TEXT", nullable: false),
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
                    test_result_id = table.Column<int>(type: "INTEGER", nullable: false),
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
                    resolution_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    target_symbol = table.Column<string>(type: "TEXT", nullable: false),
                    syntax_kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    caller_member_symbol = table.Column<string>(type: "TEXT", nullable: false),
                    caller_file_path = table.Column<string>(type: "TEXT", nullable: false),
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
                name: "rule_definitions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    rule_id = table.Column<string>(type: "TEXT", nullable: false),
                    rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    category = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_test_mappings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    evidence_kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    is_grounded = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_path_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    path_length = table.Column<int>(type: "INTEGER", nullable: false),
                    confidence = table.Column<double>(type: "REAL", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    resolver_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_test_mappings", x => x.id);
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
                name: "candidate_inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_test_mapping_id = table.Column<int>(type: "INTEGER", nullable: true),
                    existing_test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    source_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    source_method_signature = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    existing_test_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    initial_coverage = table.Column<double>(type: "REAL", nullable: false),
                    complexity_score = table.Column<double>(type: "REAL", nullable: false),
                    selection_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    existing_test_outcome = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    is_experiment_eligible = table.Column<bool>(type: "INTEGER", nullable: false),
                    ineligibility_reason = table.Column<string>(type: "TEXT", nullable: false),
                    risk_score = table.Column<double>(type: "REAL", nullable: true),
                    metric_driven_score = table.Column<double>(type: "REAL", nullable: true),
                    expected_metric_delta = table.Column<double>(type: "REAL", nullable: true),
                    metric_guardrail_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    metric_selection_reason = table.Column<string>(type: "TEXT", nullable: false),
                    test_state = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    recommended_action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    test_state_reason = table.Column<string>(type: "TEXT", nullable: false),
                    selection_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    baseline_run_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    context_evidence_kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    context_evidence_summary = table.Column<string>(type: "TEXT", nullable: false),
                    has_grounded_test_context = table.Column<bool>(type: "INTEGER", nullable: false),
                    mapped_test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    mapped_test_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    access_path_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    access_path_member_ids_json = table.Column<string>(type: "TEXT", nullable: false),
                    trace_summary = table.Column<string>(type: "TEXT", nullable: false),
                    trace_json = table.Column<string>(type: "TEXT", nullable: false),
                    test_intentions_summary = table.Column<string>(type: "TEXT", nullable: false),
                    type_construction_summary = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_metadata_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_inventory", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_inventory_source_test_mappings_source_test_mapping_id",
                        column: x => x.source_test_mapping_id,
                        principalTable: "source_test_mappings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "source_test_mapping_trace_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    source_test_mapping_id = table.Column<int>(type: "INTEGER", nullable: false),
                    step_index = table.Column<int>(type: "INTEGER", nullable: false),
                    from_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    to_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    relationship_kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    edge_source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_test_mapping_trace_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_source_test_mapping_trace_steps_source_test_mappings_source_test_mapping_id",
                        column: x => x.source_test_mapping_id,
                        principalTable: "source_test_mappings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coverage_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_run_id = table.Column<int>(type: "INTEGER", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_coverage_reports_test_runs_test_run_id",
                        column: x => x.test_run_id,
                        principalTable: "test_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mutation_testing_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_run_id = table.Column<int>(type: "INTEGER", nullable: true),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: true),
                    scope_kind = table.Column<string>(type: "TEXT", nullable: false),
                    is_baseline = table.Column<bool>(type: "INTEGER", nullable: false),
                    source_project_path = table.Column<string>(type: "TEXT", nullable: false),
                    test_project_path = table.Column<string>(type: "TEXT", nullable: false),
                    target_framework = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_mutation_testing_reports_test_runs_test_run_id",
                        column: x => x.test_run_id,
                        principalTable: "test_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                    table.ForeignKey(
                        name: "FK_test_results_test_runs_test_run_id",
                        column: x => x.test_run_id,
                        principalTable: "test_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidate_methods",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    candidate_inventory_id = table.Column<int>(type: "INTEGER", nullable: true),
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
                    test_intentions_summary = table.Column<string>(type: "TEXT", nullable: false),
                    type_construction_summary = table.Column<string>(type: "TEXT", nullable: false),
                    candidate_metadata_json = table.Column<string>(type: "TEXT", nullable: false),
                    selection_time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_methods_candidate_inventory_candidate_inventory_id",
                        column: x => x.candidate_inventory_id,
                        principalTable: "candidate_inventory",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_candidate_methods_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_mutants_mutation_testing_reports_mutation_testing_report_id",
                        column: x => x.mutation_testing_report_id,
                        principalTable: "mutation_testing_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experiment_matrix_work_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    stable_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    objective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    approach = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    metrics_path = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    context_mode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    budget_mode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ablation_variant_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    step_config_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_heartbeat_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment_matrix_work_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_experiment_matrix_work_items_candidate_methods_candidate_method_id",
                        column: x => x.candidate_method_id,
                        principalTable: "candidate_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_experiment_matrix_work_items_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_mutant_survived_tests_mutants_mutant_id",
                        column: x => x.mutant_id,
                        principalTable: "mutants",
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
                    experiment_matrix_work_item_id = table.Column<int>(type: "INTEGER", nullable: true),
                    provider_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    strategy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    objective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    generation_approach = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    metrics_path = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    context_mode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    budget_mode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ablation_variant_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    step_config_json = table.Column<string>(type: "TEXT", nullable: false),
                    effective_profile_json = table.Column<string>(type: "TEXT", nullable: false),
                    effective_profile_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    temperature = table.Column<double>(type: "REAL", nullable: false),
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
                    error_message = table.Column<string>(type: "TEXT", nullable: false),
                    rule_decision_snapshot_json = table.Column<string>(type: "TEXT", nullable: false)
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
                        name: "FK_generation_attempts_experiment_matrix_work_items_experiment_matrix_work_item_id",
                        column: x => x.experiment_matrix_work_item_id,
                        principalTable: "experiment_matrix_work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_generation_attempts_generation_attempts_parent_attempt_id",
                        column: x => x.parent_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "generated_test_executions",
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
                    validation_result_json = table.Column<string>(type: "TEXT", nullable: false),
                    accepted = table.Column<bool>(type: "INTEGER", nullable: true),
                    acceptance_reason = table.Column<string>(type: "TEXT", nullable: false),
                    validation_rule_decision_snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                    classification_rule_decision_snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                    execution_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    structured_errors = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_test_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_test_executions_generation_attempts_generation_attempt_id",
                        column: x => x.generation_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generation_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    generation_attempt_id = table.Column<int>(type: "INTEGER", nullable: false),
                    step_name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    skip_reason = table.Column<string>(type: "TEXT", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    start_time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    end_time = table.Column<DateTime>(type: "TEXT", nullable: true),
                    prompt = table.Column<string>(type: "TEXT", nullable: false),
                    response = table.Column<string>(type: "TEXT", nullable: false),
                    tokens_used = table.Column<int>(type: "INTEGER", nullable: false),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: false),
                    validation_result = table.Column<string>(type: "TEXT", nullable: false),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    rule_decision_snapshot_json = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "rule_decisions",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    csharp_project_id = table.Column<int>(type: "INTEGER", nullable: true),
                    scope_kind = table.Column<string>(type: "TEXT", nullable: false),
                    scope_id = table.Column<string>(type: "TEXT", nullable: false),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: true),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: true),
                    generation_attempt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    generated_test_execution_id = table.Column<int>(type: "INTEGER", nullable: true),
                    decision_kind = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    rule_id = table.Column<string>(type: "TEXT", nullable: false),
                    rule_version = table.Column<string>(type: "TEXT", nullable: false),
                    confidence = table.Column<string>(type: "TEXT", nullable: false),
                    evidence = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_decisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rule_decisions_candidate_methods_candidate_method_id",
                        column: x => x.candidate_method_id,
                        principalTable: "candidate_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rule_decisions_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rule_decisions_generated_test_executions_generated_test_execution_id",
                        column: x => x.generated_test_execution_id,
                        principalTable: "generated_test_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rule_decisions_generation_attempts_generation_attempt_id",
                        column: x => x.generation_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_inventory_project_id",
                table: "candidate_inventory",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_inventory_project_id_selection_strategy",
                table: "candidate_inventory",
                columns: new[] { "project_id", "selection_strategy" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_inventory_project_id_selection_strategy_context_evidence_kind",
                table: "candidate_inventory",
                columns: new[] { "project_id", "selection_strategy", "context_evidence_kind" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_inventory_project_id_selection_strategy_is_experiment_eligible",
                table: "candidate_inventory",
                columns: new[] { "project_id", "selection_strategy", "is_experiment_eligible" });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_inventory_source_test_mapping_id",
                table: "candidate_inventory",
                column: "source_test_mapping_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_method_risk_scores_candidate_method_id",
                table: "candidate_method_risk_scores",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_method_risk_scores_member_id",
                table: "candidate_method_risk_scores",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_methods_candidate_inventory_id",
                table: "candidate_methods",
                column: "candidate_inventory_id");

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
                name: "IX_coverage_reports_test_run_id",
                table: "coverage_reports",
                column: "test_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_matrix_work_items_candidate_method_id",
                table: "experiment_matrix_work_items",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_matrix_work_items_experiment_run_id_status",
                table: "experiment_matrix_work_items",
                columns: new[] { "experiment_run_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_experiment_matrix_work_items_stable_key",
                table: "experiment_matrix_work_items",
                column: "stable_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_rerun_results_run_id",
                table: "flaky_test_rerun_results",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_flaky_test_rerun_results_test_result_id",
                table: "flaky_test_rerun_results",
                column: "test_result_id");

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
                name: "IX_generated_test_executions_generation_attempt_id",
                table: "generated_test_executions",
                column: "generation_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_generation_attempts_candidate_method_id",
                table: "generation_attempts",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_generation_attempts_experiment_matrix_work_item_id",
                table: "generation_attempts",
                column: "experiment_matrix_work_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_generation_attempts_parent_attempt_id",
                table: "generation_attempts",
                column: "parent_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_generation_steps_generation_attempt_id",
                table: "generation_steps",
                column: "generation_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_mutant_survived_tests_mutant_id_test_member_id_stryker_test_id_test_name_content_hash",
                table: "mutant_survived_tests",
                columns: new[] { "mutant_id", "test_member_id", "stryker_test_id", "test_name", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mutants_mutation_testing_report_id_stryker_mutant_id_file_path_mutator_name_content_hash",
                table: "mutants",
                columns: new[] { "mutation_testing_report_id", "stryker_mutant_id", "file_path", "mutator_name", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mutation_testing_reports_project_id_experiment_run_id_scope_kind_is_baseline_source_project_path_test_project_path_target_framework",
                table: "mutation_testing_reports",
                columns: new[] { "project_id", "experiment_run_id", "scope_kind", "is_baseline", "source_project_path", "test_project_path", "target_framework" });

            migrationBuilder.CreateIndex(
                name: "IX_mutation_testing_reports_test_run_id",
                table: "mutation_testing_reports",
                column: "test_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_candidate_method_id",
                table: "rule_decisions",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_csharp_project_id",
                table: "rule_decisions",
                column: "csharp_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_experiment_run_id",
                table: "rule_decisions",
                column: "experiment_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_generated_test_execution_id",
                table: "rule_decisions",
                column: "generated_test_execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_generation_attempt_id",
                table: "rule_decisions",
                column: "generation_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_project_id",
                table: "rule_decisions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_rule_id_rule_version",
                table: "rule_decisions",
                columns: new[] { "rule_id", "rule_version" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_decisions_scope_kind_scope_id",
                table: "rule_decisions",
                columns: new[] { "scope_kind", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_definitions_category",
                table: "rule_definitions",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_rule_definitions_rule_id_rule_version",
                table: "rule_definitions",
                columns: new[] { "rule_id", "rule_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mapping_trace_steps_from_member_id_to_member_id",
                table: "source_test_mapping_trace_steps",
                columns: new[] { "from_member_id", "to_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mapping_trace_steps_source_test_mapping_id",
                table: "source_test_mapping_trace_steps",
                column: "source_test_mapping_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mapping_trace_steps_source_test_mapping_id_step_index",
                table: "source_test_mapping_trace_steps",
                columns: new[] { "source_test_mapping_id", "step_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mappings_project_id",
                table: "source_test_mappings",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mappings_project_id_source_member_id_test_member_id_evidence_kind",
                table: "source_test_mappings",
                columns: new[] { "project_id", "source_member_id", "test_member_id", "evidence_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mappings_source_member_id",
                table: "source_test_mappings",
                column: "source_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_source_test_mappings_test_member_id",
                table: "source_test_mappings",
                column: "test_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_results_test_run_id",
                table: "test_results",
                column: "test_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_test_runs_run_id",
                table: "test_runs",
                column: "run_id",
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
                name: "object_coverages");

            migrationBuilder.DropTable(
                name: "ObjectRelationships");

            migrationBuilder.DropTable(
                name: "objects");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "rule_decisions");

            migrationBuilder.DropTable(
                name: "rule_definitions");

            migrationBuilder.DropTable(
                name: "source_test_mapping_trace_steps");

            migrationBuilder.DropTable(
                name: "test_results");

            migrationBuilder.DropTable(
                name: "test_smells");

            migrationBuilder.DropTable(
                name: "mutants");

            migrationBuilder.DropTable(
                name: "generated_test_executions");

            migrationBuilder.DropTable(
                name: "mutation_testing_reports");

            migrationBuilder.DropTable(
                name: "generation_attempts");

            migrationBuilder.DropTable(
                name: "test_runs");

            migrationBuilder.DropTable(
                name: "experiment_matrix_work_items");

            migrationBuilder.DropTable(
                name: "candidate_methods");

            migrationBuilder.DropTable(
                name: "candidate_inventory");

            migrationBuilder.DropTable(
                name: "experiment_runs");

            migrationBuilder.DropTable(
                name: "source_test_mappings");
        }
    }
}
