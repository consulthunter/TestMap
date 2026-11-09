"""Linkage and attribution checks that make the per-construct tables symmetric.

Each construct should answer both questions: how many emitted rows name an entity
(attribution) and how many entities carry a row (linkage). These three checks fill
the cells that were previously blank.
"""

import pandas as pd

from analysis.msr_checks import (
    CHECK_SEVERITY,
    check_coverage,
    check_mappings,
    check_test_smells,
)


def _rate(rows, check):
    match = [r for r in rows if r["check"] == check]
    assert len(match) == 1, f"expected exactly one {check} row, got {len(match)}"
    return match[0]


def _entities():
    return pd.DataFrame([
        # entity_id, kind, is_test
        {"entity_kind": "member", "entity_id": 1, "kind": "method", "is_test": 1,
         "start_line": 0, "end_line": 20},
        {"entity_kind": "member", "entity_id": 2, "kind": "method", "is_test": 1,
         "start_line": 30, "end_line": 50},
        {"entity_kind": "member", "entity_id": 3, "kind": "method", "is_test": 0,
         "start_line": 60, "end_line": 80},
        {"entity_kind": "object", "entity_id": 10, "kind": "class", "is_test": 1,
         "start_line": 0, "end_line": 90},
        {"entity_kind": "object", "entity_id": 11, "kind": "class", "is_test": 0,
         "start_line": 0, "end_line": 90},
    ])


# --- test smells: entity-side linkage --------------------------------------

def test_test_members_missing_smells_counts_clean_test_members():
    smells = pd.DataFrame([
        {"member_id": 1.0, "object_id": None, "claimed_line": 5.0},
        {"member_id": 1.0, "object_id": None, "claimed_line": 6.0},
    ])
    rows: list[dict] = []
    check_test_smells(rows, "o/r@abc", _entities(), smells)

    row = _rate(rows, "test_members_missing_smells")
    # Two test members exist; only member 1 carries a finding.
    assert (row["numerator"], row["denominator"]) == (1, 2)
    # Production member 3 is not in the denominator.
    assert row["rate"] == 0.5


def test_test_members_missing_smells_is_prevalence_not_a_gap():
    assert "test_members_missing_smells" in CHECK_SEVERITY["prevalence"]
    assert "test_members_missing_smells" not in CHECK_SEVERITY["coverage_gap"]


def test_test_members_missing_smells_absent_when_no_test_members():
    entities = _entities()
    entities = entities[entities["is_test"] == 0]
    smells = pd.DataFrame([{"member_id": 3.0, "object_id": None, "claimed_line": 61.0}])
    rows: list[dict] = []
    check_test_smells(rows, "o/r@abc", entities, smells)
    assert not [r for r in rows if r["check"] == "test_members_missing_smells"]


# --- coverage: object-side linkage -----------------------------------------

def _coverage(entries):
    frame = pd.DataFrame(entries)
    for column in ("lines_valid", "lines_covered", "line_rate", "branch_rate"):
        frame[column] = frame.get(column, 0)
    return frame


def test_objects_missing_coverage_uses_the_object_denominator():
    coverage = _coverage([
        {"entity_kind": "member", "entity_id": 1, "lines_valid": 4,
         "lines_covered": 2, "line_rate": 0.5, "branch_rate": 0.5},
        {"entity_kind": "object", "entity_id": 10, "lines_valid": 8,
         "lines_covered": 4, "line_rate": 0.5, "branch_rate": 0.5},
    ])
    rows: list[dict] = []
    check_coverage(rows, "o/r@abc", _entities(), coverage, has_report=True)

    objects = _rate(rows, "objects_missing_coverage")
    members = _rate(rows, "members_missing_coverage")
    assert (objects["numerator"], objects["denominator"]) == (1, 2)
    assert (members["numerator"], members["denominator"]) == (2, 3)


def test_objects_missing_coverage_recorded_when_a_report_produced_no_rows():
    rows: list[dict] = []
    check_coverage(rows, "o/r@abc", _entities(), pd.DataFrame(), has_report=True)
    row = _rate(rows, "objects_missing_coverage")
    assert (row["numerator"], row["denominator"]) == (2, 2)


def test_null_entity_coverage_is_unattributed_not_orphaned():
    coverage = _coverage([
        {"entity_kind": "member", "entity_id": 1, "lines_valid": 4,
         "lines_covered": 2, "line_rate": 0.5, "branch_rate": 0.5},
        {"entity_kind": "member", "entity_id": None, "lines_valid": 4,
         "lines_covered": 0, "line_rate": 0.0, "branch_rate": 0.0},
        {"entity_kind": "object", "entity_id": None, "lines_valid": 8,
         "lines_covered": 4, "line_rate": 0.5, "branch_rate": 0.5},
    ])
    rows: list[dict] = []
    check_coverage(rows, "o/r@abc", _entities(), coverage, has_report=True)

    unattributed = _rate(rows, "coverage_unattributed")
    assert (unattributed["numerator"], unattributed["denominator"]) == (2, 3)
    assert _rate(rows, "coverage_orphan_entity")["numerator"] == 0
    # An unplaced entry covers nothing: members 2 and 3 and both objects stay uncovered.
    assert _rate(rows, "members_missing_coverage")["numerator"] == 2
    assert _rate(rows, "objects_missing_coverage")["numerator"] == 2


def test_no_coverage_rows_and_no_report_records_nothing():
    rows: list[dict] = []
    check_coverage(rows, "o/r@abc", _entities(), pd.DataFrame(), has_report=False)
    assert rows == []


# --- mappings: attribution --------------------------------------------------

def _mappings(pairs):
    return pd.DataFrame([
        {"source_member_id": s, "test_member_id": t, "path_length": 1, "trace_steps": 1}
        for s, t in pairs
    ])


def test_mappings_unattributed_is_zero_when_both_ends_are_present():
    rows: list[dict] = []
    check_mappings(rows, "o/r@abc", _entities(), _mappings([(3, 1), (3, 2)]))
    row = _rate(rows, "mappings_unattributed")
    assert (row["numerator"], row["denominator"]) == (0, 2)


def test_mappings_unattributed_counts_a_missing_or_zero_endpoint():
    rows: list[dict] = []
    check_mappings(rows, "o/r@abc", _entities(),
                   _mappings([(3, 1), (0, 2), (3, None), (None, None)]))
    row = _rate(rows, "mappings_unattributed")
    assert (row["numerator"], row["denominator"]) == (3, 4)
