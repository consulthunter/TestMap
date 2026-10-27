using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateBaselines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_baselines",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    stable_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    experiment_run_id = table.Column<int>(type: "INTEGER", nullable: false),
                    candidate_method_id = table.Column<int>(type: "INTEGER", nullable: false),
                    member_id = table.Column<int>(type: "INTEGER", nullable: false),
                    test_run_id = table.Column<int>(type: "INTEGER", nullable: true),
                    usable = table.Column<bool>(type: "INTEGER", nullable: false),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_baselines", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_baselines_stable_key",
                table: "candidate_baselines",
                column: "stable_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_baselines");
        }
    }
}
