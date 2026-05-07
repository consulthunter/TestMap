using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TestMap.Persistence.Ef;

public class SqliteSchemaCompatibilityService
{
    public async Task EnsureCompatibleAsync(TestMapDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            if (connection is not SqliteConnection sqliteConnection) return;

            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "error_message",
                "ALTER TABLE generation_attempts ADD COLUMN error_message TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "failure_category",
                "ALTER TABLE generation_attempts ADD COLUMN failure_category TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "failure_kind",
                "ALTER TABLE generation_attempts ADD COLUMN failure_kind TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "failure_stage",
                "ALTER TABLE generation_attempts ADD COLUMN failure_stage TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "objective",
                "ALTER TABLE generation_attempts ADD COLUMN objective TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "generation_approach",
                "ALTER TABLE generation_attempts ADD COLUMN generation_approach TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "metrics_path",
                "ALTER TABLE generation_attempts ADD COLUMN metrics_path TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "context_mode",
                "ALTER TABLE generation_attempts ADD COLUMN context_mode TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "budget_mode",
                "ALTER TABLE generation_attempts ADD COLUMN budget_mode TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "ablation_variant_id",
                "ALTER TABLE generation_attempts ADD COLUMN ablation_variant_id TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "step_config_json",
                "ALTER TABLE generation_attempts ADD COLUMN step_config_json TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "effective_profile_json",
                "ALTER TABLE generation_attempts ADD COLUMN effective_profile_json TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "effective_profile_hash",
                "ALTER TABLE generation_attempts ADD COLUMN effective_profile_hash TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "temperature",
                "ALTER TABLE generation_attempts ADD COLUMN temperature REAL NOT NULL DEFAULT 0;");
            await EnsureColumnAsync(sqliteConnection, "generation_attempts", "rule_decision_json",
                "ALTER TABLE generation_attempts ADD COLUMN rule_decision_json TEXT NOT NULL DEFAULT '';");

            await EnsureColumnAsync(sqliteConnection, "experiment_runs", "objective",
                "ALTER TABLE experiment_runs ADD COLUMN objective TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "experiment_runs", "candidate_selection_strategy",
                "ALTER TABLE experiment_runs ADD COLUMN candidate_selection_strategy TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "experiment_runs", "results_file_path",
                "ALTER TABLE experiment_runs ADD COLUMN results_file_path TEXT NOT NULL DEFAULT '';");

            await EnsureColumnAsync(sqliteConnection, "generation_steps", "status",
                "ALTER TABLE generation_steps ADD COLUMN status TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_steps", "skip_reason",
                "ALTER TABLE generation_steps ADD COLUMN skip_reason TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "generation_steps", "input_tokens",
                "ALTER TABLE generation_steps ADD COLUMN input_tokens INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "generation_steps", "output_tokens",
                "ALTER TABLE generation_steps ADD COLUMN output_tokens INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "generation_steps", "rule_decision_json",
                "ALTER TABLE generation_steps ADD COLUMN rule_decision_json TEXT NOT NULL DEFAULT '';");

            await EnsureColumnAsync(sqliteConnection, "members", "test_metadata_source",
                "ALTER TABLE members ADD COLUMN test_metadata_source TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "members", "test_metadata_confidence",
                "ALTER TABLE members ADD COLUMN test_metadata_confidence REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "members", "test_metadata_prompt_version",
                "ALTER TABLE members ADD COLUMN test_metadata_prompt_version TEXT NOT NULL DEFAULT '';");

            await EnsureColumnAsync(sqliteConnection, "test_runs", "mutation_score",
                "ALTER TABLE test_runs ADD COLUMN mutation_score REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "baseline_mutation_score",
                "ALTER TABLE test_executions ADD COLUMN baseline_mutation_score REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "mutation_score_after",
                "ALTER TABLE test_executions ADD COLUMN mutation_score_after REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "mutation_score_delta",
                "ALTER TABLE test_executions ADD COLUMN mutation_score_delta REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "validation_result_json",
                "ALTER TABLE test_executions ADD COLUMN validation_result_json TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "accepted",
                "ALTER TABLE test_executions ADD COLUMN accepted INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "acceptance_reason",
                "ALTER TABLE test_executions ADD COLUMN acceptance_reason TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "validation_rule_decision_json",
                "ALTER TABLE test_executions ADD COLUMN validation_rule_decision_json TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "test_executions", "classification_rule_decision_json",
                "ALTER TABLE test_executions ADD COLUMN classification_rule_decision_json TEXT NOT NULL DEFAULT '';");

            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_driven_score",
                "ALTER TABLE candidate_methods ADD COLUMN metric_driven_score REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "expected_metric_delta",
                "ALTER TABLE candidate_methods ADD COLUMN expected_metric_delta REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_confidence",
                "ALTER TABLE candidate_methods ADD COLUMN metric_confidence REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_feasibility",
                "ALTER TABLE candidate_methods ADD COLUMN metric_feasibility REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_estimated_cost",
                "ALTER TABLE candidate_methods ADD COLUMN metric_estimated_cost REAL NULL;");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_guardrail_status",
                "ALTER TABLE candidate_methods ADD COLUMN metric_guardrail_status TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "candidate_methods", "metric_selection_reason",
                "ALTER TABLE candidate_methods ADD COLUMN metric_selection_reason TEXT NOT NULL DEFAULT '';");

            await EnsureInvocationsSupportExternalAssertionsAsync(sqliteConnection);
            await EnsureCoverageGapsTableAsync(sqliteConnection);
            await EnsureMutantsTableAsync(sqliteConnection);
            await EnsureMutantSurvivedTestsTableAsync(sqliteConnection);
            await EnsureCandidateMethodRiskScoresTableAsync(sqliteConnection);
            await EnsureExperimentMatrixWorkItemsTableAsync(sqliteConnection);
            await EnsureTestExecutionResultsTableAsync(sqliteConnection);
            await EnsureFlakyTestScoresTableAsync(sqliteConnection);
            await EnsureFlakyTestRerunResultsTableAsync(sqliteConnection);
            await EnsureRuleDefinitionsTableAsync(sqliteConnection);
            await EnsureRuleDecisionsTableAsync(sqliteConnection);
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "scope_kind",
                "ALTER TABLE rule_decisions ADD COLUMN scope_kind TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "scope_id",
                "ALTER TABLE rule_decisions ADD COLUMN scope_id TEXT NOT NULL DEFAULT '';");
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "experiment_run_id",
                "ALTER TABLE rule_decisions ADD COLUMN experiment_run_id INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "candidate_method_id",
                "ALTER TABLE rule_decisions ADD COLUMN candidate_method_id INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "generation_attempt_id",
                "ALTER TABLE rule_decisions ADD COLUMN generation_attempt_id INTEGER NULL;");
            await EnsureColumnAsync(sqliteConnection, "rule_decisions", "test_execution_id",
                "ALTER TABLE rule_decisions ADD COLUMN test_execution_id INTEGER NULL;");
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string alterSql)
    {
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await pragma.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return;

        await reader.CloseAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = alterSql;
        await alter.ExecuteNonQueryAsync();
    }

    private static async Task EnsureInvocationsSupportExternalAssertionsAsync(SqliteConnection connection)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'invocations';";
        var exists = await check.ExecuteScalarAsync();
        if (exists == null) return;

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(invocations);";
        await using var reader = await pragma.ExecuteReaderAsync();

        var invokedMemberIdIsRequired = false;
        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), "invoked_member_id", StringComparison.OrdinalIgnoreCase))
            {
                invokedMemberIdIsRequired = reader.GetInt32(3) == 1;
                break;
            }

        await reader.CloseAsync();

        if (!invokedMemberIdIsRequired) return;

        await using var rebuild = connection.CreateCommand();
        rebuild.CommandText = @"
PRAGMA foreign_keys=OFF;
CREATE TABLE invocations_rebuild (
    id INTEGER NOT NULL CONSTRAINT PK_invocations PRIMARY KEY AUTOINCREMENT,
    member_id INTEGER NOT NULL,
    invoked_member_id INTEGER NULL,
    is_assertion INTEGER NOT NULL,
    full_string TEXT NOT NULL,
    location TEXT NOT NULL,
    content_hash TEXT NOT NULL
);
INSERT INTO invocations_rebuild (id, member_id, invoked_member_id, is_assertion, full_string, location, content_hash)
    SELECT id, member_id, invoked_member_id, is_assertion, full_string, location, content_hash
    FROM invocations;
DROP TABLE invocations;
ALTER TABLE invocations_rebuild RENAME TO invocations;
PRAGMA foreign_keys=ON;";
        await rebuild.ExecuteNonQueryAsync();
    }

    private static async Task EnsureCoverageGapsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "coverage_gaps", @"
CREATE TABLE coverage_gaps (
    id INTEGER NOT NULL CONSTRAINT PK_coverage_gaps PRIMARY KEY AUTOINCREMENT,
    member_id INTEGER NOT NULL,
    coverage_report_id INTEGER NOT NULL,
    line_number INTEGER NOT NULL,
    hits INTEGER NOT NULL,
    is_branch INTEGER NOT NULL,
    condition_coverage TEXT NOT NULL,
    gap_kind TEXT NOT NULL,
    source_text TEXT NOT NULL,
    member_content_hash TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_coverage_gaps_coverage_report_id_member_id_line_number_gap_kind
    ON coverage_gaps (coverage_report_id, member_id, line_number, gap_kind);");
    }

    private static async Task EnsureMutantsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "mutants", @"
CREATE TABLE mutants (
    id INTEGER NOT NULL CONSTRAINT PK_mutants PRIMARY KEY AUTOINCREMENT,
    mutation_testing_report_id INTEGER NOT NULL,
    member_id INTEGER NULL,
    stryker_mutant_id TEXT NOT NULL,
    file_path TEXT NOT NULL,
    mutator_name TEXT NOT NULL,
    replacement TEXT NOT NULL,
    status TEXT NOT NULL,
    status_reason TEXT NOT NULL,
    is_static INTEGER NOT NULL,
    location TEXT NOT NULL,
    covered_by TEXT NOT NULL,
    killed_by TEXT NOT NULL,
    content_hash TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_mutants_content_hash ON mutants (content_hash);");
    }

    private static async Task EnsureMutantSurvivedTestsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "mutant_survived_tests", @"
CREATE TABLE mutant_survived_tests (
    id INTEGER NOT NULL CONSTRAINT PK_mutant_survived_tests PRIMARY KEY AUTOINCREMENT,
    mutant_id INTEGER NOT NULL,
    test_member_id INTEGER NULL,
    stryker_test_id TEXT NOT NULL,
    test_name TEXT NOT NULL,
    test_file_path TEXT NOT NULL,
    location TEXT NOT NULL,
    content_hash TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_mutant_survived_tests_content_hash ON mutant_survived_tests (content_hash);");
    }

    private static async Task EnsureCandidateMethodRiskScoresTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "candidate_method_risk_scores", @"
CREATE TABLE candidate_method_risk_scores (
    id INTEGER NOT NULL CONSTRAINT PK_candidate_method_risk_scores PRIMARY KEY AUTOINCREMENT,
    candidate_method_id INTEGER NULL,
    member_id INTEGER NOT NULL,
    risk_score REAL NOT NULL,
    factor_scores TEXT NOT NULL,
    weights TEXT NOT NULL,
    selection_reason TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IX_candidate_method_risk_scores_candidate_method_id ON candidate_method_risk_scores (candidate_method_id);
CREATE INDEX IX_candidate_method_risk_scores_member_id ON candidate_method_risk_scores (member_id);");
    }

    private static async Task EnsureExperimentMatrixWorkItemsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "experiment_matrix_work_items", @"
CREATE TABLE experiment_matrix_work_items (
    id INTEGER NOT NULL CONSTRAINT PK_experiment_matrix_work_items PRIMARY KEY AUTOINCREMENT,
    experiment_run_id INTEGER NOT NULL,
    candidate_method_id INTEGER NOT NULL,
    member_id INTEGER NOT NULL,
    stable_key TEXT NOT NULL,
    status TEXT NOT NULL,
    provider_name TEXT NOT NULL,
    model_name TEXT NOT NULL,
    objective TEXT NOT NULL,
    approach TEXT NOT NULL,
    metrics_path TEXT NOT NULL,
    context_mode TEXT NOT NULL,
    budget_mode TEXT NOT NULL,
    ablation_variant_id TEXT NOT NULL,
    step_config_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    started_at TEXT NULL,
    last_heartbeat_at TEXT NULL,
    completed_at TEXT NULL,
    error_message TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_experiment_matrix_work_items_stable_key ON experiment_matrix_work_items (stable_key);
CREATE INDEX IX_experiment_matrix_work_items_experiment_run_id_status ON experiment_matrix_work_items (experiment_run_id, status);");
    }

    private static async Task EnsureTestExecutionResultsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "test_execution_results", @"
CREATE TABLE test_execution_results (
    id INTEGER NOT NULL CONSTRAINT PK_test_execution_results PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL,
    solution_path TEXT NOT NULL,
    project_path TEXT NOT NULL,
    test_member_id INTEGER NULL,
    test_name TEXT NOT NULL,
    file_path TEXT NOT NULL,
    target_framework TEXT NOT NULL,
    execution_context TEXT NOT NULL,
    outcome TEXT NOT NULL,
    duration_ms REAL NOT NULL,
    error_message TEXT NULL,
    error_stack_trace TEXT NULL,
    started_at TEXT NULL,
    completed_at TEXT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IX_test_execution_results_run_id ON test_execution_results (run_id);
CREATE INDEX IX_test_execution_results_test_member_id ON test_execution_results (test_member_id);
CREATE INDEX IX_test_execution_results_test_name_file_path ON test_execution_results (test_name, file_path);");
    }

    private static async Task EnsureFlakyTestScoresTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "flaky_test_scores", @"
CREATE TABLE flaky_test_scores (
    id INTEGER NOT NULL CONSTRAINT PK_flaky_test_scores PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL,
    test_member_id INTEGER NULL,
    test_name TEXT NOT NULL,
    file_path TEXT NOT NULL,
    flakiness_score REAL NOT NULL,
    classification TEXT NOT NULL,
    factor_scores TEXT NOT NULL,
    weights TEXT NOT NULL,
    evidence TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IX_flaky_test_scores_run_id ON flaky_test_scores (run_id);
CREATE INDEX IX_flaky_test_scores_test_member_id ON flaky_test_scores (test_member_id);
CREATE INDEX IX_flaky_test_scores_test_name_file_path ON flaky_test_scores (test_name, file_path);");
    }

    private static async Task EnsureFlakyTestRerunResultsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "flaky_test_rerun_results", @"
CREATE TABLE flaky_test_rerun_results (
    id INTEGER NOT NULL CONSTRAINT PK_flaky_test_rerun_results PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL,
    test_execution_result_id INTEGER NOT NULL,
    attempt_number INTEGER NOT NULL,
    outcome TEXT NOT NULL,
    duration_ms REAL NOT NULL,
    error_message TEXT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IX_flaky_test_rerun_results_run_id ON flaky_test_rerun_results (run_id);
CREATE INDEX IX_flaky_test_rerun_results_test_execution_result_id ON flaky_test_rerun_results (test_execution_result_id);");
    }

    private static async Task EnsureRuleDefinitionsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "rule_definitions", @"
CREATE TABLE rule_definitions (
    id INTEGER NOT NULL CONSTRAINT PK_rule_definitions PRIMARY KEY AUTOINCREMENT,
    rule_id TEXT NOT NULL,
    rule_version TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    category TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_rule_definitions_rule_id_rule_version ON rule_definitions (rule_id, rule_version);
CREATE INDEX IX_rule_definitions_category ON rule_definitions (category);");
    }

    private static async Task EnsureRuleDecisionsTableAsync(SqliteConnection connection)
    {
        await EnsureTableAsync(connection, "rule_decisions", @"
CREATE TABLE rule_decisions (
    id INTEGER NOT NULL CONSTRAINT PK_rule_decisions PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL,
    csharp_project_id INTEGER NULL,
    scope_kind TEXT NOT NULL,
    scope_id TEXT NOT NULL,
    experiment_run_id INTEGER NULL,
    candidate_method_id INTEGER NULL,
    generation_attempt_id INTEGER NULL,
    test_execution_id INTEGER NULL,
    decision_kind TEXT NOT NULL,
    value TEXT NOT NULL,
    rule_id TEXT NOT NULL,
    rule_version TEXT NOT NULL,
    confidence TEXT NOT NULL,
    evidence TEXT NOT NULL,
    notes TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IX_rule_decisions_project_id ON rule_decisions (project_id);
CREATE INDEX IX_rule_decisions_csharp_project_id ON rule_decisions (csharp_project_id);
CREATE INDEX IX_rule_decisions_scope_kind_scope_id ON rule_decisions (scope_kind, scope_id);
CREATE INDEX IX_rule_decisions_experiment_run_id ON rule_decisions (experiment_run_id);
CREATE INDEX IX_rule_decisions_candidate_method_id ON rule_decisions (candidate_method_id);
CREATE INDEX IX_rule_decisions_generation_attempt_id ON rule_decisions (generation_attempt_id);
CREATE INDEX IX_rule_decisions_test_execution_id ON rule_decisions (test_execution_id);
CREATE INDEX IX_rule_decisions_rule_id_rule_version ON rule_decisions (rule_id, rule_version);");
    }

    private static async Task EnsureTableAsync(SqliteConnection connection, string tableName, string createSql)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{tableName}';";
        var exists = await check.ExecuteScalarAsync();
        if (exists != null) return;

        await using var create = connection.CreateCommand();
        create.CommandText = createSql;
        await create.ExecuteNonQueryAsync();
    }
}
