using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestMap.Persistence.Ef;

#nullable disable

namespace TestMap.Migrations;

[DbContext(typeof(TestMapDbContext))]
[Migration("20260914090000_AddAnalysisOriginAndReportRoles")]
public partial class AddAnalysisOriginAndReportRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("signature", "members", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("origin_kind", "members", "TEXT", nullable: false, defaultValue: "Baseline");
        migrationBuilder.AddColumn<int>("origin_attempt_id", "members", "INTEGER", nullable: true);

        migrationBuilder.AddColumn<string>("report_role", "test_runs", "TEXT", nullable: false, defaultValue: "AttemptMeasurement");

        migrationBuilder.AddColumn<string>("scope_kind", "coverage_reports", "TEXT", nullable: false, defaultValue: "Solution");
        migrationBuilder.AddColumn<string>("report_role", "coverage_reports", "TEXT", nullable: false, defaultValue: "RepositoryBaseline");
        migrationBuilder.AddColumn<int>("experiment_run_id", "coverage_reports", "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>("source_project_path", "coverage_reports", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("test_project_path", "coverage_reports", "TEXT", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("target_framework", "coverage_reports", "TEXT", nullable: false, defaultValue: "");

        migrationBuilder.AddColumn<string>("report_role", "mutation_testing_reports", "TEXT", nullable: false, defaultValue: "RepositoryBaseline");
        migrationBuilder.AddColumn<double>("coverage_before", "generated_test_executions", "REAL", nullable: true);

        migrationBuilder.Sql("""
            UPDATE test_runs
            SET report_role = CASE
                WHEN run_id LIKE 'baseline_%' THEN 'RepositoryBaseline'
                WHEN run_id LIKE 'targeted_baseline_%' OR EXISTS (
                    SELECT 1 FROM mutation_testing_reports mr
                    WHERE mr.test_run_id = test_runs.id
                      AND mr.scope_kind = 'SourceProject'
                      AND mr.is_baseline = 1)
                THEN 'TargetedBaseline'
                ELSE 'AttemptMeasurement'
            END;

            UPDATE mutation_testing_reports
            SET report_role = CASE
                WHEN scope_kind = 'Solution' AND is_baseline = 1 THEN 'RepositoryBaseline'
                WHEN scope_kind = 'SourceProject' AND is_baseline = 1 THEN 'TargetedBaseline'
                ELSE 'AttemptMeasurement'
            END;

            UPDATE coverage_reports
            SET report_role = COALESCE(
                    (SELECT tr.report_role FROM test_runs tr WHERE tr.id = coverage_reports.test_run_id),
                    'RepositoryBaseline'),
                scope_kind = CASE
                    WHEN COALESCE((SELECT tr.report_role FROM test_runs tr WHERE tr.id = coverage_reports.test_run_id),
                                  'RepositoryBaseline') = 'RepositoryBaseline'
                    THEN 'Solution' ELSE 'SourceProject' END,
                experiment_run_id = (SELECT mr.experiment_run_id FROM mutation_testing_reports mr
                                     WHERE mr.test_run_id = coverage_reports.test_run_id LIMIT 1),
                source_project_path = COALESCE((SELECT mr.source_project_path FROM mutation_testing_reports mr
                                                WHERE mr.test_run_id = coverage_reports.test_run_id LIMIT 1), ''),
                test_project_path = COALESCE((SELECT mr.test_project_path FROM mutation_testing_reports mr
                                              WHERE mr.test_run_id = coverage_reports.test_run_id LIMIT 1), ''),
                target_framework = COALESCE((SELECT mr.target_framework FROM mutation_testing_reports mr
                                             WHERE mr.test_run_id = coverage_reports.test_run_id LIMIT 1), '');
            """);

        migrationBuilder.CreateIndex(
            "IX_members_object_id_signature_origin_kind_origin_attempt_id",
            "members",
            new[] { "object_id", "signature", "origin_kind", "origin_attempt_id" });
        migrationBuilder.CreateIndex(
            "IX_coverage_reports_project_id_report_role_scope_kind",
            "coverage_reports",
            new[] { "project_id", "report_role", "scope_kind" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_members_object_id_signature_origin_kind_origin_attempt_id", "members");
        migrationBuilder.DropIndex("IX_coverage_reports_project_id_report_role_scope_kind", "coverage_reports");
        migrationBuilder.DropColumn("signature", "members");
        migrationBuilder.DropColumn("origin_kind", "members");
        migrationBuilder.DropColumn("origin_attempt_id", "members");
        migrationBuilder.DropColumn("report_role", "test_runs");
        migrationBuilder.DropColumn("scope_kind", "coverage_reports");
        migrationBuilder.DropColumn("report_role", "coverage_reports");
        migrationBuilder.DropColumn("experiment_run_id", "coverage_reports");
        migrationBuilder.DropColumn("source_project_path", "coverage_reports");
        migrationBuilder.DropColumn("test_project_path", "coverage_reports");
        migrationBuilder.DropColumn("target_framework", "coverage_reports");
        migrationBuilder.DropColumn("report_role", "mutation_testing_reports");
        migrationBuilder.DropColumn("coverage_before", "generated_test_executions");
    }
}
