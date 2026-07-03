using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddToolAttemptLogPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "jsonl_log_path",
                table: "tool_attempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "stderr_log_path",
                table: "tool_attempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "stdout_log_path",
                table: "tool_attempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "jsonl_log_path",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "stderr_log_path",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "stdout_log_path",
                table: "tool_attempts");
        }
    }
}
