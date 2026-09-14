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


@main.command("msr-population")
@click.option("--total", "total_dir", required=True,
              help="Directory holding finished/failed manifests and the total CSV reports.")
@click.option("--data", "data_dir", required=True,
              help="Run data directory containing Output/<owner>/<repo>/<sha>/.")
@click.option("--out", required=True, help="Output directory for population reports.")
@click.option("--copy-to", default=None,
              help="Copy the selected repositories' output folders here.")
@click.option("--db-only", is_flag=True, default=False,
              help="Copy only analysis.db rather than the whole output folder.")
@click.option("--dry-run", is_flag=True, default=False,
              help="Report what would be copied without writing anything.")
def msr_population(total_dir, data_dir, out, copy_to, db_only, dry_run) -> None:
    """Select the MSR validation population from execution and validation reports.

    Reports target outcomes (completed / timeout / failed with reason), pipeline
    capability shares, and the eligible populations; optionally copies the
    selected repositories into a validation corpus.
    """
    from analysis.msr_population import run
    run(total_dir=total_dir, data_dir=data_dir, output_dir=out,
        copy_to=copy_to, db_only=db_only, dry_run=dry_run)


@main.command("msr-validate")
@click.option("--frames", "frames_dir", required=True,
              help="Frames directory from build-msr-datasets.")
@click.option("--data", "data_dir", required=True,
              help="Corpus directory; source text is read from its analysis.db files.")
@click.option("--out", required=True, help="Output directory for the sample.")
@click.option("--seed", type=int, default=20260823, help="Sampling seed.")
@click.option("--n-per-sheet", type=int, default=50, help="Units drawn per sheet.")
@click.option("--rater", "raters", multiple=True, default=("rater_a", "rater_b"),
              help="Rater id. Repeat for more than two.")
@click.option("--sheet", "sheets", multiple=True, default=None,
              help="Limit to these sheets. Repeatable.")
def msr_validate(frames_dir, data_dir, out, seed, n_per_sheet, raters, sheets) -> None:
    """Draw the human-judged validation sample and write rater artifacts.

    Writes sample units with self-contained code excerpts, per-sheet codebooks,
    per-rater rating forms, and TestMap's own answers held separately.
    """
    from analysis.build_msr_validation import run
    run(frames_dir=frames_dir, data_dir=data_dir, output_dir=out, seed=seed,
        n_per_sheet=n_per_sheet, raters=tuple(raters),
        sheets=tuple(sheets) if sheets else None)


@main.command("msr-collect")
@click.option("--sample", "sample_dir", required=True,
              help="Sample directory produced by msr-validate.")
@click.option("--out", required=True, help="Output directory for agreement results.")
def msr_collect(sample_dir, out) -> None:
    """Collect filled rating forms and compute inter-rater agreement.

    Reports form completion, Cohen's kappa (or per-label kappa and Krippendorff's
    alpha for multi-label sheets), and agreement with TestMap's own output.
    """
    from analysis.msr_agreement import run
    run(sample_dir=sample_dir, output_dir=out)


@main.command("msr-coverage-audit")
@click.option("--population", "population_csv", required=True,
              help="Path to msr_population.csv from msr-population.")
@click.option("--data", "data_dir", required=True,
              help="Corpus directory containing Output/<owner>/<repo>/<sha>/.")
@click.option("--logs", "logs_root", required=True,
              help="Run root holding the dated log directories.")
@click.option("--out", required=True, help="Output directory for the audit.")
def msr_coverage_audit(population_csv, data_dir, logs_root, out) -> None:
    """Audit repositories claiming coverage that persisted no coverage rows.

    Classifies each failure from the run logs and writes an exclusion list plus a
    findings note.
    """
    from analysis.msr_coverage_audit import run
    run(population_csv=population_csv, data_dir=data_dir,
        logs_root=logs_root, output_dir=out)


@main.command("build-msr-datasets")
@click.option("--db", "db_paths", multiple=True, required=True,
              help="Glob patterns for per-repository analysis.db paths.")
@click.option("--out", required=True,
              help="Output directory for MSR validation datasets.")
@click.option("--limit", type=int, default=None,
              help="Read at most N databases. Useful for a first pass.")
@click.option("--exclude", "exclude_csv", default=None,
              help="CSV with a repo_key column; those repositories are skipped.")
def build_msr_datasets(db_paths, out, limit, exclude_csv) -> None:
    """Consolidate per-repository analysis.db files into MSR validation frames.

    Outputs into <out>/frames/: msr_repositories.csv, msr_entities.csv,
    msr_code_metrics.csv, msr_test_smells.csv, msr_coverage.csv,
    msr_mutants.csv, msr_mappings.csv, msr_structural_checks.csv.
    """
    from analysis.build_msr_datasets import run
    run(db_paths=db_paths, output_dir=out, limit=limit, exclude_csv=exclude_csv)


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
    passed = run(results=results, db_paths=db, artifacts_root=artifacts, output_dir=out)
    if not passed:
        raise click.ClickException("Evaluation audit found blocking errors.")


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
              help="Path to NB01. Defaults to <input>/../notebooks/exploratory/01_repository_evaluation.ipynb.")
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
        "top-n", "high-severity", "first-attempt", "lane-llm", "lane-agentic",
    ]),
    default="all",
    help="Sampling strategy.",
)
@click.option("--n", default=0,
              help="Top-N labels for top-n; cases per stratum for stratified-* (0 = all).")
@click.option("--seed", type=int, default=20260914, help="Seed for stratified sampling.")
@click.option("--include-infrastructure", is_flag=True, default=False,
              help="Keep infrastructure failures (credentials, provider errors) as cases.")
@click.option("--logs", "logs_root", default=None,
              help="Folder holding the run's dated log folders; re-roots recorded log paths.")
@click.option("--markdown", is_flag=True, default=False,
              help="Write individual Markdown case files.")
def export_failures(results, db, artifacts, out, sample, n, seed, include_infrastructure,
                    logs_root, markdown) -> None:
    """Export a qualitative failure dataset for later open coding.

    Outputs: failure_cases.csv, failure_cases.jsonl,
    cases/case_*/case.md (when --markdown is set).
    """
    from analysis.export_failure_cases import run
    run(
        results=results, db_paths=db, artifacts_root=artifacts,
        output_dir=out, sample=sample, top_n=n, write_markdown=markdown,
        include_infrastructure=include_infrastructure, seed=seed, logs_root=logs_root,
    )
