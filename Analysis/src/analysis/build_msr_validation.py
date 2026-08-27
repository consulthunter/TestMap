"""Build the human-judged validation sample: units, codebooks, and rating forms.

Output layout::

    README.md                              protocol, seed, what to do
    codebooks/<sheet>.md                   label definitions, frozen before rating
    answers/<sheet>.csv                    TestMap's own values, joined back after rating
    samples/<sheet>/<unit_id>/
        unit.md                            the question and its context
        excerpt.cs                         the member under judgement
        context.cs                         containing class (when different)
        meta.json                          identity keys
    ratings/<rater>/<sheet>/<unit_id>.md   the form each rater fills in

Rating forms carry a fenced YAML block rather than prose fields so collection is
deterministic across several hundred files. A malformed block fails loudly in
``msr-collect`` instead of silently scoring as empty.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import ensure_output_dir
from analysis.msr_sampling import (
    DEFAULT_SHEETS,
    SHEETS,
    Sheet,
    SourceReader,
    assign_strata,
    stratified_draw,
)


FRAME_FILES = {
    "entities": "msr_entities.csv",
    "test_smells": "msr_test_smells.csv",
    "mappings": "msr_mappings.csv",
    "code_metrics": "msr_code_metrics.csv",
    "mutants": "msr_mutants.csv",
}


def load_frames(frames_dir: str | Path) -> dict[str, pd.DataFrame]:
    frames = {}
    for name, filename in FRAME_FILES.items():
        path = Path(frames_dir) / filename
        frames[name] = (
            pd.read_csv(path, low_memory=False) if path.exists() else pd.DataFrame()
        )
    return frames


# ---------------------------------------------------------------------------
# Candidate units per sheet
# ---------------------------------------------------------------------------

def _members(entities: pd.DataFrame) -> pd.DataFrame:
    return entities[entities["entity_kind"] == "member"]


def candidate_units(sheet_id: str, frames: dict[str, pd.DataFrame]) -> pd.DataFrame:
    """Return the frame a sheet draws from, with a ``member_id`` column."""
    entities = frames["entities"]
    members = _members(entities)

    if sheet_id == "smells_correctness":
        smells = frames["test_smells"].dropna(subset=["member_id"])
        if smells.empty:
            return smells
        units = (
            smells.groupby(["repo_key", "member_id"])
            .agg(smell_count=("observation_id", "size"))
            .reset_index()
        )
        units["member_id"] = units["member_id"].astype(int)
        return units

    if sheet_id == "smells_recall":
        smells = frames["test_smells"]
        if smells.empty:
            return pd.DataFrame()
        scoped = set(smells["repo_key"])
        flagged = set(zip(smells["repo_key"], smells["member_id"].dropna().astype(int)))
        pool = members[
            (members["repo_key"].isin(scoped))
            & (members["is_test"] == 1)
            & (members["kind"] == "method")
        ].copy()
        pool["member_id"] = pool["entity_id"].astype(int)
        return pool[~pool.apply(lambda r: (r["repo_key"], r["member_id"]) in flagged, axis=1)]

    if sheet_id == "smells_attribution":
        smells = frames["test_smells"].dropna(subset=["member_id"]).copy()
        if smells.empty:
            return smells
        smells["member_id"] = smells["member_id"].astype(int)
        return smells

    if sheet_id in ("mapping_exercise", "mapping_attribution"):
        mappings = frames["mappings"]
        if mappings.empty:
            return mappings
        grounded = mappings[mappings["is_grounded"] == 1].copy()
        grounded["member_id"] = grounded["source_member_id"].astype(int)
        return grounded

    if sheet_id == "mapping_recall":
        mappings = frames["mappings"]
        if mappings.empty:
            return pd.DataFrame()
        scoped = set(mappings["repo_key"])
        mapped = set(zip(mappings["repo_key"], mappings["source_member_id"].astype(int)))
        pool = members[
            (members["repo_key"].isin(scoped))
            & (members["is_test"] == 0)
            & (members["kind"] == "method")
        ].copy()
        pool["member_id"] = pool["entity_id"].astype(int)
        return pool[~pool.apply(lambda r: (r["repo_key"], r["member_id"]) in mapped, axis=1)]

    if sheet_id == "metrics_attribution":
        metrics = frames["code_metrics"]
        if metrics.empty:
            return metrics
        rows = metrics[metrics["entity_kind"] == "member"].copy()
        rows["member_id"] = rows["entity_id"].astype(int)
        return rows

    if sheet_id == "mutants_attribution":
        mutants = frames["mutants"]
        if mutants.empty:
            return mutants
        rows = mutants[mutants["entity_kind"] == "member"].copy()
        rows["member_id"] = rows["entity_id"].astype(int)
        return rows

    raise ValueError(f"Unknown sheet: {sheet_id}")


def sheet_answer(sheet_id: str, unit: pd.Series, frames: dict) -> dict:
    """Return TestMap's own output for this unit — never shown to the rater."""
    if sheet_id in ("smells_correctness", "smells_recall"):
        smells = frames["test_smells"]
        matching = smells[
            (smells["repo_key"] == unit["repo_key"])
            & (smells["member_id"] == unit["member_id"])
        ]
        return {"labels": sorted(set(matching["smell_name"].dropna()))}

    if sheet_id == "smells_attribution":
        return {"smell_name": unit.get("smell_name", ""), "member_id": unit["member_id"]}

    if sheet_id in ("mapping_exercise", "mapping_attribution"):
        return {
            "evidence_kind": unit.get("evidence_kind", ""),
            "path_length": unit.get("path_length", ""),
            "confidence": unit.get("confidence", ""),
            "source_member_id": unit.get("source_member_id", ""),
            "test_member_id": unit.get("test_member_id", ""),
        }

    if sheet_id == "mapping_recall":
        return {"mapped": False}

    if sheet_id == "metrics_attribution":
        return {
            k: unit.get(k, "")
            for k in ("maintainability_index", "cyclomatic_complexity", "class_coupling",
                      "depth_of_inheritance", "source_lines_of_code", "executable_lines_of_code")
        }

    if sheet_id == "mutants_attribution":
        return {
            k: unit.get(k, "")
            for k in ("mutants_total", "killed", "survived", "nocoverage", "distinct_mutators")
        }

    return {}


# ---------------------------------------------------------------------------
# Unit rendering
# ---------------------------------------------------------------------------

def _fence(code: str, language: str = "csharp") -> str:
    return f"```{language}\n{code}\n```"


def render_unit(sheet: Sheet, unit_id: str, unit: pd.Series, reader: SourceReader,
                frames: dict) -> dict[str, str]:
    """Return the files for one unit directory, keyed by filename."""
    repo_key = unit["repo_key"]
    member = reader.member(repo_key, unit["member_id"])
    files: dict[str, str] = {}

    body = [
        f"# {sheet.sheet_id} — {unit_id}",
        "",
        f"**Repository:** `{repo_key}`",
        f"**File:** `{member.get('file_path', '')}`",
        f"**Member:** `{member.get('class_name', '')}.{member.get('name', '')}` "
        f"({member.get('kind', '')}, lines {member.get('start_line', '')}–{member.get('end_line', '')})",
        "",
        "## Question",
        "",
        sheet.question,
        "",
        sheet.instructions,
        "",
        f"Labels are defined in `../../../codebooks/{sheet.sheet_id}.md`. "
        f"Record your answer in `../../../ratings/<your-rater-id>/{sheet.sheet_id}/{unit_id}.md`.",
        "",
    ]

    if member.get("source"):
        files["excerpt.cs"] = member["source"]
        body += ["## Member under judgement", "", _fence(member["source"]), ""]

    if member.get("class_source") and member.get("class_source") != member.get("source"):
        files["context.cs"] = member["class_source"]
        body += [
            "## Containing class",
            "",
            "Full text in `context.cs`.",
            "",
        ]

    # Sheet-specific context. Nothing here may reveal the answer on a blind sheet.
    if sheet.sheet_id == "smells_attribution":
        body += [
            "## The finding under judgement",
            "",
            f"- **Smell:** `{unit.get('smell_name', '')}`",
            f"- **Reported at:** `{unit.get('claimed_file_path', '')}:{unit.get('claimed_line', '')}`",
            f"- **Reported type / method:** `{unit.get('containing_type_name', '')}` / "
            f"`{unit.get('test_method_name', '')}`",
            "",
        ]
        detected = reader.smells_for_member(repo_key, unit["member_id"])
        if detected:
            body += [
                "## Every smell detected on this member",
                "",
                "| Smell | Line | Message |",
                "|---|---|---|",
            ]
            for smell in detected:
                message = (smell["message"] or "").replace("|", "\\|").replace("\n", " ")
                body.append(
                    f"| `{smell['smell_name']}` | {smell['line']} | {message[:160]} |"
                )
            body.append("")

    if sheet.sheet_id in ("mapping_exercise", "mapping_attribution"):
        test_member = reader.member(repo_key, unit["test_member_id"])
        if test_member.get("source"):
            files["test_member.cs"] = test_member["source"]
            body += [
                "## Test member (the mapping's test end)",
                "",
                f"`{test_member.get('class_name', '')}.{test_member.get('name', '')}` "
                f"— `{test_member.get('file_path', '')}`",
                "",
                _fence(test_member["source"]),
                "",
            ]

        steps = reader.trace_steps(repo_key, unit["observation_id"])
        production = f"{member.get('class_name', '')}.{member.get('name', '')}"
        test_name = f"{test_member.get('class_name', '')}.{test_member.get('name', '')}"
        body += [
            "## Trace",
            "",
            f"**Test end:** `{test_name}`  ",
            f"**Production end:** `{production}`  ",
            f"**Evidence:** `{unit.get('evidence_kind', '')}`, "
            f"path length `{unit.get('path_length', '')}`, "
            f"confidence `{unit.get('confidence', '')}`",
            "",
        ]
        if steps:
            body += [
                "Every hop TestMap recorded, in order:",
                "",
                "| # | From | Relationship | To | Edge source |",
                "|---|---|---|---|---|",
            ]
            for step in steps:
                body.append(
                    f"| {step['step']} | `{step['from_name']}` | `{step['relationship']}` "
                    f"| `{step['to_name']}` | `{step['edge_source']}` |"
                )
            body += ["", "Step summaries:", ""]
            for step in steps:
                body.append(f"- {step['step']}. {step['summary']}")
            body.append("")
        else:
            body += ["_No trace steps were recorded for this mapping._", ""]

    if sheet.sheet_id == "mapping_recall":
        names = reader.test_class_names(repo_key)
        if names:
            body += [
                "## Test classes in this repository",
                "",
                ", ".join(f"`{n}`" for n in names),
                "",
            ]

    if sheet.sheet_id == "metrics_attribution":
        body += [
            "## Reported metrics",
            "",
            f"- maintainability_index: `{unit.get('maintainability_index', '')}`",
            f"- cyclomatic_complexity: `{unit.get('cyclomatic_complexity', '')}`",
            f"- source_lines_of_code: `{unit.get('source_lines_of_code', '')}`",
            "",
        ]

    if sheet.sheet_id == "mutants_attribution":
        mutants, total = reader.mutants_for_member(repo_key, unit["member_id"])
        body += [
            "## Mutant summary for this member",
            "",
            f"- total: `{unit.get('mutants_total', '')}`",
            f"- killed: `{unit.get('killed', '')}` / survived: `{unit.get('survived', '')}` "
            f"/ no coverage: `{unit.get('nocoverage', '')}`",
            f"- distinct mutators: `{unit.get('distinct_mutators', '')}`",
            "",
        ]
        if mutants:
            shown = len(mutants)
            body += [
                f"## The mutants themselves ({shown} of {total} shown)",
                "",
                "Each row is one mutation Stryker generated. Judge whether these "
                "mutations belong to the member above.",
                "",
            ]
            for mutant in mutants:
                body += [
                    f"### Mutant `{mutant['stryker_mutant_id']}` — {mutant['mutator_name']}",
                    "",
                    f"Lines {mutant['start_line']}–{mutant['end_line']}, "
                    f"status `{mutant['status']}`"
                    + (f" ({mutant['status_reason']})" if mutant['status_reason'] else "")
                    + (" · static" if mutant["is_static"] else ""),
                    "",
                    "Original:",
                    "",
                    _fence(mutant["original_code"] or "(not recorded)"),
                    "",
                    "Mutated to:",
                    "",
                    _fence(mutant["replacement"] or "(not recorded)"),
                    "",
                ]
            files["mutants.json"] = json.dumps(mutants, indent=2)

    files["unit.md"] = "\n".join(body)
    files["meta.json"] = json.dumps({
        "unit_id": unit_id,
        "sheet": sheet.sheet_id,
        "kind": sheet.kind,
        "repo_key": repo_key,
        "member_id": int(unit["member_id"]),
        "observation_id": int(unit["observation_id"]) if "observation_id" in unit
                          and pd.notna(unit.get("observation_id")) else None,
        "stratum": unit.get("stratum", ""),
        "file_path": member.get("file_path", ""),
    }, indent=2)

    return files


def render_form(sheet: Sheet, unit_id: str, rater: str) -> str:
    """Return the rating form for one unit and rater."""
    field = "labels: []" if sheet.kind == "multi" else "label: "
    hint = (
        "# any number of labels from the codebook; [] means none apply"
        if sheet.kind == "multi"
        else f"# exactly one of: {', '.join(sheet.labels)}"
    )
    return "\n".join([
        f"# {sheet.sheet_id} — {unit_id}",
        "",
        f"**Rater:** `{rater}`",
        "",
        f"Question: {sheet.question}",
        "",
        f"Unit and code: `../../../samples/{sheet.sheet_id}/{unit_id}/unit.md`",
        f"Codebook: `../../../codebooks/{sheet.sheet_id}.md`",
        "",
        "Fill in the block below. Do not change the keys, and leave the fences intact.",
        "",
        "```yaml",
        f"unit_id: {unit_id}",
        f"rater: {rater}",
        f"{field}  {hint}",
        "confidence:   # high | medium | low",
        'notes: ""',
        "```",
        "",
    ])


def write_codebook(sheet: Sheet, path: Path) -> None:
    lines = [
        f"# Codebook — {sheet.sheet_id}",
        "",
        f"**Question.** {sheet.question}",
        "",
        sheet.instructions,
        "",
        "Freeze this file before rating begins. If the calibration round shows a "
        "definition is unclear, fix it then — not after rating starts.",
        "",
        "## Labels",
        "",
    ]
    if sheet.kind == "multi":
        lines += [
            "Select every label that applies; an empty list means none do.",
            "",
        ]
    for label in sheet.labels:
        lines += [f"### `{label}`", "", "_Definition:_", "", "_Worked example:_", ""]
    lines += [
        "## Confidence",
        "",
        "`high` — the excerpt settles it. `medium` — reasonably sure. "
        "`low` — guessing; the excerpt is not enough.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def run(
    frames_dir: str,
    data_dir: str,
    output_dir: str,
    seed: int = 20260823,
    n_per_sheet: int = 50,
    raters: tuple[str, ...] = ("rater_a", "rater_b"),
    sheets: Optional[tuple[str, ...]] = None,
) -> None:
    """Entry point for the ``msr-validate`` CLI command."""
    out = ensure_output_dir(output_dir)
    frames = load_frames(frames_dir)
    if frames["entities"].empty:
        print("No entity frame found. Run build-msr-datasets first.")
        return

    reader = SourceReader(data_dir)
    selected = list(sheets) if sheets else DEFAULT_SHEETS

    # Smell labels come from the corpus so the codebook matches what can appear.
    smell_labels = sorted(set(frames["test_smells"]["smell_name"].dropna())) \
        if not frames["test_smells"].empty else []
    for sheet_id in ("smells_correctness", "smells_recall"):
        SHEETS[sheet_id].labels = smell_labels

    codebooks = ensure_output_dir(str(out / "codebooks"))
    answers_dir = ensure_output_dir(str(out / "answers"))
    manifest_rows = []

    for sheet_id in selected:
        sheet = SHEETS[sheet_id]
        pool = candidate_units(sheet_id, frames)
        if pool.empty:
            print(f"[skip] {sheet_id}: no candidate units")
            continue

        pool = assign_strata(pool, frames["entities"], sheet)
        drawn = stratified_draw(pool, n_per_sheet, seed)

        write_codebook(sheet, Path(codebooks) / f"{sheet_id}.md")
        sample_root = ensure_output_dir(str(out / "samples" / sheet_id))
        answer_rows = []

        for index, (_, unit) in enumerate(drawn.iterrows(), start=1):
            unit_id = f"{sheet_id}-{index:04d}"
            unit_dir = ensure_output_dir(str(Path(sample_root) / unit_id))
            for filename, content in render_unit(sheet, unit_id, unit, reader, frames).items():
                (Path(unit_dir) / filename).write_text(content, encoding="utf-8")

            for rater in raters:
                form_dir = ensure_output_dir(str(out / "ratings" / rater / sheet_id))
                (Path(form_dir) / f"{unit_id}.md").write_text(
                    render_form(sheet, unit_id, rater), encoding="utf-8"
                )

            answer_rows.append({
                "unit_id": unit_id,
                "sheet": sheet_id,
                "repo_key": unit["repo_key"],
                "member_id": int(unit["member_id"]),
                "stratum": unit.get("stratum", ""),
                **{f"testmap_{k}": v for k, v in sheet_answer(sheet_id, unit, frames).items()},
            })
            manifest_rows.append({
                "unit_id": unit_id, "sheet": sheet_id,
                "repo_key": unit["repo_key"], "stratum": unit.get("stratum", ""),
            })

        pd.DataFrame(answer_rows).to_csv(Path(answers_dir) / f"{sheet_id}.csv", index=False)
        counts = drawn["stratum"].value_counts().to_dict()
        print(f"{sheet_id:<24}{len(drawn):>4} units   pool {len(pool):>8,}   strata {counts}")

    manifest = pd.DataFrame(manifest_rows)
    manifest.to_csv(out / "manifest.csv", index=False)

    (out / "README.md").write_text("\n".join([
        "# MSR validation — human-judged sample",
        "",
        f"Seed `{seed}`, {n_per_sheet} units per sheet, raters: "
        f"{', '.join(f'`{r}`' for r in raters)}.",
        f"Drawn from `{frames_dir}`.",
        "",
        "## How to rate",
        "",
        "1. Read `codebooks/<sheet>.md` first and do the calibration round.",
        "2. For each unit, open `samples/<sheet>/<unit_id>/unit.md`.",
        "3. Record your answer in `ratings/<your-rater-id>/<sheet>/<unit_id>.md`, "
        "inside the fenced `yaml` block. Do not edit the keys.",
        "4. Rate independently. Do not read the other rater's files.",
        "",
        "## What is deliberately not shown",
        "",
        "TestMap's own output lives in `answers/<sheet>.csv` and appears in no unit or "
        "form. The smell sheets are blind: you are asked which smells are present, not "
        "whether you agree with a label. Showing the label first turns the task into "
        "confirmation and inflates agreement.",
        "",
        "## Collecting results",
        "",
        "```bash",
        f"uv run python -m analysis msr-collect --sample {output_dir} --out {output_dir}/results",
        "```",
        "",
        "Reports per-sheet agreement (Cohen's kappa, or per-label kappa and "
        "Krippendorff's alpha for the multi-label smell sheets), the proportion "
        "agreeing with TestMap with a Wilson interval, and any unfilled or malformed "
        "forms.",
        "",
        f"Units: {len(manifest):,} across {manifest['sheet'].nunique()} sheets, "
        f"{len(raters)} raters — {len(manifest) * len(raters):,} forms.",
        "",
    ]), encoding="utf-8")

    print(f"\n{len(manifest):,} units, {len(manifest) * len(raters):,} forms written to {out}")
