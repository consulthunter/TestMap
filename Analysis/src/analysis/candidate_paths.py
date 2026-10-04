"""Candidate access-path export: one row per experiment-eligible candidate.

The access path is the selector's route from the paired existing test to the candidate
(``candidate_inventory.trace_json`` -> ``AccessPath``): test -> entry point -> production
callers -> candidate. ``path_hops`` = ``Distance`` + 1, so 1 means the test calls the
candidate directly. This is not ``source_test_mappings``, which stops at the first
production member.

``selected_default`` replays the default (non-randomized) cohort selection in
``MethodSelectionService.SelectCandidateMethodsAsync``: eligible candidates ordered by
context-evidence priority, then strategy rank (inventory insertion order), top N per repo.
"""

from __future__ import annotations

import glob
import json
import sqlite3
from pathlib import Path

import click
import pandas as pd

# Mirrors MethodSelectionService.GetContextSelectionPriority.
_EVIDENCE_PRIORITY = {
    "DirectMethodInvocation": 0,
    "DirectInvocation": 0,
    "MemberRelationship": 1,
    "HelperMediatedPath": 2,
    "ProductionMethodPath": 3,
    "DeepProductionMethodPath": 4,
    "DirectConstructorInvocation": 5,
    "ConstructorMediatedPath": 6,
}


def selection_priority(evidence_kind: str, is_grounded: bool) -> int:
    if evidence_kind in _EVIDENCE_PRIORITY:
        return _EVIDENCE_PRIORITY[evidence_kind]
    if is_grounded:
        return 7
    return 9 if evidence_kind == "WeakHeuristic" else 8


def _repo_key(db_path: str) -> str:
    parts = Path(db_path).parts
    return f"{parts[-4]}/{parts[-3]}@{parts[-2][:12]}"


def load_candidate_paths(db_path: str) -> pd.DataFrame:
    with sqlite3.connect(db_path) as conn:
        try:
            rows = pd.read_sql(
                """select id, source_member_id, context_evidence_kind, has_grounded_test_context,
                          access_path_strategy, trace_json
                   from candidate_inventory where is_experiment_eligible = 1""",
                conn,
            )
            metrics = _load_member_metrics(conn)
        except (sqlite3.OperationalError, pd.errors.DatabaseError):
            return pd.DataFrame()
    if rows.empty:
        return rows

    traces = rows["trace_json"].map(json.loads)
    access = traces.map(lambda t: t.get("AccessPath") or {})
    return pd.DataFrame({
        "repo_key": _repo_key(db_path),
        "inventory_id": rows["id"],
        "source_member_id": rows["source_member_id"],
        "context_evidence_kind": rows["context_evidence_kind"],
        "access_path_strategy": rows["access_path_strategy"],
        "path_hops": access.map(lambda a: int(a.get("Distance") or 0) + 1),
        "setup_bindings": traces.map(lambda t: len(t.get("SetupBindings") or [])),
        "selection_priority": [
            selection_priority(k, bool(g))
            for k, g in zip(rows["context_evidence_kind"], rows["has_grounded_test_context"])
        ],
    }).merge(metrics, on="source_member_id", how="left")


# Structural metrics of the candidate method, named as in R/common.R STRUCTURAL_METRICS.
METRIC_COLUMNS = [
    "maintainability_index", "cyclomatic_complexity", "class_coupling",
    "depth_of_inheritance", "source_lines_of_code", "executable_lines_of_code",
]


def _load_member_metrics(conn: sqlite3.Connection) -> pd.DataFrame:
    try:
        metrics = pd.read_sql(
            f"""select entity_id as source_member_id, {", ".join(METRIC_COLUMNS)}
                from code_metrics where lower(entity_type) = 'member'""",
            conn,
        )
    except (sqlite3.OperationalError, pd.errors.DatabaseError):
        return pd.DataFrame(columns=["source_member_id", *METRIC_COLUMNS])
    return metrics.drop_duplicates("source_member_id", keep="last")


def export(db_globs: tuple[str, ...], out: str, per_repo: int) -> pd.DataFrame:
    paths = sorted({p for pattern in db_globs for p in glob.glob(pattern, recursive=True)})
    frames = [f for f in (load_candidate_paths(p) for p in paths) if not f.empty]
    if not frames:
        raise click.ClickException(f"No eligible candidate_inventory rows in {len(paths)} database(s).")
    df = pd.concat(frames, ignore_index=True)

    df["path_hops_capped"] = df["path_hops"].clip(upper=3)
    df["path_indirect"] = (df["path_hops"] > 1).astype(int)
    df["selection_rank"] = (
        df.sort_values(["selection_priority", "inventory_id"])
        .groupby("repo_key").cumcount()
        .reindex(df.index)
    )
    df["selected_default"] = (df["selection_rank"] < per_repo).astype(int)

    out_path = Path(out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(out_path, index=False)
    return df

