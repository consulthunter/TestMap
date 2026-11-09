"""Draw the human-judged validation sample and write rater artifacts.

One seeded, stratified draw per sheet. Each sampled unit becomes a directory
holding the code the rater judges, so rating never requires a repository checkout
or a database: source text is read from ``members.full_string`` /
``objects.full_string`` at sampling time and written into the unit.

TestMap's own answer is written to ``answers/<sheet>.csv``, never into the unit or
the rating form. That separation is what keeps the blind sheets blind, and it is
what lets :mod:`analysis.msr_agreement` join ratings back to the tool's output
after collection.

Sheet kinds:

``single``  the rater picks exactly one label
``multi``   the rater picks any number of labels (test smells)
"""

from __future__ import annotations

import json
import sqlite3
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import ensure_output_dir
from analysis.msr_frames import find_databases


ATTRIBUTION_LABELS = ["correct", "wrong_member", "unclear"]


@dataclass
class Sheet:
    """One rating task."""

    sheet_id: str
    construct: str
    kind: str
    question: str
    labels: list[str]
    blind: bool = False
    instructions: str = ""
    strata: list[str] = field(default_factory=list)


SHEETS: dict[str, Sheet] = {
    "smells_correctness": Sheet(
        sheet_id="smells_correctness",
        construct="test_smells",
        kind="multi",
        question="Which test smells are present in this test method?",
        labels=[],  # filled from the corpus at build time
        blind=True,
        instructions=(
            "Read the test method and select every smell from the codebook that is "
            "present. Select nothing if none apply. You are not being asked whether "
            "you agree with a label — no label is shown."
        ),
        strata=["baseline", "duplicate_member_name_in_file", "large_member"],
    ),
    "smells_recall": Sheet(
        sheet_id="smells_recall",
        construct="test_smells_complement",
        kind="multi",
        question="Which test smells are present in this test method?",
        labels=[],
        blind=True,
        instructions=(
            "Same task as the correctness sheet. These methods were drawn from those "
            "TestMap flagged with no smells at all; anything you find here is a miss."
        ),
        strata=["baseline"],
    ),
    "smells_attribution": Sheet(
        sheet_id="smells_attribution",
        construct="test_smells",
        kind="single",
        question="Is this smell about the member it was attached to?",
        labels=ATTRIBUTION_LABELS,
        instructions=(
            "The smell and the member TestMap attached it to are both shown. Judge "
            "only whether the attachment is right, not whether the smell is real."
        ),
        strata=["baseline", "duplicate_member_name_in_file", "overload"],
    ),
    "mapping_exercise": Sheet(
        sheet_id="mapping_exercise",
        construct="mappings",
        kind="single",
        question="Does this test non-incidentally exercise the production member?",
        labels=["exercised", "incidental_or_setup", "not_exercised", "unclear"],
        instructions=(
            "Both members are shown. 'exercised' means the test meaningfully drives "
            "the member; 'incidental_or_setup' means it is only touched while getting "
            "somewhere else. Test-intent depth is out of scope."
        ),
        strata=["baseline", "path_length_gt_1", "low_confidence"],
    ),
    "mapping_attribution": Sheet(
        sheet_id="mapping_attribution",
        construct="mappings",
        kind="single",
        question="Does the mapping point at the member the trace actually reaches?",
        labels=ATTRIBUTION_LABELS,
        instructions=(
            "The trace chain is shown alongside both members. Judge whether the "
            "recorded source member is the endpoint the chain describes."
        ),
        strata=["baseline", "path_length_gt_1", "overload"],
    ),
    "mapping_recall": Sheet(
        sheet_id="mapping_recall",
        construct="mappings_complement",
        kind="single",
        question="Is this production member exercised by any test in the repository?",
        labels=["exercised", "not_exercised", "unclear"],
        instructions=(
            "These members have no mapping at all. The containing class and the "
            "repository's test class names are provided as context. Answer 'unclear' "
            "freely — this question cannot always be settled from the excerpt."
        ),
        strata=["baseline"],
    ),
    "metrics_attribution": Sheet(
        sheet_id="metrics_attribution",
        construct="code_metrics",
        kind="single",
        question="Is this metric row attached to the right member?",
        labels=ATTRIBUTION_LABELS,
        instructions=(
            "The metric values and the member they were attached to are shown. Judge "
            "the attachment, not whether the numbers are correct."
        ),
        strata=["baseline", "overload", "large_member"],
    ),
    "mutants_attribution": Sheet(
        sheet_id="mutants_attribution",
        construct="mutants",
        kind="single",
        question="Are these mutants on the right member?",
        labels=ATTRIBUTION_LABELS,
        instructions=(
            "The mutant counts and the member they were attached to are shown."
        ),
        strata=["baseline", "overload"],
    ),
}

DEFAULT_SHEETS = [
    "smells_correctness",
    "smells_recall",
    "smells_attribution",
    "mapping_exercise",
    "mapping_attribution",
    "mapping_recall",
    "metrics_attribution",
    "mutants_attribution",
]


# ---------------------------------------------------------------------------
# Source text
# ---------------------------------------------------------------------------

class SourceReader:
    """Read member and object source text from the per-repository databases."""

    def __init__(self, data_dir: str | Path):
        self._by_repo: dict[str, Path] = {}
        for path in find_databases([str(data_dir)]):
            try:
                conn = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
                owner, repo, commit = conn.execute(
                    "SELECT owner, repo_name, last_analyzed_commit FROM projects LIMIT 1"
                ).fetchone()
                conn.close()
            except (sqlite3.Error, TypeError):
                continue
            self._by_repo[f"{owner}/{repo}@{commit[:12]}".lower()] = path

    def _connect(self, repo_key: str) -> Optional[sqlite3.Connection]:
        path = self._by_repo.get(repo_key.lower())
        if path is None:
            return None
        return sqlite3.connect(f"file:{path}?mode=ro", uri=True)

    def member(self, repo_key: str, member_id: int) -> dict:
        """Return a member's source, its containing class source, and file path."""
        conn = self._connect(repo_key)
        if conn is None:
            return {}
        try:
            row = conn.execute("""
                SELECT m.name, m.kind, m.full_string, m.start_line_number, m.end_line_number,
                       o.name, o.full_string, f.file_path
                FROM members m
                JOIN objects o ON o.id = m.object_id
                JOIN files   f ON f.id = o.file_id
                WHERE m.id = ?
            """, (int(member_id),)).fetchone()
        except sqlite3.Error:
            row = None
        finally:
            conn.close()
        if not row:
            return {}
        return {
            "name": row[0], "kind": row[1], "source": row[2],
            "start_line": row[3], "end_line": row[4],
            "class_name": row[5], "class_source": row[6], "file_path": row[7],
        }

    def test_class_names(self, repo_key: str, limit: int = 40) -> list[str]:
        conn = self._connect(repo_key)
        if conn is None:
            return []
        try:
            rows = conn.execute(
                "SELECT DISTINCT name FROM objects WHERE is_test_object = 1 ORDER BY name LIMIT ?",
                (limit,),
            ).fetchall()
        except sqlite3.Error:
            rows = []
        finally:
            conn.close()
        return [r[0] for r in rows]

    def member_names(self, repo_key: str, member_ids: list[int]) -> dict[int, str]:
        """Resolve member ids to ``Class.Member`` names for trace rendering."""
        wanted = [int(m) for m in member_ids if m is not None]
        if not wanted:
            return {}
        conn = self._connect(repo_key)
        if conn is None:
            return {}
        placeholders = ",".join("?" for _ in wanted)
        try:
            rows = conn.execute(f"""
                SELECT m.id, o.name, m.name, m.kind, m.is_test_member
                FROM members m JOIN objects o ON o.id = m.object_id
                WHERE m.id IN ({placeholders})
            """, wanted).fetchall()
        except sqlite3.Error:
            rows = []
        finally:
            conn.close()
        return {
            r[0]: f"{r[1]}.{r[2]} ({r[3]}{', test' if r[4] else ''})"
            for r in rows
        }

    def trace_steps(self, repo_key: str, mapping_id: int) -> list[dict]:
        """Return the ordered trace chain, with both endpoints of each hop."""
        conn = self._connect(repo_key)
        if conn is None:
            return []
        try:
            rows = conn.execute("""
                SELECT step_index, from_member_id, to_member_id,
                       relationship_kind, edge_source, summary
                FROM source_test_mapping_trace_steps
                WHERE source_test_mapping_id = ?
                ORDER BY step_index
            """, (int(mapping_id),)).fetchall()
        except sqlite3.Error:
            rows = []
        finally:
            conn.close()

        steps = [
            {"step": r[0], "from_member_id": r[1], "to_member_id": r[2],
             "relationship": r[3], "edge_source": r[4], "summary": r[5]}
            for r in rows
        ]
        names = self.member_names(
            repo_key,
            [s["from_member_id"] for s in steps] + [s["to_member_id"] for s in steps],
        )
        for step in steps:
            step["from_name"] = names.get(step["from_member_id"], f"member {step['from_member_id']}")
            step["to_name"] = names.get(step["to_member_id"], f"member {step['to_member_id']}")
        return steps

    def smells_for_member(self, repo_key: str, member_id: int) -> list[dict]:
        """Return every smell TestMap attached to this member."""
        conn = self._connect(repo_key)
        if conn is None:
            return []
        try:
            rows = conn.execute("""
                SELECT smell_id, smell_name, message, line, column
                FROM test_smells WHERE member_id = ?
                ORDER BY line, smell_name
            """, (int(member_id),)).fetchall()
        except sqlite3.Error:
            rows = []
        finally:
            conn.close()
        return [
            {"smell_id": r[0], "smell_name": r[1], "message": r[2], "line": r[3], "column": r[4]}
            for r in rows
        ]

    def mutants_for_member(self, repo_key: str, member_id: int, limit: int = 10) -> tuple[list[dict], int]:
        """Return individual mutants for a member, plus the total count.

        The rater needs to see actual mutations to judge attribution; aggregate
        status counts say nothing about which code was mutated.
        """
        conn = self._connect(repo_key)
        if conn is None:
            return [], 0
        try:
            total = conn.execute(
                "SELECT COUNT(*) FROM mutants WHERE member_id = ?", (int(member_id),)
            ).fetchone()[0]
            rows = conn.execute("""
                SELECT stryker_mutant_id, mutator_name, original_code, replacement,
                       status, status_reason, location, is_static
                FROM mutants WHERE member_id = ?
                ORDER BY id LIMIT ?
            """, (int(member_id), limit)).fetchall()
        except sqlite3.Error:
            return [], 0
        finally:
            conn.close()

        mutants = []
        for r in rows:
            try:
                location = json.loads(r[6]) if r[6] else {}
            except (TypeError, ValueError):
                location = {}
            mutants.append({
                "stryker_mutant_id": r[0], "mutator_name": r[1],
                "original_code": r[2], "replacement": r[3],
                "status": r[4], "status_reason": r[5],
                "start_line": location.get("StartLineNumber"),
                "end_line": location.get("EndLineNumber"),
                "is_static": bool(r[7]),
            })
        return mutants, int(total)


# ---------------------------------------------------------------------------
# Strata
# ---------------------------------------------------------------------------

def assign_strata(units: pd.DataFrame, entities: pd.DataFrame, sheet: Sheet) -> pd.DataFrame:
    """Label each unit with the risk strata it belongs to.

    A unit can qualify for several; the first non-baseline stratum in the sheet's
    list wins so the draw stays balanced and every unit has exactly one label.
    """
    out = units.copy()
    members = entities[entities["entity_kind"] == "member"]

    overloads = (
        members.groupby(["repo_key", "parent_object_id", "name"]).size()
        .rename("n").reset_index().query("n > 1")
    )
    overload_keys = set(zip(overloads["repo_key"], overloads["parent_object_id"], overloads["name"]))

    duplicates = (
        members.groupby(["repo_key", "file_id", "name"]).size()
        .rename("n").reset_index().query("n > 1")
    )
    duplicate_keys = set(zip(duplicates["repo_key"], duplicates["file_id"], duplicates["name"]))

    large_cut = members["source_chars"].quantile(0.95)

    lookup = members.set_index(["repo_key", "entity_id"])[
        ["parent_object_id", "name", "file_id", "source_chars"]
    ]

    def _stratum(row: pd.Series) -> str:
        key = (row["repo_key"], row.get("member_id"))
        try:
            entity = lookup.loc[key]
        except KeyError:
            return "baseline"
        if isinstance(entity, pd.DataFrame):
            entity = entity.iloc[0]

        if "overload" in sheet.strata and (
            row["repo_key"], entity["parent_object_id"], entity["name"]
        ) in overload_keys:
            return "overload"
        if "duplicate_member_name_in_file" in sheet.strata and (
            row["repo_key"], entity["file_id"], entity["name"]
        ) in duplicate_keys:
            return "duplicate_member_name_in_file"
        if "large_member" in sheet.strata and entity["source_chars"] >= large_cut:
            return "large_member"
        return "baseline"

    out["stratum"] = out.apply(_stratum, axis=1)

    if "path_length_gt_1" in sheet.strata and "path_length" in out.columns:
        out.loc[out["path_length"] > 1, "stratum"] = "path_length_gt_1"
    if "low_confidence" in sheet.strata and "confidence" in out.columns:
        out.loc[out["confidence"] < 1.0, "stratum"] = "low_confidence"

    return out


def stratified_draw(units: pd.DataFrame, n: int, seed: int) -> pd.DataFrame:
    """Draw *n* units: half baseline, the rest spread over the risk strata.

    The baseline half carries the unbiased rate estimate; risk strata are for
    discovery and are reported separately, never pooled into the headline rate.
    """
    if units.empty:
        return units

    baseline = units[units["stratum"] == "baseline"]
    risk = units[units["stratum"] != "baseline"]

    take_baseline = min(len(baseline), max(1, n // 2))
    drawn = [baseline.sample(take_baseline, random_state=seed)]

    remaining = n - take_baseline
    if remaining > 0 and not risk.empty:
        groups = list(risk.groupby("stratum"))
        per_group = max(1, remaining // len(groups))
        for offset, (_, group) in enumerate(groups):
            drawn.append(group.sample(min(len(group), per_group), random_state=seed + offset + 1))

    result = pd.concat(drawn, ignore_index=True)
    if len(result) > n:
        result = result.sample(n, random_state=seed)
    return result.reset_index(drop=True)
