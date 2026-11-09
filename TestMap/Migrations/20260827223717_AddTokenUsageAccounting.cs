using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestMap.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenUsageAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "usage_policy_version",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_status",
                table: "tool_attempts",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AlterColumn<int>(
                name: "tokens_used",
                table: "generation_steps",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "usage_policy_version",
                table: "generation_steps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_source",
                table: "generation_steps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_status",
                table: "generation_steps",
                type: "TEXT",
                nullable: false,
                defaultValue: "missing");

            migrationBuilder.AlterColumn<int>(
                name: "total_tokens_used",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "input_tokens",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "output_tokens",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_policy_version",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_source",
                table: "generation_attempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_status",
                table: "generation_attempts",
                type: "TEXT",
                nullable: false,
                defaultValue: "missing");

            // Historical built-in totals are prompt-only estimates. Preserve the values but do not
            // synthesize split components or classify them as complete.
            migrationBuilder.Sql("""
                UPDATE generation_steps
                SET usage_status = 'missing', usage_source = 'legacy-prompt-only'
                WHERE tokens_used IS NOT NULL;

                UPDATE generation_attempts
                SET usage_status = 'missing', usage_source = 'legacy-prompt-only'
                WHERE total_tokens_used IS NOT NULL;

                UPDATE tool_attempts
                SET usage_status = CASE
                    WHEN input_tokens IS NOT NULL AND output_tokens IS NOT NULL THEN 'complete-reported'
                    WHEN input_tokens IS NOT NULL OR output_tokens IS NOT NULL THEN 'partial'
                    ELSE 'missing'
                END,
                usage_available = CASE
                    WHEN input_tokens IS NOT NULL AND output_tokens IS NOT NULL THEN 1
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "usage_policy_version",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "usage_status",
                table: "tool_attempts");

            migrationBuilder.DropColumn(
                name: "usage_policy_version",
                table: "generation_steps");

            migrationBuilder.DropColumn(
                name: "usage_source",
                table: "generation_steps");

            migrationBuilder.DropColumn(
                name: "usage_status",
                table: "generation_steps");

            migrationBuilder.DropColumn(
                name: "input_tokens",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "output_tokens",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "usage_policy_version",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "usage_source",
                table: "generation_attempts");

            migrationBuilder.DropColumn(
                name: "usage_status",
                table: "generation_attempts");

            migrationBuilder.AlterColumn<int>(
                name: "tokens_used",
                table: "generation_steps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "total_tokens_used",
                table: "generation_attempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
