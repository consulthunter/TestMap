using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class HardenEvaluationMeasurements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "coverage_after",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "coverage_before",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "coverage_delta",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "coverage_measurement_status",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "impact_measurement_status",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "measurement_failure_reason",
                table: "tool_attempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "measurement_policy_version",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "mutation_measurement_status",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "mutation_score_after",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "mutation_score_before",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "mutation_score_delta",
                table: "tool_attempts",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "run_uid",
                table: "experiment_runs",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Existing runs need distinct identities before the unique index is created.
            migrationBuilder.Sql(
                "UPDATE experiment_runs SET run_uid = lower(hex(randomblob(16))) WHERE run_uid = '';");

            migrationBuilder.CreateIndex(
                name: "IX_experiment_runs_run_uid",
                table: "experiment_runs",
                column: "run_uid",
                unique: true);

            migrationBuilder.AlterColumn<double>(
                name: "final_coverage",
                table: "generated_test_executions",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<double>(
                name: "coverage_delta",
                table: "generated_test_executions",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "REAL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_experiment_runs_run_uid",
                table: "experiment_runs");

            migrationBuilder.DropColumn(
                name: "coverage_after",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "coverage_before",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "coverage_delta",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "coverage_measurement_status",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "impact_measurement_status",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "measurement_failure_reason",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "measurement_policy_version",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "mutation_measurement_status",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "mutation_score_after",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "mutation_score_before",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "mutation_score_delta",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "run_uid",
                table: "experiment_runs");

            migrationBuilder.AlterColumn<double>(
                name: "final_coverage",
                table: "generated_test_executions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "coverage_delta",
                table: "generated_test_executions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true);
        }
    }
}
