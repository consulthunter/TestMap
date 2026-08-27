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
        "coverage_policy": "",
        "coverage_status": "",
        "has_usable_coverage": False,
        "raw_object_count": None,
        "mapped_object_count": None,
        "raw_member_count": None,
        "mapped_member_count": None,
        "pending_attribution_rows": 0,
        "terminal_rows_missing_reason": 0,
        "impossible_counter_rows": 0,
        "reconciliation_valid": None,
        "corrected_schema": False,
    }
    if not db_path.exists():
        return state

    conn = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    try:
        report_columns = {row[1] for row in conn.execute("PRAGMA table_info(coverage_reports)")}
        corrected = {
            "collection_status", "has_usable_coverage", "measurement_policy_version",
            "raw_object_count", "mapped_object_count", "raw_member_count", "mapped_member_count"
        }.issubset(report_columns)
        state["corrected_schema"] = corrected
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
        if corrected:
            latest = conn.execute("""
                SELECT id, measurement_policy_version, collection_status, has_usable_coverage,
                       raw_object_count, mapped_object_count, raw_member_count, mapped_member_count
                FROM coverage_reports ORDER BY id DESC LIMIT 1
            """).fetchone()
            if latest:
                report_id, policy, status, usable, raw_objects, mapped_objects, raw_members, mapped_members = latest
                state.update({
                    "coverage_policy": policy or "",
                    "coverage_status": status or "",
                    "has_usable_coverage": bool(usable),
                    "raw_object_count": raw_objects,
                    "mapped_object_count": mapped_objects,
                    "raw_member_count": raw_members,
                    "mapped_member_count": mapped_members,
                })
                object_counts = conn.execute("""
                    SELECT COUNT(*),
                           SUM(CASE WHEN object_id IS NOT NULL AND attribution_status = 'Mapped' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN attribution_status = 'Pending' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN attribution_status NOT IN ('Pending','Mapped') AND
                                         COALESCE(attribution_reason, '') = '' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN (line_counts_available = 1 AND lines_covered > lines_valid) OR
                                         (branch_counts_available = 1 AND branches_covered > branches_valid)
                                    THEN 1 ELSE 0 END)
                    FROM object_coverages WHERE coverage_report_id = ?
                """, (report_id,)).fetchone()
                member_counts = conn.execute("""
                    SELECT COUNT(*),
                           SUM(CASE WHEN member_id IS NOT NULL AND attribution_status = 'Mapped' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN attribution_status = 'Pending' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN attribution_status NOT IN ('Pending','Mapped') AND
                                         COALESCE(attribution_reason, '') = '' THEN 1 ELSE 0 END),
                           SUM(CASE WHEN (line_counts_available = 1 AND lines_covered > lines_valid) OR
                                         (branch_counts_available = 1 AND branches_covered > branches_valid)
                                    THEN 1 ELSE 0 END)
                    FROM member_coverages WHERE coverage_report_id = ?
                """, (report_id,)).fetchone()
                actual_raw_objects, actual_mapped_objects = object_counts[0], object_counts[1] or 0
                actual_raw_members, actual_mapped_members = member_counts[0], member_counts[1] or 0
                state["pending_attribution_rows"] = (object_counts[2] or 0) + (member_counts[2] or 0)
                state["terminal_rows_missing_reason"] = (object_counts[3] or 0) + (member_counts[3] or 0)
                state["impossible_counter_rows"] = (object_counts[4] or 0) + (member_counts[4] or 0)
                state["reconciliation_valid"] = (
                    raw_objects == actual_raw_objects and mapped_objects == actual_mapped_objects and
                    raw_members == actual_raw_members and mapped_members == actual_mapped_members
                )
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
        corrected = state["coverage_policy"] == "coverage-integrity-v1"
        corrected_valid = (
            state["reconciliation_valid"] is True and
            state["pending_attribution_rows"] == 0 and
            state["terminal_rows_missing_reason"] == 0 and
            state["impossible_counter_rows"] == 0
        )
        coverage_present = bool(state["has_usable_coverage"] and corrected and corrected_valid)
        effective_coverage_present = coverage_present if state["corrected_schema"] else has_rows

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
            "coverage_data_present": effective_coverage_present,
            "false_has_coverage_claim": (
                bool(row.get("HasCoverage", False)) and not effective_coverage_present
            ),
        }

        if state["corrected_schema"] and corrected:
            if not corrected_valid:
                record["verdict"] = "corrected_invalid"
            elif coverage_present:
                record["verdict"] = "coverage_present"
            else:
                record["verdict"] = "coverage_unavailable"
            record.update({"failure_class": "", "evidence_line": "", "log_files": ""})
            rows.append(record)
            continue

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
    claimed = audit_frame[audit_frame["false_has_coverage_claim"]]
    legacy_claimed = claimed[claimed["verdict"] == "claimed_without_data"]
    corrected_invalid = audit_frame[audit_frame["verdict"] == "corrected_invalid"]
    total = len(audit_frame)

    by_class = (
        claimed.groupby("failure_class").size().rename("repositories")
        .reset_index().sort_values("repositories", ascending=False)
    )
    by_os = pd.crosstab(audit_frame["docker_os"], audit_frame["coverage_data_present"])

    lines = [
        "# Finding: `HasCoverage` reported without coverage data",
        "",
        f"{len(claimed)} of {total} selected repositories report `HasCoverage = True` without "
        "usable coverage under the schema that produced the row. Corrected reports must also "
        "pass policy, reconciliation, attribution, reason, and counter-integrity checks.",
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
        f"- **{int((legacy_claimed['report_all_zero'] == True).sum())} empty-placeholder reports** "
        "— `line_rate`, `branch_rate`, and `lines_valid` all zero, `version` empty. "
        "Collection produced nothing and a stub row was written anyway. The bug is that "
        "the stub is persisted at all, and that `HasCoverage` keys off its presence.",
        f"- **{int((legacy_claimed['report_all_zero'] == False).sum())} populated reports with no "
        "per-entity rows** — the report header carries real rates and a version string, but "
        "no `member_coverages` or `object_coverages` rows were written. Collection worked; "
        "the step that maps report entries onto members did not. This is a separate defect.",
        f"- **{len(corrected_invalid)} corrected reports failed integrity checks** — these are "
        "publication blockers even if a usable-coverage flag was persisted.",
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
        "Legacy eligibility requires actual coverage rows. Corrected eligibility requires the "
        "corrected policy, a usable terminal status, complete attribution, valid reconciliation, "
        "and valid available counters.",
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

    publication_blocked = audit_frame[
        audit_frame["false_has_coverage_claim"] |
        (audit_frame["verdict"] == "corrected_invalid")
    ]
    claimed = audit_frame[audit_frame["false_has_coverage_claim"]]
    exclusions = publication_blocked[
        ["repo_key", "repository", "commit", "failure_class"]
    ].copy()
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
