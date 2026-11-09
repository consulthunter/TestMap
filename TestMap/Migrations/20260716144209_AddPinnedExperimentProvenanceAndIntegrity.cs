using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedExperimentProvenanceAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "workspace_integrity_status",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "base_commit",
                table: "generation_attempts",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "workspace_integrity_status",
                table: "generation_attempts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "materialized_at_utc",
                table: "experiment_runs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provenance_policy_version",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "repository_identity",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 511,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "requested_commit",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "resolved_commit",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_id",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_manifest_sha256",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_source_sha256",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "workspace_integrity_status",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "workspace_integrity_observations",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: true),
                    target_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    producer_lane = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    work_item_stable_key = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    attempt_number = table.Column<int>(type: "INTEGER", nullable: true),
                    checkpoint = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    expected_commit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    actual_commit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    origin_matches = table.Column<bool>(type: "INTEGER", nullable: false),
                    working_tree_dirty = table.Column<bool>(type: "INTEGER", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    details = table.Column<string>(type: "TEXT", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_integrity_observations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_target_id_resolved_commit",
                table: "experiment_runs",
                columns: new[] { "target_id", "resolved_commit" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_integrity_observations_experiment_run_id_checkpoint_status",
                table: "workspace_integrity_observations",
                columns: new[] { "experiment_run_id", "checkpoint", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_integrity_observations_target_id_observed_at_utc",
                table: "workspace_integrity_observations",
                columns: new[] { "target_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_integrity_observations_work_item_stable_key_attempt_number_checkpoint",
                table: "workspace_integrity_observations",
                columns: new[] { "work_item_stable_key", "attempt_number", "checkpoint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_integrity_observations");

            migrationBuilder.DropIndex(
                name: "IX_experiment_runs_target_id_resolved_commit",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "workspace_integrity_status",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "base_commit",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "workspace_integrity_status",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "materialized_at_utc",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "provenance_policy_version",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "repository_identity",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "requested_commit",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "resolved_commit",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "target_id",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "target_manifest_sha256",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "target_source_sha256",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "workspace_integrity_status",
                table: "experiment_runs");
        }
    }
}
