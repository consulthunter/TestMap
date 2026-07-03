using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationTimingMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "generation_duration_seconds",
                table: "tool_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "total_attempt_duration_seconds",
                table: "tool_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "validation_duration_seconds",
                table: "tool_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "generation_duration_seconds",
                table: "generation_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "total_attempt_duration_seconds",
                table: "generation_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "validation_duration_seconds",
                table: "generation_attempts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "generation_duration_seconds",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "total_attempt_duration_seconds",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "validation_duration_seconds",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "generation_duration_seconds",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "total_attempt_duration_seconds",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "validation_duration_seconds",
                table: "generation_attempts");
        }
    }
}
