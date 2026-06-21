"""CLI entry point for the analysis package.

Usage::

    python -m analysis build-datasets --results "Output/**/*.csv" --out Analysis/output
    python -m analysis overview --input Analysis/output/evaluation_attempts.csv --out Analysis/output
    python -m analysis audit --results "Output/**/*.csv" --out Analysis/output/audit
    python -m analysis export-training --db Output/testmap.db --out Analysis/output/training
    python -m analysis export-failures --results "Output/**/*.csv" --out Analysis/output/failures
"""

import click


@click.group()
def main() -> None:
    """TestMap post-run analysis tools."""


@main.command("build-datasets")
@click.option("--results", multiple=True, required=True,
              help="Glob patterns for experiment result CSV files.")
@click.option("--db", multiple=True,
              help="Glob patterns for SQLite database paths.")
@click.option("--artifacts", default=None,
              help="Root directory of artifact output folders.")
@click.option("--out", required=True,
              help="Output directory for analysis datasets.")
def build_datasets(results, db, artifacts, out) -> None:
    """Build normalized evaluation datasets.

    Outputs: evaluation_attempts.csv, evaluation_candidates.csv,
    evaluation_repositories.csv, generated_tests.csv,
    evaluation_overview.json, evaluation_overview.csv.
    """
    from analysis.build_evaluation_dataset import run
    run(results=results, db_paths=db, artifacts_root=artifacts, output_dir=out)


@main.command("overview")
@click.option("--input", "input_path", required=True,
              help="Path to evaluation_attempts.csv.")
@click.option("--out", required=True, help="Output directory.")
def overview(input_path, out) -> None:
    """Build headline evaluation counts for the cross-repository overview notebook.

    Outputs: evaluation_overview.json, evaluation_overview.csv,
    evaluation_overview.md.
    """
    from analysis.summaries import run_overview
    run_overview(input_path=input_path, output_dir=out)


@main.command("audit")
@click.option("--results", multiple=True, required=True,
              help="Glob patterns for experiment result CSV files.")
@click.option("--db", multiple=True,
              help="Glob patterns for SQLite database paths.")
@click.option("--artifacts", default=None,
              help="Root directory of artifact output folders.")
@click.option("--out", required=True,
              help="Output directory for audit reports.")
def audit(results, db, artifacts, out) -> None:
    """Check evaluation data completeness and consistency.

    Outputs: audit_report.json, audit_report.csv, audit_report.md.
    """
    from analysis.audit_evaluation_data import run
    run(results=results, db_paths=db, artifacts_root=artifacts, output_dir=out)


@main.command("export-training")
@click.option("--db", required=True,
              help="Path to the TestMap SQLite database.")
@click.option("--out", required=True,
              help="Output directory for training datasets.")
@click.option(
    "--grain",
    type=click.Choice(["mapping", "candidate", "pair"]),
    default="mapping",
    help="Row grain for the export.",
)
def export_training(db, out, grain) -> None:
    """Export ML/training-ready source-test mapping datasets.

    Outputs depend on --grain:
      mapping   -> training_mappings.csv (default)
      candidate -> training_candidates.csv
      pair      -> training_pairs.csv
    """
    from analysis.export_training_dataset import run
    run(db_path=db, output_dir=out, grain=grain)


@main.command("repo-report")
@click.option("--input", "input_path", required=True,
              help="Path to evaluation_attempts.csv.")
@click.option("--repo", "repo_name", default=None,
              help="Repository name. Omit (or pass 'all') to run all repos.")
@click.option("--out", required=True,
              help="Output directory for Markdown reports and plot images.")
@click.option("--notebook", default=None,
              help="Path to NB01. Defaults to <input>/../notebooks/01_repository_evaluation.ipynb.")
@click.option("--keep-notebook", is_flag=True, default=False,
              help="Keep the executed .ipynb alongside the Markdown output.")
def repo_report(input_path, repo_name, out, notebook, keep_notebook) -> None:
    """Execute NB01 per repository via papermill and export to Markdown.

    Produces one <repo>_report.md (+ <repo>_report_files/ with plots) per repo.
    Pass --repo <name> for a single repository, or omit to run all repos.
    """
    from analysis.repo_report import run
    run(
        input_path=input_path,
        repo_name=repo_name,
        output_dir=out,
        notebook=notebook,
        keep_notebook=keep_notebook,
    )


@main.command("export-failures")
@click.option("--results", multiple=True, required=True,
              help="Glob patterns for experiment result CSV files.")
@click.option("--db", multiple=True,
              help="Glob patterns for SQLite database paths.")
@click.option("--artifacts", default=None,
              help="Root directory of artifact output folders.")
@click.option("--out", required=True,
              help="Output directory for failure case exports.")
@click.option(
    "--sample",
    type=click.Choice([
        "all", "stratified-lane", "stratified-label", "stratified-tool",
        "top-n", "high-severity", "llm-won", "agentic-won",
    ]),
    default="all",
    help="Sampling strategy.",
)
@click.option("--n", default=0, help="Top-N count when --sample=top-n.")
@click.option("--markdown", is_flag=True, default=False,
              help="Write individual Markdown case files.")
def export_failures(results, db, artifacts, out, sample, n, markdown) -> None:
    """Export a qualitative failure dataset for later open coding.

    Outputs: failure_cases.csv, failure_cases.jsonl,
    cases/case_*/case.md (when --markdown is set).
    """
    from analysis.export_failure_cases import run
    run(
        results=results, db_paths=db, artifacts_root=artifacts,
        output_dir=out, sample=sample, top_n=n, write_markdown=markdown,
    )
