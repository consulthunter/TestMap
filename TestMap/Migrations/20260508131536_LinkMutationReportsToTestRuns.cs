using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class LinkMutationReportsToTestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 CREATE TABLE IF NOT EXISTS mutation_testing_reports (
                                     id INTEGER NOT NULL CONSTRAINT PK_mutation_testing_reports PRIMARY KEY AUTOINCREMENT,
                                     project_id INTEGER NOT NULL,
                                     schema_version TEXT NOT NULL,
                                     project_root TEXT NOT NULL,
                                     mutation_score REAL NOT NULL,
                                     files TEXT NOT NULL,
                                     test_files TEXT NOT NULL,
                                     thresholds TEXT NOT NULL,
                                     created_at TEXT NULL
                                 );
                                 """);

            migrationBuilder.AddColumn<int>(
                name: "test_run_id",
                table: "mutation_testing_reports",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_mutation_testing_reports_test_run_id",
                table: "mutation_testing_reports",
                column: "test_run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_mutation_testing_reports_test_run_id",
                table: "mutation_testing_reports");

            migrationBuilder.DropColumn(
                name: "test_run_id",
                table: "mutation_testing_reports");
        }
    }
}
