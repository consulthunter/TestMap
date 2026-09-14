"""Build normalized evaluation datasets from result CSVs and optional SQLite databases.

Outputs written to --out:
  evaluation_attempts.csv       one row per attempt
  evaluation_candidates.csv     best attempt per candidate per lane
  evaluation_repositories.csv   one row per repository/lane slice
  generated_tests.csv           one row per generated/linked test
  evaluation_overview.json      headline counts
  evaluation_overview.csv       flattened overview
"""

from __future__ import annotations

import json
import re
from pathlib import Path

import pandas as pd

from analysis.files import (
    ensure_output_dir,
    read_result_grains,
    read_schema4_result_bundle,
)
from analysis.normalize import (
    build_traced_assertions_dataset,
    build_candidate_summary,
    build_generated_tests_dataset,
    build_repository_summary,
    build_repository_family_summary,
    normalize_assertion_observations,
    normalize_attempts,
)
from analysis.schema import (
    ASSERTION_RESULTS_SCHEMA_VERSION,
    LEGACY_RESULTS_SCHEMA_VERSION,
)
from analysis.summaries import build_overview


def load_raw_attempts(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
) -> pd.DataFrame:
    """Load and concatenate attempt rows from result CSVs.

    SQLite join queries are deferred to a future pass when the column
    mapping between CSVs and the DB schema is finalized.
    """
    df, _, _ = read_result_grains(list(results))
    if df.empty:
        print("[warn] No result CSVs found.")
    return df


def build_attempts_dataset(
    raw_df: pd.DataFrame,
    generated_test_df: pd.DataFrame | None = None,
) -> pd.DataFrame:
    """Normalize raw attempt rows into the shared attempt-level schema."""
    if raw_df.empty:
        return pd.DataFrame()
    attempts = normalize_attempts(raw_df)
    if generated_test_df is None or generated_test_df.empty:
        attempts["generated_test_count"] = 0
        return attempts

    if "attempt_id" not in generated_test_df.columns:
        raise ValueError("Generated-test rows require attempt_id for parent aggregation.")
    counts = generated_test_df.groupby("attempt_id").size().rename("generated_test_count")
    attempts = attempts.drop(columns=["generated_test_count"], errors="ignore")
    return attempts.merge(counts, on="attempt_id", how="left").assign(
        generated_test_count=lambda x: x["generated_test_count"].fillna(0).astype("Int64")
    )


def build_generated_tests_dataset_from_raw(
    raw_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Build a per-generated-test dataset.

    The primary source is the canonical generated-test CSV. SQLite databases are an optional
    secondary source for additional metadata; they are queried when provided.

    Parameters
    ----------
    raw_df:
        The raw ``row_kind='generated_test'`` DataFrame.
    db_paths:
        Optional glob patterns for SQLite databases.
    """
    return build_generated_tests_dataset(raw_df)


def build_tool_generated_test_links(
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Build optional raw DB link rows for tool attempts and generated tests."""
    if not db_paths:
        return pd.DataFrame()

    from analysis.db import connect, get_members, get_tool_attempt_generated_tests
    from analysis.files import find_databases

    frames: list[pd.DataFrame] = []
    for db_path in find_databases(list(db_paths)):
        with connect(db_path) as conn:
            links = get_tool_attempt_generated_tests(conn)
            if links.empty:
                continue
            members = get_members(conn)
            merged = links.merge(
                members,
                left_on="member_id",
                right_on="id",
                how="left",
                suffixes=("", "_member"),
            )
            merged["_source_db"] = str(db_path)
            frames.append(merged)

    return pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()


# Heuristic assertion detector for common C# test frameworks (MSTest/NUnit/xUnit
# assertions + FluentAssertions ``.Should(``).
_ASSERT_RE = re.compile(
    r"Assert\s*\.|\bStringAssert\s*\.|\bCollectionAssert\s*\.|\bClassicAssert\s*\.|\.Should\s*\(",
    re.IGNORECASE,
)


def _count_assertions(code: object) -> int:
    if not isinstance(code, str) or not code:
        return 0
    return len(_ASSERT_RE.findall(code))


def build_assertion_counts(
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Legacy per-attempt assertion inventory (never classified lineage).

    A generated test is persisted as a member and parsed into the ``invocations``
    table (with ``is_assertion``) only when it *validates*. The agentic lane links
    tests to members via ``tool_attempt_generated_tests``, so it uses **real Roslyn
    assertion detection** from invocations. The LLM lane has no member FK on
    ``generated_test_executions`` and its generated test names collide with agentic
    members, so it falls back to a heuristic regex over the stored
    ``generated_test_code``. ``assertion_source`` records which method was used.

    Results from this function cannot populate Traced, Trivial, or Unresolved.
    Returns one row per ``attempt_id`` with ``assertion_count``, ``invocation_count``
    (NA for the regex lane), ``generated_test_n``, and ``assertion_source``.
    """
    if not db_paths:
        return pd.DataFrame()

    from analysis.db import (
        connect,
        get_canonical_attempt_identities,
        get_generated_test_executions,
        get_invocations,
        get_tool_attempt_generated_tests,
    )
    from analysis.files import find_databases

    rows: list[dict] = []
    for db_path in find_databases(list(db_paths)):
        with connect(db_path) as conn:
            execs = get_generated_test_executions(conn)
            links = get_tool_attempt_generated_tests(conn)
            invocations = get_invocations(conn)
            identities = get_canonical_attempt_identities(conn)

        llm_keys = {
            int(row.local_attempt_id): row.attempt_id
            for row in identities[identities["lane"] == "llm"].itertuples()
        }
        agentic_keys = {
            int(row.local_attempt_id): row.attempt_id
            for row in identities[identities["lane"] == "agentic"].itertuples()
        }

        # Per-member assertion/invocation counts (real Roslyn detection).
        per_member = pd.DataFrame()
        if not invocations.empty and "member_id" in invocations.columns:
            inv = invocations.copy()
            inv["is_assertion"] = pd.to_numeric(inv["is_assertion"], errors="coerce").fillna(0)
            per_member = inv.groupby("member_id").agg(
                assertions=("is_assertion", "sum"), invs=("is_assertion", "size"))

        # LLM: prefer real invocations via member_id (populated post-migration + re-run);
        # otherwise fall back to a regex over the stored generated test code.
        if not execs.empty and "generation_attempt_id" in execs.columns:
            has_member = "member_id" in execs.columns
            for gaid, grp in execs.groupby("generation_attempt_id"):
                aid = llm_keys.get(int(gaid))
                if aid is None:
                    continue
                mids = ([int(m) for m in grp["member_id"].dropna()]
                        if has_member and not per_member.empty else [])
                mids = [m for m in mids if m in per_member.index]
                if mids:
                    rows.append({"attempt_id": aid,
                                 "assertion_count": int(per_member.loc[mids, "assertions"].sum()),
                                 "invocation_count": int(per_member.loc[mids, "invs"].sum()),
                                 "generated_test_n": len(grp),
                                 "assertion_source": "invocations"})
                else:
                    rows.append({"attempt_id": aid,
                                 "assertion_count": int(grp.get("generated_test_code", pd.Series(dtype=str))
                                                        .map(_count_assertions).sum()),
                                 "invocation_count": pd.NA,
                                 "generated_test_n": len(grp),
                                 "assertion_source": "code_regex"})

        # Agentic: real assertion detection from invocations on the test members.
        if (not links.empty and "tool_attempt_id" in links.columns and not per_member.empty):
            links = links.drop_duplicates(["tool_attempt_id", "member_id"])
            for taid, grp in links.groupby("tool_attempt_id"):
                aid = agentic_keys.get(int(taid))
                if aid is None:
                    continue
                mids = [m for m in grp["member_id"] if m in per_member.index]
                asserts = int(per_member.loc[mids, "assertions"].sum()) if mids else 0
                ninv = int(per_member.loc[mids, "invs"].sum()) if mids else 0
                rows.append({"attempt_id": aid,
                             "assertion_count": asserts,
                             "invocation_count": ninv,
                             "generated_test_n": len(grp),
                             "assertion_source": "invocations"})

    if not rows:
        return pd.DataFrame()
    # Dedup is now a safety no-op for unique keys (LLM keys are repo-qualified, agentic keys
    # use the unique artifact path); kept to guard against any residual duplicates.
    return (pd.DataFrame(rows)
            .groupby("attempt_id", as_index=False)
            .agg(assertion_count=("assertion_count", "sum"),
                 invocation_count=("invocation_count", "sum"),
                 generated_test_n=("generated_test_n", "sum"),
                 assertion_source=("assertion_source", "first")))


def _baseline_mutants(mutants: pd.DataFrame, reports: pd.DataFrame) -> pd.DataFrame:
    """Restrict mutants to the repository's baseline mutation report.

    Every attempt writes its own mutation report into the same database, and the
    tool lane also records per-attempt targeted baselines (``SourceProject`` scope,
    ``is_baseline`` = 1). The repository baseline is the solution-scope baseline
    report. Databases that predate those columns hold one report and pass through; a
    database that has them but no solution baseline has no baseline profile at all,
    and pooling its attempt reports in place of one would misstate it.
    """
    if (reports.empty or not {"is_baseline", "scope_kind"} <= set(reports.columns)
            or "mutation_testing_report_id" not in mutants.columns):
        return mutants
    baseline = reports[(reports["is_baseline"] == 1) & (reports["scope_kind"] == "Solution")]
    return mutants[mutants["mutation_testing_report_id"].isin(baseline["id"])]


def build_mutation_operators(
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Mutation-operator survival profile per repository (RQ6 depth).

    Aggregates ``mutants`` by ``mutator_name`` x ``status``. ``survival_rate`` =
    Survived / (Survived + Killed) over covered, non-timeout mutants — higher
    means the test suite misses that operator class more often. This is the
    baseline survival profile; before/after "newly killed" attribution is
    deferred (it requires pairing mutation reports across attempts).
    """
    if not db_paths:
        return pd.DataFrame()

    from analysis.db import connect, get_mutants, get_mutation_reports, get_projects
    from analysis.files import find_databases

    frames: list[pd.DataFrame] = []
    for db_path in find_databases(list(db_paths)):
        with connect(db_path) as conn:
            mut = get_mutants(conn)
            reports = get_mutation_reports(conn)
            projects = get_projects(conn)
        if mut.empty or "mutator_name" not in mut.columns or "status" not in mut.columns:
            continue
        mut = _baseline_mutants(mut, reports)
        # The output layout is <owner>/<repo>/<commit>/analysis.db, so the parent
        # folder is the commit; the database's own project row names the repository.
        repo_name = (projects["repo_name"].iloc[0] if not projects.empty
                     else Path(db_path).parent.name)
        counts = (mut.groupby(["mutator_name", "status"]).size()
                  .unstack(fill_value=0).reset_index())
        counts.insert(0, "repo_name", repo_name)
        frames.append(counts)

    if not frames:
        return pd.DataFrame()
    df = pd.concat(frames, ignore_index=True).fillna(0)
    killed = df["Killed"] if "Killed" in df.columns else 0
    survived = df["Survived"] if "Survived" in df.columns else 0
    total = killed + survived
    if hasattr(total, "where"):
        # survival_rate = Survived / (Survived + Killed); NaN where no killable mutants.
        df["survival_rate"] = (survived / total.where(total > 0))
    else:
        df["survival_rate"] = float("nan")
    return df


def build_metric_diffs(
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Per-attempt before/after metric diffs: coverage lines closed + mutants newly killed.

    Computes set differences against the targeted source member:
    - ``lines_closed``        = baseline coverage gaps minus post-attempt gaps
    - ``mutants_newly_killed``= mutants that survived the baseline but not the post-attempt run

    Tool lane uses ``tool_attempts.targeted_baseline_id`` (before) +
    ``post_attempt_test_run_id`` (after) — available now. LLM lane uses
    ``generated_test_executions.baseline_test_run_id`` (before) + ``test_run_id`` (after);
    it activates automatically once those columns are populated (migration
    ``AddGeneratedTestExecutionMemberAndBaseline`` + an experiment re-run).
    """
    if not db_paths:
        return pd.DataFrame()

    from analysis.db import (
        connect,
        get_canonical_attempt_identities,
        get_candidate_methods,
        get_coverage_gaps,
        get_coverage_reports,
        get_generated_test_executions,
        get_generation_attempts,
        get_mutants,
        get_mutation_reports,
        get_tool_attempts,
    )
    from analysis.files import find_databases

    rows: list[dict] = []
    for db_path in find_databases(list(db_paths)):
        with connect(db_path) as conn:
            cov_reports = get_coverage_reports(conn)
            gaps = get_coverage_gaps(conn)
            mut_reports = get_mutation_reports(conn)
            mutants = get_mutants(conn)
            tool_attempts = get_tool_attempts(conn)
            cand_methods = get_candidate_methods(conn)
            execs = get_generated_test_executions(conn)
            gen_attempts = get_generation_attempts(conn)
            identities = get_canonical_attempt_identities(conn)

        llm_keys = {
            int(row.local_attempt_id): row.attempt_id
            for row in identities[identities["lane"] == "llm"].itertuples()
        }
        agentic_keys = {
            int(row.local_attempt_id): row.attempt_id
            for row in identities[identities["lane"] == "agentic"].itertuples()
        }

        # Gap-line sets keyed by (test_run_id, source member_id).
        gap_set: dict = {}
        if not gaps.empty and not cov_reports.empty:
            g = gaps.merge(
                cov_reports[["id", "test_run_id"]].rename(columns={"id": "coverage_report_id"}),
                on="coverage_report_id", how="inner")
            for (tr, mid), grp in g.dropna(subset=["test_run_id", "member_id"]).groupby(["test_run_id", "member_id"]):
                gap_set[(int(tr), int(mid))] = set(grp["line_number"])

        # Survived-mutant sets keyed by (test_run_id, source member_id).
        surv_set: dict = {}
        if not mutants.empty and not mut_reports.empty and "status" in mutants.columns:
            s = mutants[mutants["status"] == "Survived"].merge(
                mut_reports[["id", "test_run_id"]].rename(columns={"id": "mutation_testing_report_id"}),
                on="mutation_testing_report_id", how="inner")
            key_col = "stryker_mutant_id" if "stryker_mutant_id" in s.columns else "id"
            for (tr, mid), grp in s.dropna(subset=["test_run_id", "member_id"]).groupby(["test_run_id", "member_id"]):
                surv_set[(int(tr), int(mid))] = set(grp[key_col])

        def diff(before_run, after_run, member_id):
            if pd.isna(before_run) or pd.isna(after_run) or pd.isna(member_id):
                return None
            b, a, m = int(before_run), int(after_run), int(member_id)
            lines_closed = len(gap_set.get((b, m), set()) - gap_set.get((a, m), set()))
            mutants_killed = len(surv_set.get((b, m), set()) - surv_set.get((a, m), set()))
            return lines_closed, mutants_killed

        cm_member = (cand_methods.set_index("id")["source_member_id"]
                     if not cand_methods.empty else pd.Series(dtype="float64"))

        # Tool lane.
        if not tool_attempts.empty:
            for ta in tool_attempts.itertuples():
                member = cm_member.get(getattr(ta, "candidate_method_id", None))
                d = diff(getattr(ta, "targeted_baseline_id", None),
                         getattr(ta, "post_attempt_test_run_id", None), member)
                if d is None:
                    continue
                aid = agentic_keys.get(int(ta.id))
                if aid is None:
                    continue
                rows.append({"attempt_id": aid, "lane": "agentic",
                             "lines_closed": d[0], "mutants_newly_killed": d[1]})

        # LLM lane (auto-activates once baseline_test_run_id/test_run_id are populated).
        if (not execs.empty
                and {"baseline_test_run_id", "test_run_id", "generation_attempt_id"} <= set(execs.columns)
                and not gen_attempts.empty and "candidate_method_id" in gen_attempts.columns):
            ga_cm = gen_attempts.set_index("id")["candidate_method_id"]
            for ex in execs.itertuples():
                cmid = ga_cm.get(getattr(ex, "generation_attempt_id", None))
                member = cm_member.get(cmid) if cmid is not None else None
                d = diff(getattr(ex, "baseline_test_run_id", None),
                         getattr(ex, "test_run_id", None), member)
                if d is None:
                    continue
                gaid = int(ex.generation_attempt_id)
                aid = llm_keys.get(gaid)
                if aid is None:
                    continue
                rows.append({"attempt_id": aid, "lane": "llm",
                             "lines_closed": d[0], "mutants_newly_killed": d[1]})

    if not rows:
        return pd.DataFrame()
    return (pd.DataFrame(rows)
            .groupby("attempt_id", as_index=False)
            .agg(lane=("lane", "first"),
                 lines_closed=("lines_closed", "max"),
                 mutants_newly_killed=("mutants_newly_killed", "max")))


def save_datasets(
    attempts: pd.DataFrame,
    candidates: pd.DataFrame,
    repositories: pd.DataFrame,
    generated_tests: pd.DataFrame,
    overview: dict,
    output_dir: Path,
    tool_generated_test_links: pd.DataFrame | None = None,
    mutation_operators: pd.DataFrame | None = None,
    assertion_observations: pd.DataFrame | None = None,
    traced_assertions: pd.DataFrame | None = None,
) -> None:
    """Write all datasets to *output_dir*."""
    attempts.to_csv(output_dir / "evaluation_attempts.csv", index=False)
    candidates.to_csv(output_dir / "evaluation_candidates.csv", index=False)
    repositories.to_csv(output_dir / "evaluation_repositories.csv", index=False)
    if not generated_tests.empty:
        generated_tests.to_csv(output_dir / "generated_tests.csv", index=False)
    if tool_generated_test_links is not None and not tool_generated_test_links.empty:
        tool_generated_test_links.to_csv(output_dir / "tool_generated_test_links.csv", index=False)
    if mutation_operators is not None and not mutation_operators.empty:
        mutation_operators.to_csv(output_dir / "mutation_operators.csv", index=False)
    if assertion_observations is not None:
        assertion_observations.to_csv(
            output_dir / "assertion_observations.csv", index=False
        )
    if traced_assertions is not None:
        traced_assertions.to_csv(output_dir / "traced_assertions.csv", index=False)

    with open(output_dir / "evaluation_overview.json", "w", encoding="utf-8") as f:
        json.dump(overview, f, indent=2, default=str)

    rows = [
        {"section": section, "metric": k, "value": v}
        for section, metrics in overview.items()
        for k, v in metrics.items()
    ]
    pd.DataFrame(rows).to_csv(output_dir / "evaluation_overview.csv", index=False)


def run(
    results: list[str] | tuple[str, ...],
    db_paths: list[str] | tuple[str, ...],
    artifacts_root: str | None,
    output_dir: str,
    results_schema_version: str = ASSERTION_RESULTS_SCHEMA_VERSION,
) -> None:
    """Build datasets for schema 4, or the explicit legacy schema-3 path."""
    out = ensure_output_dir(output_dir)

    print("Loading raw attempts...")
    if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION:
        raw, raw_generated_tests, _, raw_assertions = read_schema4_result_bundle(
            list(results), require_assertion_sidecar=True
        )
    elif results_schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        raw, raw_generated_tests, _ = read_result_grains(
            list(results),
            expected_schema_version=LEGACY_RESULTS_SCHEMA_VERSION,
        )
        raw_assertions = pd.DataFrame()
    else:
        raise ValueError(
            f"Unsupported results_schema_version: {results_schema_version}."
        )
    print(f"  {len(raw)} raw rows loaded.")

    # Normalize child rows separately so attempt counts cannot be inflated.
    generated_tests = build_generated_tests_dataset_from_raw(raw_generated_tests, db_paths)
    tool_generated_test_links = build_tool_generated_test_links(db_paths)

    attempts = build_attempts_dataset(raw, raw_generated_tests)

    assertions = normalize_assertion_observations(raw_assertions)
    traced_assertions = build_traced_assertions_dataset(assertions)

    # Schema 3 retains its explicitly labelled inventory-only fallback. Schema 4
    # uses persisted/exported semantic evidence and never derives categories from
    # regexes or invocation counts.
    assertion_counts = pd.DataFrame()
    if results_schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        assertion_counts = build_assertion_counts(db_paths)
        if not assertion_counts.empty and "attempt_id" in attempts.columns:
            attempts = attempts.merge(assertion_counts, on="attempt_id", how="left")

    # RQ6 before/after diffs: coverage lines closed + mutants newly killed.
    metric_diffs = build_metric_diffs(db_paths)
    if not metric_diffs.empty and "attempt_id" in attempts.columns:
        attempts = attempts.merge(
            metric_diffs.drop(columns=["lane"], errors="ignore"), on="attempt_id", how="left")

    candidates = build_candidate_summary(attempts)
    repositories = build_repository_summary(candidates)
    repository_families = build_repository_family_summary(repositories)
    overview = build_overview(attempts)

    # RQ6 depth: mutation-operator survival profile.
    mutation_operators = build_mutation_operators(db_paths)

    save_datasets(
        attempts,
        candidates,
        repositories,
        generated_tests,
        overview,
        out,
        tool_generated_test_links=tool_generated_test_links,
        mutation_operators=mutation_operators,
        assertion_observations=assertions if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION else None,
        traced_assertions=traced_assertions if results_schema_version == ASSERTION_RESULTS_SCHEMA_VERSION else None,
    )
    if not repository_families.empty:
        repository_families.to_csv(out / "evaluation_repository_families.csv", index=False)

    print(f"Datasets written to {out}")
    print(f"  attempts:     {len(attempts):,}")
    print(f"  candidates:   {len(candidates):,}")
    print(f"  repositories: {len(repositories):,}")
    print(f"  repo families: {len(repository_families):,}")
    print(f"  gen. tests:   {len(generated_tests):,}")
    print(f"  tool links:   {len(tool_generated_test_links):,}")
    print(f"  assertion observations: {len(assertions):,}")
    if results_schema_version == LEGACY_RESULTS_SCHEMA_VERSION:
        print(f"  legacy assertion inventory rows: {len(assertion_counts):,}")
    print(f"  mutation ops:   {len(mutation_operators):,}")
