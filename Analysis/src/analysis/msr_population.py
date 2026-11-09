"""Select the MSR validation population from pipeline execution and validation reports.

Three inputs describe what happened to each target:

``finished-total.yaml`` / ``failed-total.yaml``
    The target manifests. A target in the failed manifest never reported back, so
    it is counted as a **timeout** regardless of how far it got.
``target-execution-total.csv``
    Per-target pipeline status, failure stage, and failure kind.
``project-validation-total.csv``
    Per-repository capability flags: did it restore, build, run tests, pass tests,
    produce coverage, produce a mutation score.

The validation flags cascade — ``Restores`` >= ``Builds`` >= ``TestsRun`` >=
``HasCoverage`` >= ``HasMutationScore`` — so the eligibility predicates below are
written out in full for legibility rather than reduced to their last term.

Two populations come out:

``eligible_core``      restored, built, and ran tests. The frame for code metrics,
                       test smells, coverage, and mappings.
``eligible_mutation``  produced a mutation score. The frame for mutants only.

Everything else is excluded, with the reason recorded.
"""

from __future__ import annotations

import re
import shutil
from pathlib import Path
from typing import Optional

import pandas as pd


VALIDATION_FLAGS = (
    "Restores",
    "Builds",
    "TestsRun",
    "TestsPass",
    "HasCoverage",
    "HasMutationScore",
)

# Order matters: eligibility is reported as the first predicate that fails.
#
# HasCoverage is deliberately absent. It is derived from a coverage_reports row
# existing rather than from that row holding anything, so it admits repositories
# with no coverage data at all (see msr_validation_bugs.md, bug 1). Selection
# stops at "the tests ran"; whether coverage data actually landed is settled by
# msr-coverage-audit against the database, not by the flag.
CORE_PREDICATES = ("Restores", "Builds", "TestsRun")


# ---------------------------------------------------------------------------
# Inputs
# ---------------------------------------------------------------------------

def _manifest_repositories(path: Path) -> list[str]:
    """Return the repository slugs listed in a target manifest.

    Parsed with a line scan rather than a YAML load: the manifests are large and
    only the ``repository:`` field of each target is needed here.
    """
    if not path.exists():
        print(f"[warn] Manifest not found: {path}")
        return []
    text = path.read_text(encoding="utf-8")
    return re.findall(r"^\s*repository:\s*(\S+)", text, re.M)


def load_targets(total_dir: str | Path) -> pd.DataFrame:
    """Return one row per target: manifest membership, status, and outcome."""
    total = Path(total_dir)

    finished = {r.lower() for r in _manifest_repositories(total / "finished-total.yaml")}
    failed = {r.lower() for r in _manifest_repositories(total / "failed-total.yaml")}

    execution = pd.read_csv(total / "target-execution-total.csv")
    execution["repository"] = execution["repository"].str.lower()

    execution["manifest"] = execution["repository"].map(
        lambda r: "failed" if r in failed else ("finished" if r in finished else "unlisted")
    )

    def _outcome(row: pd.Series) -> str:
        # A target in the failed manifest never reported back. It is a timeout
        # whatever partial status the execution report recorded.
        if row["manifest"] == "failed":
            return "timeout"
        if row["status"] == "Completed":
            return "completed"
        return "failed"

    execution["outcome"] = execution.apply(_outcome, axis=1)

    def _reason(row: pd.Series) -> str:
        if row["outcome"] == "completed":
            return ""
        if row["outcome"] == "timeout":
            return "timeout (failed manifest)"
        kind = row.get("failure_kind")
        stage = row.get("failure_stage")
        if isinstance(kind, str) and kind:
            return f"{stage or 'unknown'}: {kind}"
        return str(row["status"])

    execution["failure_reason"] = execution.apply(_reason, axis=1)

    # A target listed as failed but reported Completed, or the reverse, is a
    # bookkeeping conflict between the manifest and the execution report.
    execution["manifest_status_conflict"] = (
        ((execution["manifest"] == "failed") & (execution["status"] == "Completed"))
        | ((execution["manifest"] == "finished") & (execution["status"] == "Materialized"))
    )

    return execution


def load_validation(total_dir: str | Path) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Return deduplicated per-repository validation flags and the conflicts found.

    A repository can appear more than once when the pipeline ran it again. The
    surviving row is the most successful one — flags are OR-ed across duplicates,
    matching the question being asked ("did this repository ever produce coverage")
    rather than "did every attempt produce coverage". Rows whose duplicates
    disagree are returned separately so the choice is visible.
    """
    total = Path(total_dir)
    validation = pd.read_csv(total / "project-validation-total.csv")
    validation["repository"] = (
        validation["Owner"].str.lower() + "/" + validation["Repo"].str.lower()
    )

    for flag in VALIDATION_FLAGS:
        validation[flag] = validation[flag].astype(bool)

    duplicated = validation[validation.duplicated("repository", keep=False)]
    conflicts = (
        duplicated.groupby("repository")[list(VALIDATION_FLAGS)]
        .nunique()
        .pipe(lambda d: d[d.gt(1).any(axis=1)])
        .reset_index()
        if not duplicated.empty
        else pd.DataFrame()
    )

    aggregation = {flag: "max" for flag in VALIDATION_FLAGS}
    aggregation.update({
        "URL": "first",
        "Owner": "first",
        "Repo": "first",
        "CandidateCount": "max",
        "ExperimentEligibleCandidateCount": "max",
        "FailureCategory": "first",
        "FailureSummary": "first",
        # Environment columns: carried through because coverage collection is
        # environment-sensitive and these are the first thing to check when a
        # capability flag disagrees with what landed in the database.
        "DockerContext": "first",
        "DockerOs": "first",
        "ExecutionSupport": "first",
        "UnsupportedProjectCount": "max",
        "BaselineRunId": "first",
    })
    available = {k: v for k, v in aggregation.items() if k in validation.columns}
    deduped = validation.groupby("repository", as_index=False).agg(available)
    deduped["duplicate_rows"] = (
        validation.groupby("repository").size().reindex(deduped["repository"]).values
    )

    return deduped, conflicts


# ---------------------------------------------------------------------------
# Population
# ---------------------------------------------------------------------------

def build_population(
    total_dir: str | Path,
    data_dir: str | Path,
) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Join execution and validation into one row per target.

    Returns the population frame and the validation duplicate conflicts.
    """
    targets = load_targets(total_dir)
    validation, conflicts = load_validation(total_dir)

    population = targets.merge(validation, on="repository", how="left")

    for flag in VALIDATION_FLAGS:
        population[flag] = population[flag].fillna(False).astype(bool)

    population["has_validation_row"] = population["Owner"].notna()

    completed = population["outcome"] == "completed"
    population["eligible_core"] = completed & population[list(CORE_PREDICATES)].all(axis=1)
    population["eligible_mutation"] = completed & population["HasMutationScore"]
    population["selected"] = population["eligible_core"] | population["eligible_mutation"]

    def _exclusion(row: pd.Series) -> str:
        if row["selected"]:
            return ""
        if row["outcome"] != "completed":
            return f"not completed: {row['outcome']}"
        if not row["has_validation_row"]:
            return "no validation row"
        for predicate in CORE_PREDICATES:
            if not row[predicate]:
                return f"failed {predicate}"
        return "unknown"

    population["exclusion_reason"] = population.apply(_exclusion, axis=1)

    output_root = Path(data_dir) / "Output"
    population["output_dir"] = [
        str(output_root / repository.split("/")[0] / repository.split("/")[1] / str(commit))
        if isinstance(commit, str) and commit else ""
        for repository, commit in zip(population["repository"], population["resolved_commit"])
    ]
    population["has_db"] = [
        bool(path) and (Path(path) / "analysis.db").exists()
        for path in population["output_dir"]
    ]

    return population, conflicts


# ---------------------------------------------------------------------------
# Summaries
# ---------------------------------------------------------------------------

def summarize_outcomes(population: pd.DataFrame) -> pd.DataFrame:
    """Return target counts and shares by outcome."""
    total = len(population)
    summary = (
        population.groupby("outcome")
        .size()
        .rename("targets")
        .reset_index()
        .assign(share=lambda d: d["targets"] / total)
        .sort_values("targets", ascending=False)
        .reset_index(drop=True)
    )
    return summary


def summarize_failures(population: pd.DataFrame) -> pd.DataFrame:
    """Return why targets did not complete, ranked."""
    failures = population[population["outcome"] != "completed"]
    if failures.empty:
        return pd.DataFrame()
    total = len(population)
    return (
        failures.groupby(["outcome", "failure_reason"])
        .size()
        .rename("targets")
        .reset_index()
        .assign(
            share_of_all=lambda d: d["targets"] / total,
            share_of_failed=lambda d: d["targets"] / len(failures),
        )
        .sort_values("targets", ascending=False)
        .reset_index(drop=True)
    )


def summarize_flags(population: pd.DataFrame, scope: str = "completed") -> pd.DataFrame:
    """Return the share of repositories reaching each pipeline capability.

    ``scope`` sets the denominator: ``completed`` counts only targets whose
    pipeline finished (the only ones that could have produced these flags),
    ``all`` counts every target in the manifest.
    """
    frame = population if scope == "all" else population[population["outcome"] == "completed"]
    denominator = len(frame)
    rows = []
    for flag in VALIDATION_FLAGS:
        reached = int(frame[flag].sum())
        rows.append({
            "capability": flag,
            "repositories": reached,
            "denominator": denominator,
            "share": reached / denominator if denominator else float("nan"),
        })
    return pd.DataFrame(rows)


def summarize_selection(population: pd.DataFrame) -> pd.DataFrame:
    """Return the selected populations and what they are used for."""
    total = len(population)
    core = int(population["eligible_core"].sum())
    mutation = int(population["eligible_mutation"].sum())
    selected = int(population["selected"].sum())
    with_db = int(population.loc[population["selected"], "has_db"].sum())
    return pd.DataFrame([
        {"population": "eligible_core", "constructs": "metrics, smells, coverage, mappings",
         "repositories": core, "share_of_targets": core / total},
        {"population": "eligible_mutation", "constructs": "mutants",
         "repositories": mutation, "share_of_targets": mutation / total},
        {"population": "selected (union)", "constructs": "copied to the validation corpus",
         "repositories": selected, "share_of_targets": selected / total},
        {"population": "selected with analysis.db", "constructs": "actually readable",
         "repositories": with_db, "share_of_targets": with_db / total},
    ])


def summarize_exclusions(population: pd.DataFrame) -> pd.DataFrame:
    """Return why each excluded target was excluded."""
    excluded = population[~population["selected"]]
    if excluded.empty:
        return pd.DataFrame()
    total = len(population)
    return (
        excluded.groupby("exclusion_reason")
        .size()
        .rename("targets")
        .reset_index()
        .assign(share_of_all=lambda d: d["targets"] / total)
        .sort_values("targets", ascending=False)
        .reset_index(drop=True)
    )


# ---------------------------------------------------------------------------
# Corpus copy
# ---------------------------------------------------------------------------

# SQLite side files are recreated on open and must not be carried over.
_TRANSIENT_SUFFIXES = ("-wal", "-shm")


def copy_selected(
    population: pd.DataFrame,
    destination: str | Path,
    db_only: bool = False,
    dry_run: bool = False,
) -> pd.DataFrame:
    """Copy each selected repository's output folder into *destination*.

    The destination mirrors the source layout (``Output/<owner>/<repo>/<sha>/``)
    so the copied tree can be handed straight to ``build-msr-datasets``.
    """
    selected = population[population["selected"] & population["has_db"]]
    dest_root = Path(destination)
    rows = []

    for _, row in selected.iterrows():
        source = Path(row["output_dir"])
        owner, repo = row["repository"].split("/", 1)
        target = dest_root / "Output" / owner / repo / str(row["resolved_commit"])

        copied_bytes = 0
        if db_only:
            files = [source / "analysis.db"]
        else:
            files = [
                p for p in source.rglob("*")
                if p.is_file() and not p.name.endswith(_TRANSIENT_SUFFIXES)
            ]

        for path in files:
            if not path.exists():
                continue
            copied_bytes += path.stat().st_size
            if dry_run:
                continue
            relative = path.relative_to(source)
            out_path = target / relative
            out_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, out_path)

        rows.append({
            "repository": row["repository"],
            "commit": row["resolved_commit"],
            "source": str(source),
            "destination": str(target),
            "files": len(files),
            "bytes": copied_bytes,
            "eligible_core": bool(row["eligible_core"]),
            "eligible_mutation": bool(row["eligible_mutation"]),
        })

    return pd.DataFrame(rows)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def run(
    total_dir: str,
    data_dir: str,
    output_dir: str,
    copy_to: Optional[str] = None,
    db_only: bool = False,
    dry_run: bool = False,
) -> None:
    """Entry point for the ``msr-population`` CLI command."""
    from analysis.files import ensure_output_dir

    out = ensure_output_dir(output_dir)
    population, conflicts = build_population(total_dir, data_dir)

    outcomes = summarize_outcomes(population)
    failures = summarize_failures(population)
    flags = summarize_flags(population, scope="completed")
    flags_all = summarize_flags(population, scope="all")
    selection = summarize_selection(population)
    exclusions = summarize_exclusions(population)

    population.to_csv(out / "msr_population.csv", index=False)
    outcomes.to_csv(out / "msr_outcomes.csv", index=False)
    failures.to_csv(out / "msr_failure_reasons.csv", index=False)
    flags.to_csv(out / "msr_capabilities.csv", index=False)
    selection.to_csv(out / "msr_selection.csv", index=False)
    exclusions.to_csv(out / "msr_exclusions.csv", index=False)
    if not conflicts.empty:
        conflicts.to_csv(out / "msr_validation_conflicts.csv", index=False)

    total = len(population)
    completed = int((population["outcome"] == "completed").sum())
    failed = total - completed

    print(f"Targets: {total:,}")
    print(f"  completed {completed:,} ({completed / total:.1%})")
    print(f"  not completed {failed:,} ({failed / total:.1%})\n")

    print("Outcome")
    print(outcomes.to_string(index=False))

    if not failures.empty:
        print("\nWhy targets did not complete")
        print(failures.to_string(index=False))

    print("\nPipeline capabilities (denominator: completed targets)")
    print(flags.to_string(index=False))
    print("\nPipeline capabilities (denominator: all targets)")
    print(flags_all.to_string(index=False))

    print("\nSelected populations")
    print(selection.to_string(index=False))

    if not exclusions.empty:
        print("\nExclusions")
        print(exclusions.to_string(index=False))

    if not conflicts.empty:
        print(f"\n[note] {len(conflicts)} repositories had disagreeing duplicate validation rows "
              f"(see msr_validation_conflicts.csv); flags were OR-ed across duplicates")

    conflicted = int(population["manifest_status_conflict"].sum())
    if conflicted:
        print(f"[note] {conflicted} targets disagree between manifest and execution status")

    if copy_to:
        copied = copy_selected(population, copy_to, db_only=db_only, dry_run=dry_run)
        copied.to_csv(out / "msr_copied.csv", index=False)
        gigabytes = copied["bytes"].sum() / 1024 ** 3
        verb = "Would copy" if dry_run else "Copied"
        print(f"\n{verb} {len(copied):,} repositories "
              f"({copied['files'].sum():,} files, {gigabytes:,.2f} GB) to {copy_to}")

    print(f"\nReports written to {out}")
