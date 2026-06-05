using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddToolAttemptPostMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "post_attempt_test_run_id",
                table: "tool_attempts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tool_attempt_generated_tests",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    tool_attempt_id = table.Column<int>(type: "INTEGER", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    mapping_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_attempt_generated_tests", x => x.id);
                    table.ForeignKey(
                        name: "FK_tool_attempt_generated_tests_tool_attempts_tool_attempt_id",
                        column: x => x.tool_attempt_id,
                        principalTable: "tool_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempt_generated_tests_member_id",
                table: "tool_attempt_generated_tests",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_tool_attempt_generated_tests_tool_attempt_id",
                table: "tool_attempt_generated_tests",
                column: "tool_attempt_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_attempt_generated_tests");

            migrationBuilder.DropColumn(
                name: "post_attempt_test_run_id",
                table: "tool_attempts");
        }
    }
}
