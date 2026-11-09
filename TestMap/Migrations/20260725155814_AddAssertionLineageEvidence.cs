using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddAssertionLineageEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assertion_catalog_version",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "assertion_lineage_max_depth",
                table: "experiment_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "assertion_lineage_path_cap",
                table: "experiment_runs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "assertion_lineage_policy_version",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "assertion_lineage_measurements",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    generation_attempt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    tool_attempt_id = table.Column<int>(type: "INTEGER", nullable: true),
                    producer_lane = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    failure_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: true),
                    policy_version = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    assertion_catalog_version = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    max_depth = table.Column<int>(type: "INTEGER", nullable: false),
                    eligible_test_count = table.Column<int>(type: "INTEGER", nullable: true),
                    analyzed_test_count = table.Column<int>(type: "INTEGER", nullable: true),
                    unavailable_test_count = table.Column<int>(type: "INTEGER", nullable: true),
                    no_recognized_assertion_test_count = table.Column<int>(type: "INTEGER", nullable: true),
                    recognized_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    unrecognized_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    traced_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    trivial_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    unresolved_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    analysis_duration_ms = table.Column<double>(type: "REAL", nullable: true),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assertion_lineage_measurements", x => x.id);
                    table.CheckConstraint("ck_assertion_lineage_measurements_category_counts", "recognized_assertion_count IS NULL OR recognized_assertion_count = traced_assertion_count + trivial_assertion_count + unresolved_assertion_count");
                    table.CheckConstraint("ck_assertion_lineage_measurements_positive_depth", "max_depth > 0");
                    table.CheckConstraint("ck_assertion_lineage_measurements_single_owner", "(generation_attempt_id IS NOT NULL AND tool_attempt_id IS NULL) OR (generation_attempt_id IS NULL AND tool_attempt_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_assertion_lineage_measurements_candidate_methods_candidate_method_id",
                        column: x => x.candidate_method_id,
                        principalTable: "candidate_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assertion_lineage_measurements_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assertion_lineage_measurements_generation_attempts_generation_attempt_id",
                        column: x => x.generation_attempt_id,
                        principalTable: "generation_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assertion_lineage_measurements_tool_attempts_tool_attempt_id",
                        column: x => x.tool_attempt_id,
                        principalTable: "tool_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generated_test_assertion_summaries",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    assertion_lineage_measurement_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    generated_test_execution_id = table.Column<int>(type: "INTEGER", nullable: true),
                    tool_attempt_generated_test_id = table.Column<int>(type: "INTEGER", nullable: true),
                    test_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    test_file_path = table.Column<string>(type: "TEXT", nullable: false),
                    test_member_content_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    fallback_identity_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    status_reason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    recognized_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    unrecognized_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    traced_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    trivial_assertion_count = table.Column<int>(type: "INTEGER", nullable: true),
                    unresolved_assertion_count = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_test_assertion_summaries", x => x.id);
                    table.CheckConstraint("ck_generated_test_assertion_summaries_category_counts", "recognized_assertion_count IS NULL OR recognized_assertion_count = traced_assertion_count + trivial_assertion_count + unresolved_assertion_count");
                    table.CheckConstraint("ck_generated_test_assertion_summaries_single_owner", "generated_test_execution_id IS NULL OR tool_attempt_generated_test_id IS NULL");
                    table.ForeignKey(
                        name: "FK_generated_test_assertion_summaries_assertion_lineage_measurements_assertion_lineage_measurement_id",
                        column: x => x.assertion_lineage_measurement_id,
                        principalTable: "assertion_lineage_measurements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_generated_test_assertion_summaries_generated_test_executions_generated_test_execution_id",
                        column: x => x.generated_test_execution_id,
                        principalTable: "generated_test_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_generated_test_assertion_summaries_tool_attempt_generated_tests_tool_attempt_generated_test_id",
                        column: x => x.tool_attempt_generated_test_id,
                        principalTable: "tool_attempt_generated_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assertion_observations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    generated_test_assertion_summary_id = table.Column<int>(type: "INTEGER", nullable: false),
                    invocation_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    assertion_method = table.Column<string>(type: "TEXT", nullable: false),
                    recognition_kind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    file_path = table.Column<string>(type: "TEXT", nullable: false),
                    start_line = table.Column<int>(type: "INTEGER", nullable: false),
                    start_column = table.Column<int>(type: "INTEGER", nullable: false),
                    end_line = table.Column<int>(type: "INTEGER", nullable: false),
                    end_column = table.Column<int>(type: "INTEGER", nullable: false),
                    expression_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    resolution_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    depth_reached = table.Column<int>(type: "INTEGER", nullable: false),
                    target_relation = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    trace_summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assertion_observations", x => x.id);
                    table.ForeignKey(
                        name: "FK_assertion_observations_generated_test_assertion_summaries_generated_test_assertion_summary_id",
                        column: x => x.generated_test_assertion_summary_id,
                        principalTable: "generated_test_assertion_summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assertion_lineage_steps",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    assertion_observation_id = table.Column<int>(type: "INTEGER", nullable: false),
                    input_index = table.Column<int>(type: "INTEGER", nullable: false),
                    path_index = table.Column<int>(type: "INTEGER", nullable: false),
                    step_index = table.Column<int>(type: "INTEGER", nullable: false),
                    step_kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    depth = table.Column<int>(type: "INTEGER", nullable: false),
                    symbol_display = table.Column<string>(type: "TEXT", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: true),
                    file_path = table.Column<string>(type: "TEXT", nullable: true),
                    start_line = table.Column<int>(type: "INTEGER", nullable: true),
                    start_column = table.Column<int>(type: "INTEGER", nullable: true),
                    end_line = table.Column<int>(type: "INTEGER", nullable: true),
                    end_column = table.Column<int>(type: "INTEGER", nullable: true),
                    outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    reason_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assertion_lineage_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_assertion_lineage_steps_assertion_observations_assertion_observation_id",
                        column: x => x.assertion_observation_id,
                        principalTable: "assertion_observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_measurements_candidate_method_id",
                table: "assertion_lineage_measurements",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_measurements_experiment_run_id",
                table: "assertion_lineage_measurements",
                column: "experiment_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_measurements_generation_attempt_id_policy_version_assertion_catalog_version_max_depth",
                table: "assertion_lineage_measurements",
                columns: new[] { "generation_attempt_id", "policy_version", "assertion_catalog_version", "max_depth" },
                unique: true,
                filter: "generation_attempt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_measurements_project_id",
                table: "assertion_lineage_measurements",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_measurements_tool_attempt_id_policy_version_assertion_catalog_version_max_depth",
                table: "assertion_lineage_measurements",
                columns: new[] { "tool_attempt_id", "policy_version", "assertion_catalog_version", "max_depth" },
                unique: true,
                filter: "tool_attempt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_steps_assertion_observation_id_input_index_path_index_step_index",
                table: "assertion_lineage_steps",
                columns: new[] { "assertion_observation_id", "input_index", "path_index", "step_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assertion_lineage_steps_step_kind_member_id",
                table: "assertion_lineage_steps",
                columns: new[] { "step_kind", "member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_assertion_observations_category",
                table: "assertion_observations",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_assertion_observations_generated_test_assertion_summary_id_expression_hash",
                table: "assertion_observations",
                columns: new[] { "generated_test_assertion_summary_id", "expression_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assertion_observations_generated_test_assertion_summary_id_ordinal",
                table: "assertion_observations",
                columns: new[] { "generated_test_assertion_summary_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assertion_observations_invocation_id",
                table: "assertion_observations",
                column: "invocation_id");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_assertion_lineage_measurement_id",
                table: "generated_test_assertion_summaries",
                column: "assertion_lineage_measurement_id");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_assertion_lineage_measurement_id_fallback_identity_hash",
                table: "generated_test_assertion_summaries",
                columns: new[] { "assertion_lineage_measurement_id", "fallback_identity_hash" },
                unique: true,
                filter: "test_member_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_assertion_lineage_measurement_id_test_member_id",
                table: "generated_test_assertion_summaries",
                columns: new[] { "assertion_lineage_measurement_id", "test_member_id" },
                unique: true,
                filter: "test_member_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_generated_test_execution_id",
                table: "generated_test_assertion_summaries",
                column: "generated_test_execution_id",
                unique: true,
                filter: "generated_test_execution_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_test_member_id",
                table: "generated_test_assertion_summaries",
                column: "test_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_generated_test_assertion_summaries_tool_attempt_generated_test_id",
                table: "generated_test_assertion_summaries",
                column: "tool_attempt_generated_test_id",
                unique: true,
                filter: "tool_attempt_generated_test_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assertion_lineage_steps");

            migrationBuilder.DropTable(
                name: "assertion_observations");

            migrationBuilder.DropTable(
                name: "generated_test_assertion_summaries");

            migrationBuilder.DropTable(
                name: "assertion_lineage_measurements");

            migrationBuilder.DropColumn(
                name: "assertion_catalog_version",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "assertion_lineage_max_depth",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "assertion_lineage_path_cap",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "assertion_lineage_policy_version",
                table: "experiment_runs");
        }
    }
}
