using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedProjectProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "materialized_at_utc",
                table: "projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provenance_policy_version",
                table: "projects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "repository_identity",
                table: "projects",
                type: "TEXT",
                maxLength: 511,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_commit",
                table: "projects",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resolved_commit",
                table: "projects",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_id",
                table: "projects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_manifest_sha256",
                table: "projects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_source_sha256",
                table: "projects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_repository_identity_resolved_commit",
                table: "projects",
                columns: new[] { "repository_identity", "resolved_commit" });

            migrationBuilder.CreateIndex(
                name: "IX_projects_target_id",
                table: "projects",
                column: "target_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_repository_identity_resolved_commit",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_target_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "materialized_at_utc",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "provenance_policy_version",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "repository_identity",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "requested_commit",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "resolved_commit",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "target_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "target_manifest_sha256",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "target_source_sha256",
                table: "projects");
        }
    }
}
