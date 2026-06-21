using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicExtensionPatchMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "applied_helper_count",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "applied_using_count",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patch_application_outcome",
                table: "generation_attempts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "patch_json",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repair_patch_json",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applied_helper_count",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "applied_using_count",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "patch_application_outcome",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "patch_json",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "repair_patch_json",
                table: "generation_attempts");
        }
    }
}
