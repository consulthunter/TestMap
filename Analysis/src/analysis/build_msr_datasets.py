"""Build the consolidated MSR validation datasets.

Reads every per-repository ``analysis.db`` matching the given patterns and writes
one CSV per mined construct, plus per-repository summaries and structural check
results.

Outputs::

    msr_repositories.csv       one row per repository revision (counts, availability)
    msr_entities.csv           attribution spine: one row per member and object
    msr_code_metrics.csv       one row per persisted metric row
    msr_test_smells.csv        one row per detected smell
    msr_coverage.csv           one row per raw member/object coverage observation,
                               retaining nullable attribution and corrected provenance
    msr_mutants.csv            one row per (member, report) mutant aggregate
    msr_mappings.csv           one row per source-to-test mapping
    msr_structural_checks.csv  long format: repo x construct x check

Frames are appended to disk per repository rather than concatenated in memory:
the corpus runs to a thousand databases and several million entity rows.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import ensure_output_dir
from analysis.msr_checks import run_checks, summarize_repository
from analysis.msr_frames import iter_repositories


# Frame name -> output file. ``mutant_locations`` is intentionally absent: it is
# one row per mutant (millions across the corpus) and is only used for checks.
FRAME_OUTPUTS = {
    "files": "msr_files.csv",
    "entities": "msr_entities.csv",
    "code_metrics": "msr_code_metrics.csv",
    "test_smells": "msr_test_smells.csv",
    "coverage": "msr_coverage.csv",
    "mutants": "msr_mutants.csv",
    "mutant_operators": "msr_mutant_operators.csv",
    "mappings": "msr_mappings.csv",
    "test_results": "msr_test_results.csv",
}


class _CsvAppender:
    """Append DataFrames to a CSV, writing the header once."""

    def __init__(self, path: Path):
        self.path = path
        self._started = False
        self._rows = 0
        self._columns: list[str] | None = None

    def discard_stale(self) -> None:
        """Remove a leftover file when this run produced no rows for it.

        Deletion is deferred to the end of the run rather than done up front: an
        up-front unlink destroys the previous run's outputs even when the new run
        fails partway, which is exactly when the old data is still wanted.
        """
        if self._started or not self.path.exists():
            return
        try:
            self.path.unlink()
        except OSError as exc:
            print(f"[warn] Could not remove stale {self.path}: {exc}")

    def append(self, frame: pd.DataFrame) -> None:
        if frame is None or frame.empty:
            return
        if self._columns is None:
            self._columns = list(frame.columns)
        elif set(frame.columns) != set(self._columns):
            raise ValueError(f"Inconsistent columns for {self.path}: "
                             f"expected {self._columns}, got {list(frame.columns)}")
        frame = frame.reindex(columns=self._columns)
        frame.to_csv(
            self.path,
            mode="a" if self._started else "w",
            header=not self._started,
            index=False,
            encoding="utf-8",
        )
        self._started = True
        self._rows += len(frame)

    @property
    def rows(self) -> int:
        return self._rows


def _load_exclusions(exclude_csv: Optional[str]) -> set[str]:
    """Return the repo_keys to skip, read from an exclusion CSV."""
    if not exclude_csv:
        return set()
    frame = pd.read_csv(exclude_csv)
    if "repo_key" not in frame.columns:
        raise ValueError(f"{exclude_csv} has no repo_key column")
    return {str(key).lower() for key in frame["repo_key"].dropna()}


def run(
    db_paths: list[str] | tuple[str, ...],
    output_dir: str,
    limit: Optional[int] = None,
    exclude_csv: Optional[str] = None,
) -> None:
    """Entry point for the ``build-msr-datasets`` CLI command."""
    out = ensure_output_dir(output_dir)
    excluded_keys = _load_exclusions(exclude_csv)
    if excluded_keys:
        print(f"Excluding {len(excluded_keys):,} repositories from {exclude_csv}")
    frames_dir = ensure_output_dir(str(Path(out) / "frames"))

    appenders = {
        name: _CsvAppender(Path(frames_dir) / filename)
        for name, filename in FRAME_OUTPUTS.items()
    }
    checks_appender = _CsvAppender(Path(frames_dir) / "msr_structural_checks.csv")

    summaries: list[dict] = []
    repositories = 0
    skipped = 0

    for bundle in iter_repositories(db_paths, limit=limit):
        if bundle["identity"]["repo_key"].lower() in excluded_keys:
            skipped += 1
            continue
        repositories += 1
        for name, appender in appenders.items():
            appender.append(bundle.get(name))
        checks_appender.append(run_checks(bundle))
        summaries.append(summarize_repository(bundle))

    if not summaries:
        print("No databases matched. Nothing written.")
        return

    for appender in (*appenders.values(), checks_appender):
        appender.discard_stale()

    repo_frame = pd.DataFrame(summaries)
    repo_path = Path(frames_dir) / "msr_repositories.csv"
    repo_frame.to_csv(repo_path, index=False, encoding="utf-8")

    print(f"\nMSR datasets written to {frames_dir}")
    print(f"  msr_repositories.csv        {len(repo_frame):>10,} rows")
    for name, filename in FRAME_OUTPUTS.items():
        print(f"  {filename:<28}{appenders[name].rows:>10,} rows")
    print(f"  msr_structural_checks.csv   {checks_appender.rows:>10,} rows")

    available = repo_frame[[c for c in repo_frame.columns if c.startswith("has_")]].sum()
    print(f"\nData availability across {repositories:,} repositories:")
    for column, count in available.items():
        construct = column.removeprefix("has_")
        print(f"  {construct:<12}{int(count):>6,} repos ({count / repositories:.1%})")
