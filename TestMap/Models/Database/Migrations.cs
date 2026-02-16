namespace TestMap.Models.Database;

public static class Migrations
{
    public static readonly string Schema = @"
-- Tables

CREATE TABLE IF NOT EXISTS projects (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    owner TEXT NOT NULL,
    repo_name TEXT NOT NULL,
    directory_path TEXT NOT NULL,
    web_url TEXT,
    database_path TEXT,
    last_analyzed_commit TEXT,
    content_hash TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_projects_hash ON projects(content_hash);

CREATE TABLE IF NOT EXISTS analysis_solutions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    solution_path TEXT NOT NULL,
    content_hash TEXT,
    FOREIGN KEY (project_id) REFERENCES projects(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_analysis_solutions_hash ON analysis_solutions(content_hash);

CREATE TABLE IF NOT EXISTS analysis_projects (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    solution_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    project_path TEXT NOT NULL,
    target_framework TEXT,
    content_hash TEXT,
    FOREIGN KEY (solution_id) REFERENCES analysis_solutions(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_analysis_projects_hash ON analysis_projects(content_hash);


CREATE TABLE IF NOT EXISTS source_files (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    analysis_project_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    namespace TEXT,
    name TEXT,
    language TEXT,
    meta_data TEXT,
    usings TEXT,
    path TEXT,
    content_hash TEXT,
    FOREIGN KEY (analysis_project_id) REFERENCES analysis_projects(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_source_files_hash ON source_files(content_hash);

CREATE TABLE IF NOT EXISTS imports (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    import_name TEXT NOT NULL,
    import_path TEXT NOT NULL,
    full_string TEXT,
    is_local BOOLEAN,
    FOREIGN KEY (file_id) REFERENCES source_files(id)
);

CREATE TABLE IF NOT EXISTS classes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    name TEXT NOT NULL,
    visibility TEXT,
    modifiers TEXT,
    attributes TEXT,
    full_string TEXT,
    doc_string TEXT,
    is_test_class BOOLEAN,
    testing_framework TEXT,
    location_start_lin_no INTEGER,
    location_body_start INTEGER,
    location_body_end INTEGER,
    location_end_lin_no INTEGER,
    content_hash TEXT,
    FOREIGN KEY (file_id) REFERENCES source_files(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_classes_hash ON classes(content_hash);

CREATE TABLE IF NOT EXISTS methods (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    class_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    name TEXT NOT NULL,
    visibility TEXT,
    modifiers TEXT,
    attributes TEXT,
    full_string TEXT,
    doc_string TEXT,
    is_test_method BOOLEAN,
    testing_framework TEXT,
    test_type TEXT,
    location_start_lin_no INTEGER,
    location_body_start INTEGER,
    location_body_end INTEGER,
    location_end_lin_no INTEGER,
    content_hash TEXT,
    FOREIGN KEY (class_id) REFERENCES classes(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_methods_hash ON methods(content_hash);

CREATE TABLE IF NOT EXISTS invocations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    target_method_id INTEGER NOT NULL,
    source_method_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    is_assertion BOOLEAN,
    full_string TEXT,
    content_hash TEXT,
    location_start_lin_no INTEGER,
    location_body_start INTEGER,
    location_body_end INTEGER,
    location_end_lin_no INTEGER
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_invocations_hash ON invocations(content_hash);

CREATE TABLE IF NOT EXISTS properties (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    class_id INTEGER NOT NULL,
    guid TEXT NOT NULL,
    name TEXT NOT NULL,
    visibility TEXT,
    modifiers TEXT,
    attributes TEXT,
    full_string TEXT,
    content_hash TEXT,
    location_start_lin_no INTEGER,
    location_body_start INTEGER,
    location_body_end INTEGER,
    location_end_lin_no INTEGER,
    FOREIGN KEY (class_id) REFERENCES classes(id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_properties_hash ON properties(content_hash);

CREATE TABLE IF NOT EXISTS test_runs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT,
    run_date TEXT NOT NULL,
    result TEXT NOT NULL,
    coverage INTEGER,
    log_path TEXT,
    error TEXT,
    report TEXT
);

CREATE TABLE IF NOT EXISTS test_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    method_id INTEGER NOT NULL,
    run_id TEXT,
    run_date TEXT,
    test_name TEXT,
    test_outcome TEXT,
    test_duration TEXT,
    error_message TEXT,
    FOREIGN KEY (method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS generated_tests (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id INTEGER NOT NULL,
    original_method_id INTEGER NOT NULL,
    test_method_id INTEGER NOT NULL,
    filepath TEXT,
    provider TEXT,
    model TEXT,
    strategy TEXT,
    prompt_token_count INTEGER,
    generation_duration INTEGER,
    generated_body TEXT,
    FOREIGN KEY (test_run_id) REFERENCES test_runs(id),
    FOREIGN KEY (original_method_id) REFERENCES methods(id)
    FOREIGN KEY (test_method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS coverage_reports (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id INTEGER,
    timestamp INTEGER,
    line_rate REAL,
    branch_rate REAL,
    complexity INTEGER,
    lines_covered INTEGER,
    lines_valid INTEGER,
    branches_covered INTEGER,
    branches_valid INTEGER,
    full_report_xml TEXT
);

CREATE TABLE IF NOT EXISTS mutation_reports (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id TEXT,
    timestamp INTEGER,
    project_root TEXT,
    schema_version TEXT,
    full_report_json TEXT
);

CREATE TABLE IF NOT EXISTS file_mutation_result (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    mutation_report_id INTEGER,
    source_file_id INTEGER,
    language TEXT,
    mutation_score REAL,
    FOREIGN KEY (mutation_report_id) REFERENCES mutation_reports(id),
    FOREIGN KEY (source_file_id) REFERENCES source_files(id)
);

CREATE TABLE IF NOT EXISTS mutants (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_mutation_result_id INTEGER,
    method_id INTEGER,
    mutant_id TEXT,
    mutator_name TEXT,
    replacement TEXT,
    start_line INTEGER,
    start_column INTEGER,
    end_line INTEGER,
    end_column INTEGER,
    status TEXT,
    status_reason TEXT,
    static_status TEXT,
    FOREIGN KEY (file_mutation_result_id) REFERENCES file_mutation_result(id),
    FOREIGN KEY (method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS mutant_test_map (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    mutant_id INTEGER NOT NULL,
    test_method_id INTEGER NOT NULL,
    interaction_type TEXT NOT NULL,
    FOREIGN KEY (mutant_id) REFERENCES mutants(id),
    FOREIGN KEY (test_method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS lizard_file_code_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id TEXT,
    file_id INTEGER,
    ncss INTEGER,
    ccn INTEGER,
    function_count INTEGER,
    FOREIGN KEY (file_id) REFERENCES source_files(id)
);

CREATE TABLE IF NOT EXISTS lizard_function_code_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id TEXT,
    method_id INTEGER,
    ncss INTEGER,
    ccn INTEGER,
    FOREIGN KEY (method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS test_smells (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    description TEXT NULL
);

INSERT OR IGNORE INTO test_smells (name, description) VALUES
('EmptyTestSmell', NULL),
('ConditionalTestSmell', NULL),
('CyclomaticComplexityTestSmell', NULL),
('ExpectedExceptionTestSmell', NULL),
('AssertionRouletteTestSmell', NULL),
('UnknownTestSmell', NULL),
('RedundantPrintTestSmell', NULL),
('SleepyTestSmell', NULL),
('IgnoreTestSmell', NULL),
('RedundantAssertionTestSmell', NULL),
('DuplicateAssertionTestSmell', NULL),
('MagicNumberTestSmell', NULL),
('EagerTestSmell', NULL),
('BoolInAssertEqualSmell', NULL),
('EqualInAssertSmell', NULL),
('SensitiveEqualitySmell', NULL),
('ConstructorInitializationTestSmell', NULL),
('ObscureInLineSetUpSmell', NULL);

CREATE TABLE IF NOT EXISTS method_test_smells (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    method_id INTEGER NOT NULL REFERENCES methods(id),
    test_smell_id INTEGER NOT NULL REFERENCES test_smells(id),
    status TEXT NOT NULL  -- e.g. 'Found', 'Not Found'
);

CREATE TABLE IF NOT EXISTS package_coverage (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    coverage_report_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    line_rate REAL,
    branch_rate REAL,
    complexity INTEGER,
    FOREIGN KEY (coverage_report_id) REFERENCES coverage_reports(id)
);

CREATE TABLE IF NOT EXISTS class_coverage (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    package_coverage_id INTEGER NOT NULL,
    class_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    line_rate REAL,
    branch_rate REAL,
    complexity INTEGER,
    FOREIGN KEY (class_id) REFERENCES classes(id),
    FOREIGN KEY (package_coverage_id) REFERENCES package_coverage(id)
);

CREATE TABLE IF NOT EXISTS method_coverage (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    class_coverage_id INTEGER NOT NULL,
    method_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    line_rate REAL,
    branch_rate REAL,
    complexity INTEGER,
    FOREIGN KEY (class_coverage_id) REFERENCES class_coverage(id),
    FOREIGN KEY (method_id) REFERENCES methods(id)
);

CREATE VIEW IF NOT EXISTS v_baseline_uncovered_tested_methods AS
WITH UncoveredMethods AS (
    SELECT 
        mc.method_id, 
        mc.line_rate, 
        mc.branch_rate,
        m.class_id, 
        m.name AS method_name,
        m.full_string AS method_body,
        c.name AS class_name,
        sf.analysis_project_id
    FROM method_coverage mc
    JOIN methods m ON mc.method_id = m.id
    JOIN classes c ON m.class_id = c.id
    JOIN source_files sf ON c.file_id = sf.id
    JOIN analysis_projects ap ON sf.analysis_project_id = ap.id
    WHERE mc.line_rate != 1
),
MethodTestMapping AS (
    SELECT
        sm.id AS source_method_id,
        tm.id AS test_method_id,
        tm.name AS test_method_name,
        tm.full_string AS test_method_body,
        tc.id AS test_class_id,
        tc.name AS test_class_name,
        tc.testing_framework,
        tc.location_start_lin_no AS test_class_lin_start,
        tc.location_body_start AS test_class_body_start,
        tc.location_end_lin_no AS test_class_lin_end,
        tc.location_body_end AS test_class_body_end,
        tf.path AS test_file_path,
        tf.usings AS test_dependencies
    FROM invocations i
    JOIN methods sm ON i.source_method_id = sm.id
    JOIN methods tm ON i.target_method_id = tm.id AND tm.is_test_method = 1
    JOIN classes tc ON tm.class_id = tc.id
    JOIN source_files tf ON tc.file_id = tf.id
)
SELECT 
    um.method_id,
    um.method_name,
    um.method_body,
    um.line_rate,
    um.branch_rate,
    um.class_id,
    um.class_name,
    CASE WHEN mt.test_method_id IS NOT NULL THEN 'Has tests covering method' ELSE 'No tests covering method' END AS coverage_status,
    mt.test_method_id,
    mt.test_method_name,
    mt.test_method_body,
    mt.test_class_id,
    mt.test_class_name,
    mt.testing_framework,
    mt.test_class_lin_start,
    mt.test_class_body_start,
    mt.test_class_lin_end,
    mt.test_class_body_end,
    mt.test_file_path,
    mt.test_dependencies,
    asol.solution_path AS solution_file_path
FROM UncoveredMethods um
LEFT JOIN MethodTestMapping mt 
    ON um.method_id = mt.source_method_id
LEFT JOIN analysis_projects ap 
    ON ap.id = um.analysis_project_id
LEFT JOIN analysis_solutions asol 
    ON asol.id = ap.solution_id
WHERE test_method_id IS NOT NULL
ORDER BY um.method_name, mt.test_method_name;
";
}