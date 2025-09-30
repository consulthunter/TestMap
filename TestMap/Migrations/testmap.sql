-- Tables

CREATE TABLE IF NOT EXISTS projects (
                          id INTEGER PRIMARY KEY AUTOINCREMENT,
                          owner TEXT NOT NULL,
                          repo_name TEXT NOT NULL,
                          directory_path TEXT NOT NULL,
                          web_url TEXT,
                          database_path TEXT,
                          last_analyzed_commit TEXT,
                          created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS analysis_solutions (
                           id INTEGER PRIMARY KEY AUTOINCREMENT,
                           project_id INTEGER NOT NULL,
                           guid TEXT NOT NULL,
                           solution_path TEXT NOT NULL,
                           FOREIGN KEY (project_id) REFERENCES projects(id)
);

CREATE TABLE IF NOT EXISTS analysis_projects(
                                      id INTEGER PRIMARY KEY AUTOINCREMENT,
                                      solution_id INTEGER NOT NULL,
                                      guid TEXT NOT NULL,
                                      project_path TEXT NOT NULL,
                                      target_framework TEXT,
                                      FOREIGN KEY (solution_id) REFERENCES analysis_solutions(id)
);

CREATE TABLE IF NOT EXISTS source_packages(
                                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                analysis_project_id INTEGER NOT NULL,
                                                guid TEXT NOT NULL,
                                                package_name TEXT NOT NULL,
                                                package_path TEXT NOT NULL,
                                                FOREIGN KEY (analysis_project_id) REFERENCES analysis_projects(id)
    );



CREATE TABLE IF NOT EXISTS source_files (
                              id INTEGER PRIMARY KEY AUTOINCREMENT,
                              package_id INTEGER NOT NULL,
                              guid TEXT NOT NULL,
                              namespace TEXT,
                              name TEXT,
                              language TEXT,
                              meta_data JSON,
                              usings JSON,
                              path TEXT,
                              FOREIGN KEY (package_id) REFERENCES source_packages(id)
);

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
                         visibility JSON,
                         modifiers JSON,
                         attributes JSON,
                         full_string TEXT,
                         doc_string TEXT,
                         is_test_class BOOLEAN,
                         testing_framework TEXT,
                         location_start_lin_no INTEGER,
                         location_body_start INTEGER,
                         location_body_end INTEGER,
                         location_end_lin_no INTEGER,
                         FOREIGN KEY (file_id) REFERENCES source_files(id)
);

CREATE TABLE IF NOT EXISTS methods (
                         id INTEGER PRIMARY KEY AUTOINCREMENT,
                         class_id INTEGER NOT NULL,
                         guid TEXT NOT NULL,
                         name TEXT NOT NULL,
                         visibility JSON,
                         modifiers JSON,
                         attributes JSON,
                         full_string TEXT,
                         doc_string TEXT,
                         is_test_method BOOLEAN,
                         testing_framework TEXT,
                         test_type TEXT,
                         location_start_lin_no INTEGER,
                         location_body_start INTEGER,
                         location_body_end INTEGER,
                         location_end_lin_no INTEGER,
                         FOREIGN KEY (class_id) REFERENCES classes(id)
);

CREATE TABLE IF NOT EXISTS invocations (
                                          id INTEGER PRIMARY KEY AUTOINCREMENT,
                                          target_method_id INTEGER NOT NULL,
                                          source_method_id INTEGER NOT NULL,
                                          guid TEXT NOT NULL,
                                          is_assertion BOOLEAN,
                                          full_string TEXT,
                                          location_start_lin_no INTEGER,
                                          location_body_start INTEGER,
                                          location_body_end INTEGER,
                                          location_end_lin_no INTEGER,
                                          FOREIGN KEY (target_method_id) REFERENCES methods(id)
                                          FOREIGN KEY (source_method_id) REFERENCES methods(id)
    );

CREATE TABLE IF NOT EXISTS properties (
                                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                                       class_id INTEGER NOT NULL,
                                       guid TEXT NOT NULL,
                                       name TEXT NOT NULL,
                                       visibility JSON,
                                       modifiers JSON,
                                       attributes JSON,
                                       full_string TEXT,
                                       location_start_lin_no INTEGER,
                                       location_body_start INTEGER,
                                       location_body_end INTEGER,
                                       location_end_lin_no INTEGER,
                                       FOREIGN KEY (class_id) REFERENCES classes(id)
    );

CREATE TABLE IF NOT EXISTS test_runs (
                           id INTEGER PRIMARY KEY AUTOINCREMENT, 
                           run_id TEXT,
                           run_date TEXT NOT NULL,   
                           result TEXT NOT NULL,     
                           coverage INTEGER, 
                           log_path TEXT,           
                           error TEXT
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
                                 test_run_id TEXT NOT NULL,
                                 original_method_id TEXT NOT NULL,
                                 generated_body TEXT,
                                 FOREIGN KEY (test_run_id) REFERENCES test_runs(id),
                                 FOREIGN KEY (original_method_id) REFERENCES methods(id)
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
                                  FOREIGN KEY (test_run_id) REFERENCES test_results(run_id)
);

CREATE TABLE IF NOT EXISTS package_coverage (
                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  coverage_report_id INTEGER NOT NULL,
                                  package_id INTEGER NOT NULL,
                                  name TEXT NOT NULL,
                                  line_rate REAL,
                                  branch_rate REAL,
                                  complexity INTEGER,
                                  FOREIGN KEY (package_id) REFERENCES source_packages(id),
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
