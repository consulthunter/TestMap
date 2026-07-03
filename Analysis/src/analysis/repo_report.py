"""Generate a per-repository analysis report by executing NB01 via papermill.

The report is the full Notebook 1 executed for one repository, exported to
Markdown (with embedded plot images) via ``jupyter nbconvert``.

Usage::

    # Single repository
    python -m analysis repo-report \\
        --input Analysis/data/evaluation_attempts.csv \\
        --repo TestMap-Example \\
        --out Analysis/output/reports

    # All repositories (one report each + nothing combined)
    python -m analysis repo-report \\
        --input Analysis/data/evaluation_attempts.csv \\
        --out Analysis/output/reports
"""

from __future__ import annotations

import subprocess
from pathlib import Path
from typing import Optional

import pandas as pd

from analysis.files import ensure_output_dir


def _notebook_path(input_path: str, notebook: Optional[str]) -> Path:
    """Resolve NB01 path: explicit ``notebook`` arg, else infer from *input_path*."""
    if notebook:
        return Path(notebook).resolve()
    # Convention: input is  Analysis/data/evaluation_attempts.csv
    #             NB01 is   Analysis/notebooks/01_repository_evaluation.ipynb
    return Path(input_path).resolve().parent.parent / "notebooks" / "01_repository_evaluation.ipynb"


def run_single(
    nb_path: Path,
    repo_name: str,
    out: Path,
    keep_notebook: bool = False,
) -> Path:
    """Execute NB01 for *repo_name* and export to Markdown. Returns the .md path."""
    slug = repo_name.replace("/", "_").replace(" ", "_")
    executed_nb = out / f"{slug}_report.ipynb"
    report_stem = f"{slug}_report"
    report_md = out / f"{report_stem}.md"

    print(f"  [{repo_name}] executing notebook...")
    subprocess.run(
        [
            "uv", "run", "papermill",
            str(nb_path),
            str(executed_nb),
            "--cwd", str(nb_path.parent),
            "-p", "repo_name", repo_name,
        ],
        check=True,
    )

    print(f"  [{repo_name}] exporting to markdown...")
    subprocess.run(
        [
            "uv", "run", "jupyter", "nbconvert",
            str(executed_nb),
            "--to", "markdown",
            "--output", report_stem,
            "--output-dir", str(out),
        ],
        check=True,
    )

    if not keep_notebook and executed_nb.exists():
        executed_nb.unlink()

    print(f"  [{repo_name}] written: {report_md}")
    return report_md


def run(
    input_path: str,
    repo_name: Optional[str],
    output_dir: str,
    notebook: Optional[str] = None,
    keep_notebook: bool = False,
) -> None:
    """Entry point for the ``repo-report`` CLI command."""
    nb_path = _notebook_path(input_path, notebook)
    if not nb_path.exists():
        print(f"[error] Notebook not found: {nb_path}")
        print("Use --notebook <path> to specify the notebook explicitly.")
        return

    df = pd.read_csv(input_path, low_memory=False)
    out = ensure_output_dir(output_dir)

    if repo_name and repo_name != "all":
        available = (
            sorted(df["repo_name"].dropna().unique().tolist())
            if "repo_name" in df.columns
            else []
        )
        if repo_name not in available:
            print(f"[error] Repository '{repo_name}' not found. Available: {available}")
            return
        repos = [repo_name]
    else:
        repos = (
            sorted(df["repo_name"].dropna().unique().tolist())
            if "repo_name" in df.columns
            else []
        )

    if not repos:
        print("[error] No repositories found in the dataset.")
        return

    for rname in repos:
        run_single(nb_path, rname, out, keep_notebook=keep_notebook)

    print(f"\nDone. {len(repos)} report(s) written to {out}")
