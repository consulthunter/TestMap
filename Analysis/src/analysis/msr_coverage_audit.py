"""Audit repositories whose coverage claim disagrees with their persisted data.

``project-validation`` reports ``HasCoverage = True`` for a repository whenever a
``coverage_reports`` row exists. That row is written even when collection produced
nothing, so the flag admits repositories with no coverage data at all: an all-zero
report header and zero ``member_coverages`` / ``object_coverages`` rows.

This module finds those repositories, classifies why collection failed by matching
the run logs, and writes an exclusion list so the empty ones can be kept out of the
consolidated frames.

Failure classes:

``collector_unavailable``  the test host could not load the coverage data collector
``collector_crashed``      the collector loaded and then died (Vanguard/CodeCoverage.exe)
``no_artifacts_produced``  the run finished but emitted no coverage or TRX files
``artifacts_not_found``    files were expected on the host side and were missing
``unclassified``           no known pattern matched; needs a manual read
"""

from __future__ import annotations

import re
import sqlite3
from pathlib import Path
from typing import Optional

import pandas as pd


# Ordered: the first class whose required patterns all match wins, so the most
# specific explanation is reported rather than a downstream symptom.
FAILURE_CLASSES = (
    ("collector_crashed", (
        r"VanguardException",
        r"Running event not received from CodeCoverage\.exe",
    )),
    ("collector_unavailable", (
        r"Unable to find a datacollector with friendly name",
    )),
    ("artifacts_not_found", (
        r"(Raw|Normalized) coverage file not found",
    )),
    ("no_artifacts_produced", (
        r"No coverage files were produced|produced \d+ TRX file\(s\) and 0 coverage file\(s\)",
    )),
)

# Recorded for every repository regardless of class, as corroborating evidence.
EVIDENCE_PATTERNS = {
    "datacollector_missing": r"Unable to find a datacollector with friendly name '([^']+)'",
    "fallback_attempted": r"Trying fallback collector",
    "zero_coverage_files": r"produced \d+ TRX file\(s\) and 0 coverage file\(s\)",
    "no_coverage_files": r"No coverage files were produced",
    "no_trx_results": r"No TRX test results were produced",
    "vanguard_exception": r"VanguardException",
    "raw_file_missing": r"Raw coverage file not found",
    "normalized_file_missing": r"Normalized coverage file not found",
    "trx_dir_missing": r"TRX results directory not found",
}


def index_log_directories(logs_root: str | Path) -> dict[str, list[Path]]:
    """Map ``owner-repo`` to its run log directories.

    Log directories are named ``<HH-MM-SS>_<owner>-<repo>`` under
    ``<root>/<date>/logs/<date>/``. A repository can have several if it was run
    on more than one date.
    """
    root = Path(logs_root)
    index: dict[str, list[Path]] = {}
    for directory in root.glob("*/logs/*/*"):
        if not directory.is_dir():
            continue
        slug = directory.name.split("_", 1)[-1].lower()
        index.setdefault(slug, []).append(directory)
    return index


def _read_logs(directories: list[Path]) -> tuple[str, list[str]]:
    """Return the concatenated log text and the files it came from."""
    text_parts: list[str] = []
    sources: list[str] = []
    for directory in sorted(directories):
        for path in sorted(directory.glob("*.log")):
            try:
                text_parts.append(path.read_text(encoding="utf-8", errors="ignore"))
                sources.append(str(path))
            except OSError as exc:
                print(f"[warn] Could not read {path}: {exc}")
    return "\n".join(text_parts), sources


def _classify(text: str) -> str:
    for name, patterns in FAILURE_CLASSES:
        if all(re.search(p, text) for p in patterns):
            return name
    return "unclassified"


def _evidence(text: str) -> dict:
    found = {}
    for name, pattern in EVIDENCE_PATTERNS.items():
        match = re.search(pattern, text)
        found[name] = bool(match)
    collector = re.search(EVIDENCE_PATTERNS["datacollector_missing"], text)
    found["missing_collector_name"] = collector.group(1) if collector else ""
    return found


def _first_matching_line(text: str, patterns: tuple[str, ...]) -> str:
    for line in text.splitlines():
        if any(re.search(p, line) for p in patterns):
            return line.strip()[:300]
    return ""


def read_coverage_state(db_path: Path) -> dict:
    """Return what the database actually holds for coverage."""
    state = {
        "coverage_reports": 0,
        "member_coverages": 0,
        "object_coverages": 0,
        "report_all_zero": None,
        "report_has_version": None,
    }
    if not db_path.exists():
        return state

    conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    try:
        reports = conn.execute(
            "SELECT line_rate, branch_rate, lines_valid, version FROM coverage_reports"
        ).fetchall()
        state["coverage_reports"] = len(reports)
        state["member_coverages"] = conn.execute(
            "SELECT COUNT(*) FROM member_coverages").fetchone()[0]
        state["object_coverages"] = conn.execute(
            "SELECT COUNT(*) FROM object_coverages").fetchone()[0]
        if reports:
            state["report_all_zero"] = all(
                r[0] == 0 and r[1] == 0 and r[2] == 0 for r in reports
            )
            state["report_has_version"] = any(bool(r[3]) for r in reports)
    except sqlite3.Error as exc:
        print(f"[warn] Could not read coverage state from {db_path}: {exc}")
    finally:
        conn.close()
    return state


def audit(
    population: pd.DataFrame,
    data_dir: str | Path,
    logs_root: str | Path,
) -> pd.DataFrame:
    """Return one row per selected repository, with its coverage claim verdict."""
    logs = index_log_directories(logs_root)
    output_root = Path(data_dir) / "Output"
    rows = []

    selected = population[population["selected"]]
    for _, row in selected.iterrows():
        owner, repo = row["repository"].split("/", 1)
        commit = str(row["resolved_commit"])
        db_path = output_root / owner / repo / commit / "analysis.db"

        state = read_coverage_state(db_path)
        has_rows = (state["member_coverages"] + state["object_coverages"]) > 0

        record = {
            "repository": row["repository"],
            "commit": commit,
            "repo_key": f"{row['repository']}@{commit[:12]}",
            "docker_os": row.get("DockerOs", ""),
            "docker_context": row.get("DockerContext", ""),
            "execution_support": row.get("ExecutionSupport", ""),
            "tests_pass": bool(row.get("TestsPass", False)),
            "has_mutation_score": bool(row.get("HasMutationScore", False)),
            "flag_has_coverage": bool(row.get("HasCoverage", False)),
            "failure_category": row.get("FailureCategory", ""),
            **state,
            "coverage_data_present": has_rows,
        }

        if has_rows:
            record.update({
                "verdict": "coverage_present",
                "failure_class": "",
                "evidence_line": "",
                "log_files": "",
            })
            rows.append(record)
            continue

        directories = logs.get(f"{owner}-{repo}".lower(), [])
        text, sources = _read_logs(directories)
        failure_class = _classify(text) if text else "no_logs_found"
        evidence = _evidence(text) if text else {}
        patterns = dict(FAILURE_CLASSES).get(failure_class, ())

        record.update({
            "verdict": "claimed_without_data",
            "failure_class": failure_class,
            "evidence_line": _first_matching_line(text, patterns) if patterns else "",
            "log_files": ";".join(sources),
            **evidence,
        })
        rows.append(record)

    return pd.DataFrame(rows)


def _markdown_table(frame: pd.DataFrame, index: bool = False) -> str:
    """Render a DataFrame as a GitHub markdown table.

    Written out rather than using ``DataFrame.to_markdown`` so the package does
    not gain a ``tabulate`` dependency for one report.
    """
    if frame.empty:
        return "_(none)_"
    table = frame.reset_index() if index else frame
    headers = [str(c) for c in table.columns]
    lines = ["| " + " | ".join(headers) + " |",
             "|" + "|".join("---" for _ in headers) + "|"]
    for _, row in table.iterrows():
        cells = ["" if pd.isna(v) else str(v).replace("|", "\\|") for v in row]
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def write_findings(audit_frame: pd.DataFrame, path: Path) -> None:
    """Write the human-readable findings note."""
    claimed = audit_frame[audit_frame["verdict"] == "claimed_without_data"]
    total = len(audit_frame)

    by_class = (
        claimed.groupby("failure_class").size().rename("repositories")
        .reset_index().sort_values("repositories", ascending=False)
    )
    by_os = pd.crosstab(audit_frame["docker_os"], audit_frame["coverage_data_present"])

    lines = [
        "# Finding: `HasCoverage` reported without coverage data",
        "",
        f"{len(claimed)} of {total} selected repositories report `HasCoverage = True` in "
        "`project-validation-total.csv` while their `analysis.db` holds no "
        "`member_coverages` or `object_coverages` rows.",
        "",
        "## Mechanism",
        "",
        "A `coverage_reports` row is persisted even when coverage collection produced "
        "nothing. The row carries `line_rate = 0`, `branch_rate = 0`, `lines_valid = 0`, "
        "and an empty `version`. `HasCoverage` is derived from the presence of that row "
        "rather than from its contents, so the flag reports success for a run that "
        "collected no coverage.",
        "",
        "The tests themselves often passed. This is not a test failure — it is a coverage "
        "collection failure recorded as a success.",
        "",
        "## Two distinct failures",
        "",
        "The affected repositories split by whether the persisted report header is empty, "
        "and the two halves point at different code:",
        "",
        f"- **{int((claimed['report_all_zero'] == True).sum())} empty-placeholder reports** "
        "— `line_rate`, `branch_rate`, and `lines_valid` all zero, `version` empty. "
        "Collection produced nothing and a stub row was written anyway. The bug is that "
        "the stub is persisted at all, and that `HasCoverage` keys off its presence.",
        f"- **{int((claimed['report_all_zero'] == False).sum())} populated reports with no "
        "per-entity rows** — the report header carries real rates and a version string, but "
        "no `member_coverages` or `object_coverages` rows were written. Collection worked; "
        "the step that maps report entries onto members did not. This is a separate defect.",
        "",
        "## Failure classes",
        "",
        _markdown_table(by_class),
        "",
        "## Environment",
        "",
        "Coverage data present, by container OS:",
        "",
        _markdown_table(by_os, index=True),
        "",
    ]

    windows = audit_frame[audit_frame["docker_os"] == "windows"]
    linux = audit_frame[audit_frame["docker_os"] == "linux"]
    if len(windows) and len(linux):
        win_rate = 1 - windows["coverage_data_present"].mean()
        lin_rate = 1 - linux["coverage_data_present"].mean()
        lines += [
            f"Windows containers fail at {win_rate:.1%} against {lin_rate:.1%} on Linux — "
            "over-represented, but not exclusive: the majority of affected repositories "
            f"({int((~linux['coverage_data_present']).sum())} of {len(claimed)}) ran on Linux.",
            "",
        ]

    lines += [
        "## Consequence for validation",
        "",
        "These repositories contribute every member to `members_missing_coverage` while "
        "having had no opportunity to be covered, which inflates that gap rate. They are "
        "written to the exclusion list and dropped from the consolidated frames.",
        "",
        "`HasCoverage` should not be used as an eligibility predicate. The reliable test "
        "is whether `member_coverages` rows exist.",
        "",
        "## Affected repositories",
        "",
        _markdown_table(claimed[[
            "repository", "commit", "docker_os", "failure_class",
            "tests_pass", "has_mutation_score", "coverage_reports",
        ]]),
        "",
    ]

    path.write_text("\n".join(lines), encoding="utf-8")


def run(
    population_csv: str,
    data_dir: str,
    logs_root: str,
    output_dir: str,
) -> None:
    """Entry point for the ``msr-coverage-audit`` CLI command."""
    from analysis.files import ensure_output_dir

    out = ensure_output_dir(output_dir)
    population = pd.read_csv(population_csv)
    audit_frame = audit(population, data_dir, logs_root)

    audit_frame.to_csv(out / "coverage_claim_audit.csv", index=False)

    claimed = audit_frame[audit_frame["verdict"] == "claimed_without_data"]
    exclusions = claimed[["repo_key", "repository", "commit", "failure_class"]].copy()
    exclusions.to_csv(out / "coverage_exclusions.csv", index=False)

    write_findings(audit_frame, out / "FINDING_coverage_claimed_without_data.md")

    total = len(audit_frame)
    print(f"Selected repositories audited: {total:,}")
    print(f"  coverage data present   {total - len(claimed):,}")
    print(f"  claimed without data    {len(claimed):,} ({len(claimed) / total:.1%})\n")

    print("Failure class")
    print(claimed.groupby("failure_class").size().rename("repositories").to_string())

    print("\nCoverage data present by container OS")
    print(pd.crosstab(audit_frame["docker_os"], audit_frame["coverage_data_present"]).to_string())

    print(f"\nReports written to {out}")
    print(f"  coverage_claim_audit.csv                   {total:,} rows")
    print(f"  coverage_exclusions.csv                    {len(exclusions):,} rows")
    print("  FINDING_coverage_claimed_without_data.md")
