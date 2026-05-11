using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_member_id = table.Column<int>(type: "INTEGER", nullable: false),
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
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_inventory", x => x.id);
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
                name: "IX_candidate_inventory_project_id_selection_strategy_is_experiment_eligible",
                table: "candidate_inventory",
                columns: new[] { "project_id", "selection_strategy", "is_experiment_eligible" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_inventory");
        }
    }
}
