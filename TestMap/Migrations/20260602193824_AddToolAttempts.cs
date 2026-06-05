using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddToolAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tool_attempts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    matrix_work_item_id = table.Column<int>(type: "INTEGER", nullable: true),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    targeted_baseline_id = table.Column<int>(type: "INTEGER", nullable: true),
                    effective_profile_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    tool_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    run_status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    validation_outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    observed_outcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    image_name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    image_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    base_commit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    workspace_path = table.Column<string>(type: "TEXT", nullable: false),
                    artifact_path = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    elapsed_seconds = table.Column<double>(type: "REAL", nullable: false),
                    timeout_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    exit_code = table.Column<int>(type: "INTEGER", nullable: true),
                    tool_version = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    provider_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    jsonl_log_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    usage_available = table.Column<bool>(type: "INTEGER", nullable: false),
                    usage_source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    input_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    output_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    estimated_prompt_tokens = table.Column<int>(type: "INTEGER", nullable: true),
                    changed_files_count = table.Column<int>(type: "INTEGER", nullable: false),
                    production_files_changed = table.Column<int>(type: "INTEGER", nullable: false),
                    test_files_changed = table.Column<int>(type: "INTEGER", nullable: false),
                    project_files_changed = table.Column<int>(type: "INTEGER", nullable: false),
                    deleted_files_count = table.Column<int>(type: "INTEGER", nullable: false),
                    constraint_violation_summary = table.Column<string>(type: "TEXT", nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_tool_attempts_candidate_methods_candidate_method_id",
                        column: x => x.candidate_method_id,
                        principalTable: "candidate_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tool_attempts_experiment_matrix_work_items_matrix_work_item_id",
                        column: x => x.matrix_work_item_id,
                        principalTable: "experiment_matrix_work_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tool_attempts_experiment_runs_experiment_run_id",
                        column: x => x.experiment_run_id,
                        principalTable: "experiment_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempts_candidate_method_id",
                table: "tool_attempts",
                column: "candidate_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempts_experiment_run_id",
                table: "tool_attempts",
                column: "experiment_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempts_experiment_run_id_tool_id",
                table: "tool_attempts",
                columns: new[] { "experiment_run_id", "tool_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempts_matrix_work_item_id",
                table: "tool_attempts",
                column: "matrix_work_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_attempts");
        }
    }
}
