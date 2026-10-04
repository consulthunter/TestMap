"""Regressions for the SQLite/dataset review, including multi-run and empty cohorts."""
import sqlite3

import pandas as pd
import pytest

from analysis.build_evaluation_dataset import build_metric_diffs, build_mutation_operators, save_datasets
from analysis.build_evaluation_dataset import run as build_datasets
from analysis.audit_evaluation_data import audit_assertion_lineage
from analysis.build_msr_datasets import _CsvAppender
from analysis.export_training_dataset import build_mapping_rows
from analysis.files import read_result_grains, read_schema4_result_bundle
from analysis.msr_frames import load_mappings
from analysis.normalize import build_candidate_summary, build_paired_comparison
from analysis.export_failure_cases import build_failure_cases
from tests.test_assertion_lineage import _result_row, _assertion_row


def _attempt(aid, number=1, success=True, run="run-1", work="work-1", **extra):
    return dict(attempt_id=aid, generation_attempt_id=number, candidate_key="owner/repo|rev|target|10", lane="llm",
                producer="model", budget_mode="PassAt1RepairAt5", experiment_run_uid=run,
                resume_stable_key=work, attempt_number=number, validated_success=success,
                positive_impact=success, validated_evidence_positive=success,
                validated_low_impact=False, infrastructure_failure=False, **extra)


@pytest.mark.parametrize("identity", ["run", "work"])
def test_independent_repetitions_remain_separate(identity):
    args = {identity: "different"}
    attempts = pd.DataFrame([_attempt("a", success=False), _attempt("b", **args)])
    chains = build_candidate_summary(attempts)
    assert len(chains) == 2
    assert chains.any_positive_impact.mean() == .5
    cases = build_failure_cases(attempts, (), None)
    assert not cases.chain_succeeded.iloc[0]
    with pytest.raises(ValueError, match="run/work item"):
        build_paired_comparison(chains, "model")


def test_shuffled_fully_measured_vep_chain_keeps_terminal_components():
    attempts = pd.DataFrame([
        _attempt("a", success=False, cumulative_tokens=100, cumulative_input_tokens=80, cumulative_output_tokens=20),
        _attempt("b", number=2, cumulative_tokens=300, cumulative_input_tokens=240, cumulative_output_tokens=60),
    ])
    for frame in (attempts, attempts.iloc[::-1]):
        chain = build_candidate_summary(frame).iloc[0]
        assert chain.effective_tokens == 300
        assert chain.effective_input_tokens == 240
        assert chain.effective_output_tokens == 60
        assert chain.any_positive_impact
        assert not chain.first_attempt_positive_impact


def test_ambiguous_attempt_numbers_do_not_silently_select_a_cost():
    with pytest.raises(ValueError, match="unique"):
        build_candidate_summary(pd.DataFrame([_attempt("a"), _attempt("b")]))


@pytest.mark.parametrize("values,expected", [([None], None), ([False, None], None),
                                             ([True, None], True), ([False, False], False)])
def test_three_valued_chain_impact(values, expected):
    a = pd.DataFrame([_attempt(str(i), number=i + 1) for i in range(len(values))])
    a["positive_impact"] = pd.array(values, dtype="boolean")
    value = build_candidate_summary(a).any_positive_impact.iloc[0]
    assert pd.isna(value) if expected is None else value == expected


@pytest.fixture
def database(tmp_path):
    path = tmp_path / "analysis.db"
    con = sqlite3.connect(path)
    con.executescript("""
    CREATE TABLE projects(id INTEGER PRIMARY KEY,owner TEXT,repo_name TEXT,last_analyzed_commit TEXT);
    CREATE TABLE files(id INTEGER PRIMARY KEY,file_path TEXT);
    CREATE TABLE objects(id INTEGER PRIMARY KEY,file_id INTEGER,full_string TEXT);
    CREATE TABLE members(id INTEGER PRIMARY KEY,object_id INTEGER,name TEXT,full_string TEXT,start_line_number INTEGER,modifiers TEXT);
    CREATE TABLE source_test_mappings(id INTEGER PRIMARY KEY,project_id INTEGER,source_member_id INTEGER,test_member_id INTEGER,evidence_kind TEXT,is_grounded INTEGER,confidence REAL,access_path_strategy TEXT,path_length INTEGER,resolver_version TEXT);
    CREATE TABLE candidate_inventory(id INTEGER PRIMARY KEY,project_id INTEGER,source_member_id INTEGER,source_test_mapping_id INTEGER,risk_score REAL,metric_driven_score REAL,test_state TEXT,recommended_action TEXT);
    CREATE TABLE code_metrics(id INTEGER PRIMARY KEY,entity_id INTEGER,entity_type TEXT,cyclomatic_complexity INTEGER);
    CREATE TABLE test_smells(id INTEGER PRIMARY KEY,member_id INTEGER,smell_id TEXT);
    CREATE TABLE test_runs(id INTEGER PRIMARY KEY,project_id INTEGER);
    CREATE TABLE coverage_reports(id INTEGER PRIMARY KEY,project_id INTEGER,test_run_id INTEGER,has_usable_coverage INTEGER,line_counts_available INTEGER);
    CREATE TABLE member_coverages(id INTEGER PRIMARY KEY,member_id INTEGER,coverage_report_id INTEGER,line_rate REAL,lines_covered INTEGER,lines_valid INTEGER,line_counts_available INTEGER,attribution_status TEXT);
    CREATE TABLE coverage_gaps(id INTEGER PRIMARY KEY,member_id INTEGER,coverage_report_id INTEGER,line_number INTEGER);
    CREATE TABLE mutation_testing_reports(id INTEGER PRIMARY KEY,project_id INTEGER,test_run_id INTEGER,is_baseline INTEGER,scope_kind TEXT);
    CREATE TABLE mutants(id INTEGER PRIMARY KEY,member_id INTEGER,mutation_testing_report_id INTEGER,status TEXT,mutator_name TEXT,stryker_mutant_id TEXT);
    CREATE TABLE experiment_runs(id INTEGER PRIMARY KEY,project_id INTEGER,experiment_series_id TEXT,run_uid TEXT,resolved_commit TEXT);
    CREATE TABLE experiment_matrix_work_items(id INTEGER PRIMARY KEY,stable_key TEXT);
    CREATE TABLE candidate_methods(id INTEGER PRIMARY KEY,source_member_id INTEGER,existing_test_member_id INTEGER,experiment_run_id INTEGER);
    CREATE TABLE generation_attempts(id INTEGER PRIMARY KEY,candidate_method_id INTEGER,experiment_matrix_work_item_id INTEGER,attempt_number INTEGER);
    CREATE TABLE tool_attempts(id INTEGER PRIMARY KEY,experiment_run_id INTEGER,matrix_work_item_id INTEGER,attempt_number INTEGER,candidate_method_id INTEGER,targeted_baseline_id INTEGER,post_attempt_test_run_id INTEGER);
    CREATE TABLE generated_test_executions(id INTEGER PRIMARY KEY,generation_attempt_id INTEGER,baseline_test_run_id INTEGER,test_run_id INTEGER);
    INSERT INTO projects VALUES(1,'owner','repo','revision');
    INSERT INTO files VALUES(1,'src.cs'),(2,'test.cs');
    INSERT INTO objects VALUES(1,1,'source'),(2,2,'tests');
    INSERT INTO members VALUES(10,1,'Source','void Source()',1,'public'),(20,2,'Test','void Test()',1,'public');
    INSERT INTO source_test_mappings VALUES(1,1,10,20,'Direct',1,1,'Direct',1,'v1');
    INSERT INTO candidate_inventory VALUES(1,1,10,1,1,1,'HasTests','Expand');
    INSERT INTO code_metrics VALUES(1,10,'Member',2);
    INSERT INTO test_runs VALUES(1,1),(2,1);
    INSERT INTO coverage_reports VALUES(1,1,1,1,1),(2,1,2,1,1);
    INSERT INTO member_coverages VALUES(1,10,1,.5,5,10,1,'Mapped'),(2,10,2,1,10,10,1,'Mapped');
    INSERT INTO coverage_gaps VALUES(1,10,1,5),(2,10,1,6);
    INSERT INTO mutation_testing_reports VALUES(1,1,1,1,'Solution'),(2,1,2,0,'SourceProject');
    INSERT INTO mutants VALUES(1,10,1,'Killed','Boolean','a'),(2,10,1,'Survived','Boolean','b'),(3,10,2,'Killed','Boolean','a'),(4,10,2,'Killed','Boolean','b');
    INSERT INTO experiment_runs VALUES(1,1,'series','run','revision');
    INSERT INTO experiment_matrix_work_items VALUES(1,'work');
    INSERT INTO candidate_methods VALUES(1,10,20,1);
    INSERT INTO tool_attempts VALUES(1,1,1,1,1,1,2);
    """)
    con.commit()
    yield path, con
    con.close()


@pytest.mark.parametrize("change", ["member", "report", "attribution", "legacy"])
def test_gap_diff_requires_measured_post_coverage(database, change):
    path, con = database
    if change == "member":
        con.execute("UPDATE member_coverages SET line_counts_available=0 WHERE coverage_report_id=2")
    elif change == "report":
        con.execute("UPDATE coverage_reports SET has_usable_coverage=0 WHERE id=2")
    elif change == "attribution":
        con.execute("UPDATE member_coverages SET attribution_status='Ambiguous' WHERE coverage_report_id=2")
    else:
        con.execute("ALTER TABLE member_coverages DROP COLUMN line_counts_available")
    con.commit()
    row = build_metric_diffs([str(path)]).iloc[0]
    assert pd.isna(row.lines_closed)
    assert row.mutants_newly_killed == 1


def test_valid_gap_diff(database):
    path, _ = database
    assert build_metric_diffs([str(path)]).lines_closed.iloc[0] == 2


def test_gap_diff_ignores_unusable_report_in_same_test_run(database):
    path, con = database
    con.executescript("""
    INSERT INTO coverage_reports VALUES(3,1,1,0,0);
    INSERT INTO member_coverages VALUES(3,10,3,0,0,10,0,'Mapped');
    INSERT INTO coverage_gaps VALUES(3,10,3,100);
    """)
    con.commit()
    assert build_metric_diffs([str(path)]).lines_closed.iloc[0] == 2


@pytest.mark.parametrize("rate,status", [(.5, "Measured"), (.7, "ConflictingObservations")])
def test_training_duplicate_coverage_does_not_duplicate_mapping(database, rate, status):
    _, con = database
    con.execute("INSERT INTO member_coverages VALUES(3,10,1,?,5,10,1,'Mapped')", (rate,))
    out = build_mapping_rows(con)
    assert len(out) == 1
    assert out.mapping_id.is_unique
    assert out.coverage_observation_status.iloc[0] == status
    if status == "ConflictingObservations":
        assert pd.isna(out.source_coverage.iloc[0])
        assert pd.isna(out.coverage_gap_count.iloc[0])


def test_training_uses_initial_run_not_post_attempt_reports(database):
    _, con = database
    row = build_mapping_rows(con).iloc[0]
    assert row.source_coverage == .5
    assert row.coverage_gap_count == 2
    assert row.mutation_score == 50
    assert row.killed_mutant_count == row.survived_mutant_count == 1
    assert row.coverage_report_id == row.mutation_report_id == 1
    assert row.coverage_test_run_id == row.mutation_test_run_id == 1
    con.execute("INSERT INTO mutants VALUES(5,10,2,'Killed','Boolean','c')")
    assert build_mapping_rows(con).iloc[0].mutation_score == 50


def test_training_does_not_substitute_targeted_baseline(database):
    _, con = database
    con.execute("UPDATE mutation_testing_reports SET scope_kind='SourceProject' WHERE id=1")
    row = build_mapping_rows(con).iloc[0]
    assert pd.isna(row.mutation_score)
    assert pd.isna(row.mutation_report_id)


@pytest.mark.parametrize("table,column,metric", [
    ("member_coverages", "coverage_report_id", "source_coverage"),
    ("coverage_gaps", "coverage_report_id", "coverage_gap_count"),
    ("mutants", "mutation_testing_report_id", "mutation_score"),
])
def test_training_does_not_pool_unattributed_measurements(database, table, column, metric):
    _, con = database
    con.execute(f'ALTER TABLE "{table}" DROP COLUMN "{column}"')
    row = build_mapping_rows(con).iloc[0]
    assert pd.isna(row[metric])


def test_optional_outputs_do_not_survive_a_new_cohort(tmp_path):
    optional = ["generated_tests.csv", "tool_generated_test_links.csv", "mutation_operators.csv",
                "assertion_observations.csv", "traced_assertions.csv", "evaluation_repository_families.csv"]
    for name in optional:
        pd.DataFrame([{"attempt_id": "old"}]).to_csv(tmp_path / name, index=False)
    save_datasets(pd.DataFrame([{"attempt_id": "new"}]), pd.DataFrame(), pd.DataFrame(),
                  pd.DataFrame(), {}, tmp_path)
    assert all(not (tmp_path / name).exists() for name in optional)


def test_empty_result_children_and_sidecar_are_valid(tmp_path):
    path = tmp_path / "results.csv"
    pd.DataFrame([_result_row(recognized_assertion_count=0)]).to_csv(path, index=False)
    for suffix in ("generated-tests", "test-results"):
        pd.DataFrame(columns=_result_row()).to_csv(tmp_path / f"results-{suffix}.csv", index=False)
    pd.DataFrame(columns=_assertion_row(1, "Traced", 0)).to_csv(tmp_path / "results.assertions.csv", index=False)
    attempts, generated, tests, assertions = read_schema4_result_bundle([str(path)])
    assert len(attempts) == 1
    assert generated.empty and tests.empty and assertions.empty
    assert assertions.attrs["sidecar_present"]


def test_failed_cohort_with_empty_children_builds_and_audits(tmp_path):
    path = tmp_path / "results.csv"
    row = _result_row(
        outcome_classification="ValidationFailed", assertion_measurement_status="Unavailable",
        assertion_measurement_reason="NoAppliedTestArtifact", generated_test_member_id=None,
        recognized_assertion_count=None, traced_assertion_count=None,
        trivial_assertion_count=None, unresolved_assertion_count=None,
        no_recognized_assertions=None,
    )
    pd.DataFrame([row]).to_csv(path, index=False)
    for suffix in ("generated-tests", "test-results"):
        pd.DataFrame(columns=row).to_csv(tmp_path / f"results-{suffix}.csv", index=False)
    pd.DataFrame(columns=_assertion_row(1, "Traced", 0)).to_csv(tmp_path / "results.assertions.csv", index=False)
    output = tmp_path / "built"
    build_datasets([str(path)], (), None, str(output))
    attempts = pd.read_csv(output / "evaluation_attempts.csv")
    assert len(attempts) == 1
    assert not attempts.positive_impact.iloc[0]
    assertions = pd.read_csv(output / "assertion_observations.csv")
    assert assertions.empty
    assert audit_assertion_lineage(attempts, pd.DataFrame(), assertions, sidecar_present=True) == []


def test_blank_schema_on_nonempty_row_still_fails(tmp_path):
    path = tmp_path / "results.csv"
    pd.DataFrame([{"results_schema_version": None, "row_kind": "attempt", "attempt_id": "a"}]).to_csv(path, index=False)
    with pytest.raises(ValueError, match="schema_version"):
        read_result_grains([str(path)])


@pytest.mark.parametrize("legacy_first", [True, False])
def test_msr_mapping_schema_is_stable_across_versions(database, tmp_path, legacy_first):
    _, con = database
    legacy = load_mappings(con)
    con.executescript("CREATE TABLE source_test_mapping_trace_steps(source_test_mapping_id INTEGER,relationship_kind TEXT,edge_source TEXT); INSERT INTO source_test_mapping_trace_steps VALUES(1,'call','semantic');")
    current = load_mappings(con)
    assert list(legacy.columns) == list(current.columns)
    assert pd.isna(legacy.trace_steps.iloc[0])
    appender = _CsvAppender(tmp_path / "mappings.csv")
    for frame in ([legacy, current] if legacy_first else [current, legacy]):
        appender.append(frame[frame.columns[::-1]] if frame is current else frame)
    read = pd.read_csv(tmp_path / "mappings.csv")
    assert len(read) == 2
    assert read.trace_steps.dropna().tolist() == [1]


def test_msr_rejects_schema_drift_before_appending(tmp_path):
    path = tmp_path / "frame.csv"
    appender = _CsvAppender(path)
    appender.append(pd.DataFrame([{"a": 1}]))
    with pytest.raises(ValueError, match="Inconsistent columns"):
        appender.append(pd.DataFrame([{"a": 2, "b": 3}]))
    assert pd.read_csv(path).a.tolist() == [1]


def test_mutation_profile_preserves_owner_revision_and_report(database, tmp_path):
    path, con = database
    other = tmp_path / "other.db"
    dest = sqlite3.connect(other)
    con.backup(dest)
    dest.execute("UPDATE projects SET owner='another',last_analyzed_commit='otherrev'")
    dest.commit()
    dest.close()
    profiles = build_mutation_operators([str(path), str(other)])
    assert set(profiles.repository_key) == {"owner/repo|revision", "another/repo|otherrev"}
    assert profiles.mutation_testing_report_id.tolist() == [1, 1]
    assert profiles._source_db.nunique() == 2
    assert profiles.Killed.tolist() == [1, 1]


def test_mutation_profile_handles_blank_additive_provenance(database):
    path, con = database
    con.executescript("""
    ALTER TABLE projects ADD COLUMN repository_identity TEXT DEFAULT '';
    ALTER TABLE projects ADD COLUMN resolved_commit TEXT DEFAULT '';
    """)
    con.commit()
    assert build_mutation_operators([str(path)]).repository_key.tolist() == ["owner/repo|revision"]
