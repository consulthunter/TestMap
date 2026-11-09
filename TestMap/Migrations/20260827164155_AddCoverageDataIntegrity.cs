using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverageDataIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "object_id",
                table: "object_coverages",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "attribution_reason",
                table: "object_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "attribution_status",
                table: "object_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "branch_counts_available",
                table: "object_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "filename",
                table: "object_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "line_counts_available",
                table: "object_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "object_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "package_name",
                table: "object_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "source_ordinal",
                table: "object_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AlterColumn<int>(
                name: "member_id",
                table: "member_coverages",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "attribution_reason",
                table: "member_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "attribution_status",
                table: "member_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "branch_counts_available",
                table: "member_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "line_counts_available",
                table: "member_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "member_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "object_coverage_id",
                table: "member_coverages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature",
                table: "member_coverages",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "source_ordinal",
                table: "member_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<bool>(
                name: "branch_counts_available",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "collection_metadata_json",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "collection_reason",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "collection_status",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "LegacyNotMeasured");

            migrationBuilder.AddColumn<bool>(
                name: "has_usable_coverage",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "line_counts_available",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "mapped_member_count",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "mapped_object_count",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "measurement_policy_version",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "raw_member_count",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "raw_object_count",
                table: "coverage_reports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "run_id",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "successful_collector",
                table: "coverage_reports",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_object_coverages_coverage_report_id_source_ordinal",
                table: "object_coverages",
                columns: new[] { "coverage_report_id", "source_ordinal" },
                unique: true,
                filter: "source_ordinal >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_member_coverages_object_coverage_id_source_ordinal",
                table: "member_coverages",
                columns: new[] { "object_coverage_id", "source_ordinal" },
                unique: true,
                filter: "object_coverage_id IS NOT NULL AND source_ordinal >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_coverage_reports_project_id_run_id",
                table: "coverage_reports",
                columns: new[] { "project_id", "run_id" },
                unique: true,
                filter: "run_id <> ''");

            migrationBuilder.AddForeignKey(
                name: "FK_member_coverages_object_coverages_object_coverage_id",
                table: "member_coverages",
                column: "object_coverage_id",
                principalTable: "object_coverages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_member_coverages_object_coverages_object_coverage_id",
                table: "member_coverages");

            migrationBuilder.DropIndex(
                name: "IX_object_coverages_coverage_report_id_source_ordinal",
                table: "object_coverages");

            migrationBuilder.DropIndex(
                name: "IX_member_coverages_object_coverage_id_source_ordinal",
                table: "member_coverages");

            migrationBuilder.DropIndex(
                name: "IX_coverage_reports_project_id_run_id",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "attribution_reason",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "attribution_status",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "branch_counts_available",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "filename",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "line_counts_available",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "name",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "package_name",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "source_ordinal",
                table: "object_coverages");

            migrationBuilder.DropColumn(
                name: "attribution_reason",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "attribution_status",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "branch_counts_available",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "line_counts_available",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "name",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "object_coverage_id",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "signature",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "source_ordinal",
                table: "member_coverages");

            migrationBuilder.DropColumn(
                name: "branch_counts_available",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "collection_metadata_json",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "collection_reason",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "collection_status",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "has_usable_coverage",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "line_counts_available",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "mapped_member_count",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "mapped_object_count",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "measurement_policy_version",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "raw_member_count",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "raw_object_count",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "run_id",
                table: "coverage_reports");

            migrationBuilder.DropColumn(
                name: "successful_collector",
                table: "coverage_reports");

            migrationBuilder.AlterColumn<int>(
                name: "object_id",
                table: "object_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "member_id",
                table: "member_coverages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}

