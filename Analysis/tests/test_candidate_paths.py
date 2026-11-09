import json
import sqlite3

from analysis.candidate_paths import export


def _write_inventory(path, rows):
    path.parent.mkdir(parents=True)
    conn = sqlite3.connect(path)
    conn.execute("""
        CREATE TABLE candidate_inventory (
            id INTEGER PRIMARY KEY, source_member_id INTEGER, is_experiment_eligible INTEGER,
            context_evidence_kind TEXT, has_grounded_test_context INTEGER,
            access_path_strategy TEXT, trace_json TEXT
        )""")
    for row_id, member, eligible, kind, distance, bindings in rows:
        trace = {"AccessPath": {"Distance": distance}, "SetupBindings": [{}] * bindings}
        conn.execute("INSERT INTO candidate_inventory VALUES (?, ?, ?, ?, 1, 'x', ?)",
                     (row_id, member, eligible, kind, json.dumps(trace)))
    conn.commit()
    conn.close()


def test_export_replays_default_selection_order(tmp_path):
    # Inventory order is strategy rank; direct evidence outranks earlier indirect rows.
    db = tmp_path / "owner" / "repo" / "abc123" / "analysis.db"
    _write_inventory(db, [
        (1, 10, 1, "IndirectInvocation", 2, 3),
        (2, 11, 1, "DirectMethodInvocation", 0, 1),
        (3, 12, 0, "DirectMethodInvocation", 0, 1),  # not eligible: excluded
        (4, 13, 1, "DirectMethodInvocation", 0, 0),
        (5, 14, 1, "HelperMediatedPath", 4, 2),
    ])

    df = export((str(tmp_path / "**" / "analysis.db"),), str(tmp_path / "out.csv"), per_repo=2)

    assert sorted(df["source_member_id"]) == [10, 11, 13, 14]
    selected = df[df["selected_default"] == 1]
    assert sorted(selected["source_member_id"]) == [11, 13]
    by_member = df.set_index("source_member_id")
    assert by_member.loc[10, "path_hops"] == 3
    assert by_member.loc[14, "path_hops_capped"] == 3
    assert by_member.loc[10, "setup_bindings"] == 3
    assert list(by_member["path_indirect"]) == [1, 0, 0, 1]
