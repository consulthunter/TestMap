using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddMutantOriginalCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_code",
                table: "mutants",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_code",
                table: "mutants");
        }
    }
}
