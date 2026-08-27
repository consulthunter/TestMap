"""Collect filled rating forms and compute inter-rater agreement.

Reads the fenced YAML block out of every form under ``ratings/<rater>/<sheet>/``,
validates it, and reports:

- **coverage of the task** — how many forms are filled, blank, or malformed;
- **agreement between raters** — Cohen's kappa for single-label sheets, per-label
  kappa plus Krippendorff's alpha for the multi-label smell sheets;
- **agreement with TestMap** — the proportion of units where the raters' verdict
  matches what the tool recorded, with a Wilson interval.

Agreement is computed on the **pre-adjudication** ratings. Agreement measured
after disagreements are resolved is meaningless, so adjudication is recorded in a
separate column and never overwrites the raters' original answers.

Cohen's kappa, Krippendorff's alpha, and the Wilson interval are implemented here
rather than pulled in: each is a few lines, and the package already avoids
optional statistics dependencies.
"""

from __future__ import annotations

import json
import math
import re
from itertools import combinations
from pathlib import Path
from typing import Optional

import pandas as pd
import yaml


YAML_BLOCK = re.compile(r"```yaml\s*\n(.*?)```", re.S)

REQUIRED_KEYS = {"unit_id", "rater"}


# ---------------------------------------------------------------------------
# Statistics
# ---------------------------------------------------------------------------

def cohens_kappa(a: list, b: list) -> Optional[float]:
    """Cohen's kappa for two raters over paired nominal labels."""
    if not a or len(a) != len(b):
        return None
    categories = sorted(set(a) | set(b))
    n = len(a)
    observed = sum(1 for x, y in zip(a, b) if x == y) / n

    expected = 0.0
    for category in categories:
        pa = sum(1 for x in a if x == category) / n
        pb = sum(1 for y in b if y == category) / n
        expected += pa * pb

    if expected >= 1.0:
        # Both raters used a single category throughout: agreement is total but
        # kappa is undefined (no variance to correct for).
        return None
    return (observed - expected) / (1 - expected)


def krippendorff_alpha_nominal(units: list[list]) -> Optional[float]:
    """Krippendorff's alpha for nominal data.

    *units* is one list of ratings per unit; units with fewer than two ratings
    contribute nothing, as in the standard definition.
    """
    usable = [u for u in units if len(u) >= 2]
    if not usable:
        return None

    # Observed disagreement: ordered disagreeing pairs within each unit,
    # normalized by (m - 1) and averaged over all pairable values.
    values: list = []
    pairable = 0
    numerator = 0.0
    for unit in usable:
        m = len(unit)
        values.extend(unit)
        pairable += m
        disagreements = sum(2 for x, y in combinations(unit, 2) if x != y)
        numerator += disagreements / (m - 1)
    do = numerator / pairable if pairable else 0.0

    total = len(values)
    if total < 2:
        return None
    counts: dict = {}
    for value in values:
        counts[value] = counts.get(value, 0) + 1
    de = 1.0 - sum(c * (c - 1) for c in counts.values()) / (total * (total - 1))

    if de == 0:
        return None
    return 1 - do / de


def wilson_interval(successes: int, trials: int, z: float = 1.96) -> tuple[float, float]:
    """Wilson score interval for a binomial proportion."""
    if trials == 0:
        return (float("nan"), float("nan"))
    p = successes / trials
    denominator = 1 + z ** 2 / trials
    centre = (p + z ** 2 / (2 * trials)) / denominator
    margin = z * math.sqrt(p * (1 - p) / trials + z ** 2 / (4 * trials ** 2)) / denominator
    return (max(0.0, centre - margin), min(1.0, centre + margin))


# ---------------------------------------------------------------------------
# Collection
# ---------------------------------------------------------------------------

def parse_form(path: Path) -> dict:
    """Parse one rating form. Returns a record with a ``status`` field."""
    record = {
        "path": str(path),
        "unit_id": path.stem,
        "rater": path.parent.parent.name,
        "sheet": path.parent.name,
        "status": "ok",
        "label": None,
        "labels": None,
        "confidence": None,
        "notes": "",
        "problem": "",
    }

    try:
        text = path.read_text(encoding="utf-8")
    except OSError as exc:
        record.update(status="unreadable", problem=str(exc))
        return record

    match = YAML_BLOCK.search(text)
    if not match:
        record.update(status="malformed", problem="no fenced yaml block")
        return record

    try:
        parsed = yaml.safe_load(match.group(1))
    except yaml.YAMLError as exc:
        record.update(status="malformed", problem=f"yaml error: {exc}")
        return record

    if not isinstance(parsed, dict):
        record.update(status="malformed", problem="yaml block is not a mapping")
        return record

    missing = REQUIRED_KEYS - set(parsed)
    if missing:
        record.update(status="malformed", problem=f"missing keys: {sorted(missing)}")
        return record

    if str(parsed["unit_id"]) != record["unit_id"]:
        record.update(status="malformed",
                      problem=f"unit_id {parsed['unit_id']!r} does not match filename")
        return record
    if str(parsed["rater"]) != record["rater"]:
        record.update(status="malformed",
                      problem=f"rater {parsed['rater']!r} does not match directory")
        return record

    record["confidence"] = parsed.get("confidence") or None
    record["notes"] = parsed.get("notes") or ""

    if "labels" in parsed:
        labels = parsed.get("labels")
        if labels is None:
            labels = []
        if not isinstance(labels, list):
            record.update(status="malformed", problem="labels is not a list")
            return record
        record["labels"] = sorted(str(x) for x in labels)
        # An empty list is a real answer on the smell sheets ("no smells present"),
        # so emptiness alone does not make a form unfilled.
        if parsed.get("confidence") in (None, ""):
            record["status"] = "unfilled"
    elif "label" in parsed:
        label = parsed.get("label")
        if label in (None, ""):
            record["status"] = "unfilled"
        else:
            record["label"] = str(label)
    else:
        record.update(status="malformed", problem="neither label nor labels present")

    return record


def collect_ratings(sample_dir: str | Path) -> pd.DataFrame:
    """Parse every rating form under ``<sample_dir>/ratings``."""
    root = Path(sample_dir) / "ratings"
    if not root.exists():
        return pd.DataFrame()
    records = [parse_form(path) for path in sorted(root.glob("*/*/*.md"))]
    return pd.DataFrame(records)


def load_answers(sample_dir: str | Path) -> dict[str, pd.DataFrame]:
    """Load TestMap's own values per sheet."""
    answers_dir = Path(sample_dir) / "answers"
    if not answers_dir.exists():
        return {}
    return {
        path.stem: pd.read_csv(path)
        for path in sorted(answers_dir.glob("*.csv"))
    }


# ---------------------------------------------------------------------------
# Agreement
# ---------------------------------------------------------------------------

def _paired(ratings: pd.DataFrame, sheet: str) -> pd.DataFrame:
    """Return one row per unit with a column per rater, for filled forms only."""
    usable = ratings[(ratings["sheet"] == sheet) & (ratings["status"] == "ok")]
    if usable.empty:
        return usable
    value = "labels" if usable["labels"].notna().any() else "label"
    wide = usable.pivot_table(
        index="unit_id", columns="rater", values=value, aggfunc="first"
    )
    return wide.dropna()


def sheet_agreement(ratings: pd.DataFrame, sheet: str) -> dict:
    """Return agreement statistics for one sheet."""
    wide = _paired(ratings, sheet)
    result = {"sheet": sheet, "units_rated_by_all": len(wide)}
    if wide.empty or wide.shape[1] < 2:
        result["note"] = "fewer than two raters with filled forms"
        return result

    raters = list(wide.columns)
    result["raters"] = ",".join(raters)
    first, second = wide[raters[0]], wide[raters[1]]
    multi = isinstance(first.iloc[0], list)

    if not multi:
        a, b = list(first), list(second)
        result["percent_agreement"] = sum(1 for x, y in zip(a, b) if x == y) / len(a)
        result["cohens_kappa"] = cohens_kappa(a, b)
        result["krippendorff_alpha"] = krippendorff_alpha_nominal(
            [[x, y] for x, y in zip(a, b)]
        )
        return result

    # Multi-label: one binary decision per (unit, label).
    label_space = sorted({label for value in list(first) + list(second) for label in value})
    per_label = []
    binary_units = []
    exact = 0
    jaccard_total = 0.0
    for unit_a, unit_b in zip(first, second):
        set_a, set_b = set(unit_a), set(unit_b)
        exact += int(set_a == set_b)
        union = set_a | set_b
        jaccard_total += 1.0 if not union else len(set_a & set_b) / len(union)

    for label in label_space:
        a = [label in value for value in first]
        b = [label in value for value in second]
        binary_units.extend([[x, y] for x, y in zip(a, b)])
        per_label.append({
            "label": label,
            "kappa": cohens_kappa(a, b),
            "rater_a_positive": sum(a),
            "rater_b_positive": sum(b),
            "both_positive": sum(1 for x, y in zip(a, b) if x and y),
        })

    result["exact_set_agreement"] = exact / len(first)
    result["mean_jaccard"] = jaccard_total / len(first)
    result["krippendorff_alpha"] = krippendorff_alpha_nominal(binary_units)
    result["per_label"] = per_label
    return result


def agreement_with_testmap(
    ratings: pd.DataFrame,
    answers: dict[str, pd.DataFrame],
    sheet: str,
) -> Optional[dict]:
    """Return how often the raters' agreed verdict matches TestMap's own output.

    Only units where both raters gave the same answer are counted: a unit the
    raters disagree on has no verdict until adjudication, and forcing one would
    bury the disagreement rather than report it.
    """
    wide = _paired(ratings, sheet)
    if wide.empty or wide.shape[1] < 2 or sheet not in answers:
        return None

    raters = list(wide.columns)
    answer_frame = answers[sheet].set_index("unit_id")
    matched = agreed = 0

    for unit_id, row in wide.iterrows():
        first, second = row[raters[0]], row[raters[1]]
        if isinstance(first, list):
            if set(first) != set(second):
                continue
            agreed += 1
            if unit_id not in answer_frame.index:
                continue
            recorded = answer_frame.loc[unit_id].get("testmap_labels", "[]")
            try:
                tool_labels = set(json.loads(str(recorded).replace("'", '"')))
            except (ValueError, TypeError):
                tool_labels = set()
            matched += int(tool_labels == set(first))
        else:
            if first != second:
                continue
            agreed += 1
            # Attribution and exercise sheets encode "TestMap was right" directly
            # in the label, so the rater's verdict is the comparison.
            matched += int(first in ("correct", "exercised"))

    if agreed == 0:
        return None
    low, high = wilson_interval(matched, agreed)
    return {
        "sheet": sheet,
        "units_with_agreed_verdict": agreed,
        "matching_testmap": matched,
        "proportion": matched / agreed,
        "wilson_low": low,
        "wilson_high": high,
    }


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def run(sample_dir: str, output_dir: str) -> None:
    """Entry point for the ``msr-collect`` CLI command."""
    from analysis.files import ensure_output_dir

    out = ensure_output_dir(output_dir)
    ratings = collect_ratings(sample_dir)
    if ratings.empty:
        print(f"No rating forms found under {sample_dir}/ratings")
        return

    answers = load_answers(sample_dir)
    ratings.to_csv(out / "ratings_long.csv", index=False)

    status = (
        ratings.groupby(["sheet", "rater", "status"]).size()
        .rename("forms").reset_index()
    )
    status.to_csv(out / "form_status.csv", index=False)

    problems = ratings[ratings["status"].isin(["malformed", "unreadable"])]
    if not problems.empty:
        problems[["path", "status", "problem"]].to_csv(out / "form_problems.csv", index=False)

    agreement_rows = []
    per_label_rows = []
    for sheet in sorted(ratings["sheet"].unique()):
        result = sheet_agreement(ratings, sheet)
        for entry in result.pop("per_label", []):
            per_label_rows.append({"sheet": sheet, **entry})
        agreement_rows.append(result)

    agreement = pd.DataFrame(agreement_rows)
    agreement.to_csv(out / "agreement.csv", index=False)
    if per_label_rows:
        pd.DataFrame(per_label_rows).to_csv(out / "agreement_per_label.csv", index=False)

    testmap_rows = [
        row for sheet in sorted(ratings["sheet"].unique())
        if (row := agreement_with_testmap(ratings, answers, sheet)) is not None
    ]
    if testmap_rows:
        pd.DataFrame(testmap_rows).to_csv(out / "agreement_with_testmap.csv", index=False)

    total = len(ratings)
    filled = int((ratings["status"] == "ok").sum())
    print(f"Forms: {total:,}")
    print(f"  filled     {filled:,} ({filled / total:.1%})")
    for state in ("unfilled", "malformed", "unreadable"):
        count = int((ratings["status"] == state).sum())
        if count:
            print(f"  {state:<10} {count:,}")

    print("\nInter-rater agreement (pre-adjudication)")
    print(agreement.to_string(index=False))

    if per_label_rows:
        print("\nPer-label kappa")
        print(pd.DataFrame(per_label_rows).to_string(index=False))

    if testmap_rows:
        print("\nAgreement with TestMap, on units where both raters agree")
        print(pd.DataFrame(testmap_rows).to_string(index=False))

    if not problems.empty:
        print(f"\n{len(problems)} malformed or unreadable forms — see form_problems.csv")

    print(f"\nResults written to {out}")
