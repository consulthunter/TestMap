using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateCohorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "candidate_cohort_id",
                table: "experiment_runs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "experiment_series_id",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "candidate_cohort_member_id",
                table: "candidate_methods",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "candidate_cohorts",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    project_id = table.Column<int>(type: "INTEGER", nullable: false),
                    cohort_key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    repository_identity = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    commit_hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    objective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    selection_strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    selection_configuration_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    candidate_limit = table.Column<int>(type: "INTEGER", nullable: false),
                    random_seed = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_cohorts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_cohort_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    candidate_cohort_id = table.Column<int>(type: "INTEGER", nullable: false),
                    ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    source_member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    source_method_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    source_method_signature = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    source_file_path = table.Column<string>(type: "TEXT", nullable: false),
                    containing_type = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    source_content_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    candidate_snapshot_json = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_cohort_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_cohort_members_candidate_cohorts_candidate_cohort_id",
                        column: x => x.candidate_cohort_id,
                        principalTable: "candidate_cohorts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_candidate_cohort_id",
                table: "experiment_runs",
                column: "candidate_cohort_id");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_experiment_series_id",
                table: "experiment_runs",
                column: "experiment_series_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_methods_candidate_cohort_member_id",
                table: "candidate_methods",
                column: "candidate_cohort_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_cohort_members_candidate_cohort_id_ordinal",
                table: "candidate_cohort_members",
                columns: new[] { "candidate_cohort_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_cohort_members_candidate_cohort_id_source_member_id",
                table: "candidate_cohort_members",
                columns: new[] { "candidate_cohort_id", "source_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_cohorts_project_id_cohort_key",
                table: "candidate_cohorts",
                columns: new[] { "project_id", "cohort_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_methods_candidate_cohort_members_candidate_cohort_member_id",
                table: "candidate_methods",
                column: "candidate_cohort_member_id",
                principalTable: "candidate_cohort_members",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_experiment_runs_candidate_cohorts_candidate_cohort_id",
                table: "experiment_runs",
                column: "candidate_cohort_id",
                principalTable: "candidate_cohorts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_candidate_methods_candidate_cohort_members_candidate_cohort_member_id",
                table: "candidate_methods");

            migrationBuilder.DropForeignKey(
                name: "FK_experiment_runs_candidate_cohorts_candidate_cohort_id",
                table: "experiment_runs");

            migrationBuilder.DropTable(
                name: "candidate_cohort_members");

            migrationBuilder.DropTable(
                name: "candidate_cohorts");

            migrationBuilder.DropIndex(
                name: "IX_experiment_runs_candidate_cohort_id",
                table: "experiment_runs");

            migrationBuilder.DropIndex(
                name: "IX_experiment_runs_experiment_series_id",
                table: "experiment_runs");

            migrationBuilder.DropIndex(
                name: "IX_candidate_methods_candidate_cohort_member_id",
                table: "candidate_methods");

            migrationBuilder.DropColumn(
                name: "candidate_cohort_id",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "experiment_series_id",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "candidate_cohort_member_id",
                table: "candidate_methods");
        }
    }
}
