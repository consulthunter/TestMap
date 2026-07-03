using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationAttemptModifiedFileSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "modified_file_contents",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_file_path",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_file_sha256",
                table: "generation_attempts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modified_file_contents",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "modified_file_path",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "modified_file_sha256",
                table: "generation_attempts");
        }
    }
}
