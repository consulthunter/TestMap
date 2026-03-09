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
    is_generated BOOLEAN,
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
    gen_test_method_id INTEGER NOT NULL,
    filepath TEXT,
    provider TEXT,
    model TEXT,
    strategy TEXT,
    prompt_token_count INTEGER,
    generation_duration INTEGER,
    FOREIGN KEY (test_run_id) REFERENCES test_runs(id),
    FOREIGN KEY (original_method_id) REFERENCES methods(id),
    FOREIGN KEY (test_method_id) REFERENCES methods(id),
    FOREIGN KEY (gen_test_method_id) REFERENCES methods(id)
);

CREATE TABLE IF NOT EXISTS coverage_reports (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    test_run_id TEXT,
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

CREATE VIEW IF NOT EXISTS v_method_coverage_agg AS
SELECT
    method_id,
    MAX(line_rate) AS max_line_rate,    -- best line coverage achieved
    MAX(branch_rate) AS max_branch_rate, -- best branch coverage achieved
    AVG(line_rate) AS avg_line_rate,    -- average line coverage across reports
    AVG(branch_rate) AS avg_branch_rate -- average branch coverage
FROM method_coverage
GROUP BY method_id;

CREATE VIEW IF NOT EXISTS v_method_coverage_baseline_agg AS
SELECT
    mc.method_id,
    mc.line_rate   AS max_line_rate,
    mc.branch_rate AS max_branch_rate,
    mc.line_rate   AS avg_line_rate,
    mc.branch_rate AS avg_branch_rate
FROM method_coverage mc
JOIN class_coverage cc 
    ON mc.class_coverage_id = cc.id
JOIN package_coverage pc 
    ON cc.package_coverage_id = pc.id
JOIN coverage_reports cr 
    ON pc.coverage_report_id = cr.id
JOIN v_baseline_report br
    ON cr.id = br.id;

CREATE VIEW IF NOT EXISTS v_candidate_covered_methods AS
SELECT 
    m.id AS method_id,
    m.name AS method_name,
    m.full_string AS method_body,
    mc_agg.max_line_rate AS line_rate,
    mc_agg.max_branch_rate AS branch_rate,
    c.id AS class_id,
    c.name AS class_name,
    sf.analysis_project_id
FROM methods m
JOIN classes c ON m.class_id = c.id
JOIN source_files sf ON c.file_id = sf.id
JOIN v_method_coverage_baseline_agg mc_agg 
    ON mc_agg.method_id = m.id
WHERE (mc_agg.max_line_rate IS NOT NULL OR mc_agg.max_branch_rate IS NOT NULL)
AND (mc_agg.max_line_rate != 1 OR mc_agg.max_branch_rate != 1);

CREATE VIEW IF NOT EXISTS v_method_test_mapping AS
SELECT
    i.source_method_id,
    tm.id AS test_method_id,
    tm.name AS test_method_name,
    tm.full_string AS test_method_body,
    tc.id AS test_class_id,
    tc.name AS test_class_name,
    tc.full_string AS test_class_body,
    tc.testing_framework,
    tc.location_start_lin_no AS test_class_lin_start,
    tc.location_body_start AS test_class_body_start,
    tc.location_end_lin_no AS test_class_lin_end,
    tc.location_body_end AS test_class_body_end,
    tf.path AS test_file_path,
    tf.usings AS test_dependencies,
    tf.namespace AS test_namespace
FROM invocations i
JOIN methods tm ON i.target_method_id = tm.id AND tm.is_test_method = 1
JOIN methods sm ON i.source_method_id = sm.id
JOIN classes tc ON tm.class_id = tc.id
JOIN classes sc ON sm.class_id = sc.id
JOIN source_files tf ON tc.file_id = tf.id
WHERE i.source_method_id != 0
AND sc.is_test_class = 0;

CREATE VIEW IF NOT EXISTS v_baseline_uncovered_tested_methods AS
SELECT DISTINCT
    cm.method_id,
    cm.method_name,
    cm.method_body,
    cm.line_rate,
    cm.branch_rate,
    cm.class_id,
    cm.class_name,
    'Has tests covering method' AS coverage_status,
    mt.test_method_id,
    mt.test_method_name,
    mt.test_method_body,
    mt.test_class_id,
    mt.test_class_name,
    mt.test_class_body,
    mt.testing_framework,
    mt.test_class_lin_start,
    mt.test_class_body_start,
    mt.test_class_lin_end,
    mt.test_class_body_end,
    mt.test_file_path,
    mt.test_dependencies,
    mt.test_namespace,
    asol.solution_path AS solution_file_path
FROM v_candidate_covered_methods cm
JOIN v_method_test_mapping mt 
    ON cm.method_id = mt.source_method_id
LEFT JOIN analysis_projects ap 
    ON ap.id = cm.analysis_project_id
LEFT JOIN analysis_solutions asol 
    ON asol.id = ap.solution_id;


CREATE VIEW IF NOT EXISTS v_baseline_report AS
SELECT cr.id
FROM coverage_reports cr
WHERE cr.test_run_id LIKE '%baseline%'
ORDER BY cr.timestamp DESC
LIMIT 1;

CREATE VIEW IF NOT EXISTS v_baseline_coverage AS
SELECT
    mc.method_id,
    mc.line_rate  AS baseline_line_rate,
    mc.branch_rate AS baseline_branch_rate,
    mc.complexity AS baseline_complexity
FROM v_baseline_report lbr
JOIN package_coverage pc
    ON pc.coverage_report_id = lbr.id
JOIN class_coverage cc
    ON cc.package_coverage_id = pc.id
JOIN method_coverage mc
    ON mc.class_coverage_id = cc.id;

CREATE VIEW IF NOT EXISTS v_gen_test_coverage AS
SELECT
    gt.original_method_id AS method_id,
    gt.test_method_id,
	gt.gen_test_method_id,
    tr.id AS test_run_db_id,
    mc.line_rate  AS generated_line_rate,
    mc.branch_rate AS generated_branch_rate,
    mc.complexity AS generated_complexity
FROM generated_tests gt
JOIN test_runs tr
    ON tr.id = gt.test_run_id
JOIN coverage_reports cr
    ON cr.test_run_id = tr.run_id
JOIN package_coverage pc
    ON pc.coverage_report_id = cr.id
JOIN class_coverage cc 
    ON cc.package_coverage_id = pc.id
JOIN method_coverage mc
    ON mc.class_coverage_id = cc.id
   AND mc.method_id = gt.original_method_id;

CREATE VIEW IF NOT EXISTS v_latest_test_outcome AS
SELECT
    tr_db.id AS test_run_db_id,
    tr.method_id AS test_method_id,
    tr.test_outcome,
    tr.test_duration,
    tr.error_message
FROM test_results tr
JOIN test_runs tr_db
    ON tr_db.run_id = tr.run_id;

CREATE VIEW IF NOT EXISTS v_test_agg AS
SELECT
    gt.id                           AS generated_test_id,
    gt.test_run_id,
    gt.provider,
    gt.model,
    gt.strategy,
    gt.prompt_token_count,
    gt.generation_duration,

    m.id                            AS method_id,
    m.name                          AS method_name,
    c.name                          AS class_name,

    AVG(bc.baseline_line_rate)      AS baseline_line_rate,
    AVG(gr.generated_line_rate)     AS generated_line_rate,

    AVG(bc.baseline_branch_rate)    AS baseline_branch_rate,
    AVG(gr.generated_branch_rate)   AS generated_branch_rate,

    AVG(bc.baseline_complexity)     AS baseline_complexity,
    AVG(gr.generated_complexity)    AS generated_complexity,

    lto.test_outcome,
    lto.test_duration,
    lto.error_message

FROM generated_tests gt
JOIN methods m 
    ON gt.original_method_id = m.id
JOIN classes c 
    ON m.class_id = c.id
LEFT JOIN v_baseline_coverage bc 
    ON bc.method_id = m.id
LEFT JOIN v_gen_test_coverage gr 
    ON gr.method_id = m.id
    AND gr.test_run_db_id = gt.test_run_id
LEFT JOIN v_latest_test_outcome lto
    ON lto.test_method_id = gt.gen_test_method_id
    AND lto.test_run_db_id = gt.test_run_id
GROUP BY 
    gt.id, gt.test_run_id, gt.provider, gt.model, gt.strategy, gt.prompt_token_count, gt.generation_duration,
    m.id, m.name, c.name,
    lto.test_outcome, lto.test_duration, lto.error_message;

CREATE VIEW IF NOT EXISTS v_generated_test_evaluation AS
SELECT
    *,
    -- calculate deltas
    (generated_line_rate - baseline_line_rate) AS line_rate_delta,
    (generated_branch_rate - baseline_branch_rate) AS branch_rate_delta,
    ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate)) / 2 AS coverage_delta,
    -- evaluation category
    CASE
        WHEN (test_outcome IN ('Passed', 'pass') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 > 0)
            THEN 'Approved / Improved Test'
        WHEN (test_outcome IN ('Passed', 'pass') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 = 0)
            THEN 'Benign Test'
        WHEN (test_outcome IN ('Failed', 'fail', 'Error') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 > 0)
            THEN 'Candidate Test'
        ELSE 'Failed Test'
    END AS evaluation_category,
    -- UI color
    CASE
        WHEN (test_outcome IN ('Passed', 'pass') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 > 0)
            THEN 'green'
        WHEN (test_outcome IN ('Passed', 'pass') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 = 0)
            THEN 'gray'
        WHEN (test_outcome IN ('Failed', 'fail', 'Error') AND ((generated_line_rate - baseline_line_rate) + (generated_branch_rate - baseline_branch_rate))/2 > 0)
            THEN 'yellow'
        ELSE 'red'
    END AS ui_color
FROM v_test_agg;

CREATE VIEW IF NOT EXISTS v_lizard_test_metric_comparison AS
SELECT
    gt.id AS generated_test_id,
    gt.test_run_id,

    -- baseline test
    gt.test_method_id              AS baseline_test_method_id,
    bm.ncss                        AS baseline_ncss,
    bm.ccn                         AS baseline_ccn,

    -- generated test
    gt.gen_test_method_id          AS generated_test_method_id,
    gm.ncss                        AS generated_ncss,
    gm.ccn                         AS generated_ccn,

    -- deltas
    (gm.ncss - bm.ncss)            AS ncss_delta,
    (gm.ccn - bm.ccn)              AS ccn_delta,

    -- relative ratios
    CASE
        WHEN bm.ncss > 0 THEN 1.0 * gm.ncss / bm.ncss
        ELSE NULL
    END AS ncss_ratio,

    CASE
        WHEN bm.ccn > 0 THEN 1.0 * gm.ccn / bm.ccn
        ELSE NULL
    END AS ccn_ratio

FROM generated_tests gt

LEFT JOIN lizard_function_code_metrics bm
    ON bm.method_id = gt.test_method_id
   AND bm.test_run_id = (
        SELECT tr.run_id
        FROM test_runs tr
        WHERE tr.id = gt.test_run_id
   )

LEFT JOIN lizard_function_code_metrics gm
    ON gm.method_id = gt.gen_test_method_id
   AND gm.test_run_id = (
        SELECT tr.run_id
        FROM test_runs tr
        WHERE tr.id = gt.test_run_id
   );

CREATE VIEW IF NOT EXISTS v_test_smell_ratio AS
SELECT
    method_id,
    SUM(CASE WHEN status = 'Found' THEN 1 ELSE 0 END) AS smells_found,
    SUM(CASE WHEN status = 'Not Found' THEN 1 ELSE 0 END) AS smells_not_found,
    COUNT(*) AS total_smells,
    CASE
        WHEN COUNT(*) > 0
        THEN 1.0 * SUM(CASE WHEN status = 'Found' THEN 1 ELSE 0 END) / COUNT(*)
        ELSE NULL
    END AS smell_ratio
FROM method_test_smells
GROUP BY method_id;

CREATE VIEW IF NOT EXISTS v_generated_vs_baseline_smell_comparison AS
SELECT
    gt.id AS generated_test_id,
    gt.test_run_id,

    -- baseline
    gt.test_method_id AS baseline_test_method_id,
    bs.smells_found   AS baseline_smells_found,
    bs.total_smells   AS baseline_total_smells,
    bs.smell_ratio    AS baseline_smell_ratio,

    -- generated
    gt.gen_test_method_id AS generated_test_method_id,
    gs.smells_found       AS generated_smells_found,
    gs.total_smells       AS generated_total_smells,
    gs.smell_ratio        AS generated_smell_ratio,

    -- delta
    (gs.smell_ratio - bs.smell_ratio) AS smell_ratio_delta

FROM generated_tests gt

LEFT JOIN v_test_smell_ratio bs
    ON bs.method_id = gt.test_method_id

LEFT JOIN v_test_smell_ratio gs
    ON gs.method_id = gt.gen_test_method_id;

CREATE VIEW IF NOT EXISTS v_project_baseline_coverage AS
SELECT
    cr.id                     AS coverage_report_id,
    cr.test_run_id,
    cr.line_rate              AS baseline_line_rate,
    cr.branch_rate            AS baseline_branch_rate,
    cr.lines_covered,
    cr.lines_valid,
    cr.branches_covered,
    cr.branches_valid,
    cr.complexity             AS baseline_complexity
FROM coverage_reports cr
JOIN v_baseline_report br
    ON cr.id = br.id;

CREATE VIEW IF NOT EXISTS v_project_generated_coverage AS
SELECT
    cr.id              AS coverage_report_id,
    tr.id              AS test_run_db_id,
    cr.test_run_id,
    cr.line_rate       AS generated_line_rate,
    cr.branch_rate     AS generated_branch_rate,
    cr.lines_covered,
    cr.lines_valid,
    cr.branches_covered,
    cr.branches_valid,
    cr.complexity      AS generated_complexity
FROM coverage_reports cr
JOIN test_runs tr
    ON tr.run_id = cr.test_run_id
WHERE cr.test_run_id NOT LIKE '%baseline%';

CREATE VIEW IF NOT EXISTS v_project_coverage_comparison AS
SELECT
    pg.test_run_db_id,
    pg.coverage_report_id,

    -- baseline
    pb.baseline_line_rate,
    pb.baseline_branch_rate,
    pb.baseline_complexity,

    -- generated
    pg.generated_line_rate,
    pg.generated_branch_rate,
    pg.generated_complexity,

    -- deltas
    (pg.generated_line_rate - pb.baseline_line_rate)     AS line_rate_delta,
    (pg.generated_branch_rate - pb.baseline_branch_rate) AS branch_rate_delta,
    (pg.generated_complexity - pb.baseline_complexity)   AS complexity_delta,

    -- absolute coverage improvement (more correct than averaging rates)
    (pg.lines_covered - pb.lines_covered)               AS lines_covered_delta,
    (pg.branches_covered - pb.branches_covered)         AS branches_covered_delta

FROM v_project_generated_coverage pg
CROSS JOIN v_project_baseline_coverage pb;
";
}