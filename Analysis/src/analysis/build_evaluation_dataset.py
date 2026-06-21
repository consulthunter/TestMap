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

from analysis.files import ensure_output_dir, read_results_csvs
from analysis.normalize import (
    build_candidate_summary,
    build_generated_tests_dataset,
    build_repository_summary,
    normalize_attempts,
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
    df = read_results_csvs(list(results))
    if df.empty:
        print("[warn] No result CSVs found.")
    return df


def build_attempts_dataset(raw_df: pd.DataFrame) -> pd.DataFrame:
    """Normalize raw attempt rows into the shared attempt-level schema."""
    if raw_df.empty:
        return pd.DataFrame()
    return normalize_attempts(raw_df)


def build_generated_tests_dataset_from_raw(
    raw_df: pd.DataFrame,
    db_paths: list[str] | tuple[str, ...] = (),
) -> pd.DataFrame:
    """Build a per-generated-test dataset.

    The primary source is the raw CSV rows (both lanes are at per-test grain
    before the agentic collapse step).  SQLite databases are an optional
    secondary source for additional metadata; they are queried when provided.

    Parameters
    ----------
    raw_df:
        The raw DataFrame as loaded from result CSVs, *before*
        ``normalize_attempts`` is called (so agentic rows have not been
        collapsed yet).
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
    """Per-attempt assertion counts of the generated tests (RQ3 guard + density).

    A generated test is persisted as a member and parsed into the ``invocations``
    table (with ``is_assertion``) only when it *validates*. The agentic lane links
    tests to members via ``tool_attempt_generated_tests``, so it uses **real Roslyn
    assertion detection** from invocations. The LLM lane has no member FK on
    ``generated_test_executions`` and its generated test names collide with agentic
    members, so it falls back to a heuristic regex over the stored
    ``generated_test_code``. ``assertion_source`` records which method was used.

    Returns one row per ``attempt_id`` with ``assertion_count``, ``invocation_count``
    (NA for the regex lane), ``generated_test_n``, and ``assertion_source``.
    """
    if not db_paths:
        return pd.DataFrame()

    from analysis.db import (
        connect,
        get_generated_test_executions,
        get_invocations,
        get_tool_attempt_generated_tests,
        get_tool_attempts,
    )
    from analysis.files import find_databases

    rows: list[dict] = []
    for db_path in find_databases(list(db_paths)):
        with connect(db_path) as conn:
            execs = get_generated_test_executions(conn)
            links = get_tool_attempt_generated_tests(conn)
            tool_attempts = get_tool_attempts(conn)
            invocations = get_invocations(conn)

        # LLM: regex over the stored generated test code (no reliable member link).
        if not execs.empty and "generation_attempt_id" in execs.columns:
            for gaid, grp in execs.groupby("generation_attempt_id"):
                asserts = int(grp.get("generated_test_code", pd.Series(dtype=str))
                              .map(_count_assertions).sum())
                rows.append({"attempt_id": f"llm:{int(gaid)}",
                             "assertion_count": asserts,
                             "invocation_count": pd.NA,
                             "generated_test_n": len(grp),
                             "assertion_source": "code_regex"})

        # Agentic: real assertion detection from invocations on the test members.
        if (not links.empty and "tool_attempt_id" in links.columns
                and not invocations.empty and "member_id" in invocations.columns):
            inv = invocations.copy()
            inv["is_assertion"] = pd.to_numeric(inv["is_assertion"], errors="coerce").fillna(0)
            per_member = inv.groupby("member_id").agg(
                assertions=("is_assertion", "sum"), invs=("is_assertion", "size"))
            artifact = (tool_attempts.set_index("id")["artifact_path"]
                        if (not tool_attempts.empty and "artifact_path" in tool_attempts.columns)
                        else pd.Series(dtype=str))
            links = links.drop_duplicates(["tool_attempt_id", "member_id"])
            for taid, grp in links.groupby("tool_attempt_id"):
                mids = [m for m in grp["member_id"] if m in per_member.index]
                asserts = int(per_member.loc[mids, "assertions"].sum()) if mids else 0
                ninv = int(per_member.loc[mids, "invs"].sum()) if mids else 0
                key = artifact.get(taid)
                key = str(taid) if key is None or pd.isna(key) or key == "" else str(key)
                rows.append({"attempt_id": f"agentic:{key}",
                             "assertion_count": asserts,
                             "invocation_count": ninv,
                             "generated_test_n": len(grp),
                             "assertion_source": "invocations"})

    return pd.DataFrame(rows) if rows else pd.DataFrame()


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

    from analysis.db import connect, get_mutants
    from analysis.files import find_databases

    frames: list[pd.DataFrame] = []
    for db_path in find_databases(list(db_paths)):
        repo_name = Path(db_path).parent.name
        with connect(db_path) as conn:
            mut = get_mutants(conn)
        if mut.empty or "mutator_name" not in mut.columns or "status" not in mut.columns:
            continue
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


def save_datasets(
    attempts: pd.DataFrame,
    candidates: pd.DataFrame,
    repositories: pd.DataFrame,
    generated_tests: pd.DataFrame,
    overview: dict,
    output_dir: Path,
    tool_generated_test_links: pd.DataFrame | None = None,
    mutation_operators: pd.DataFrame | None = None,
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
) -> None:
    """Entry point for the ``build-datasets`` CLI command."""
    out = ensure_output_dir(output_dir)

    print("Loading raw attempts...")
    raw = load_raw_attempts(results, db_paths)
    print(f"  {len(raw)} raw rows loaded.")

    # Build generated-test dataset BEFORE agentic rows are collapsed
    generated_tests = build_generated_tests_dataset_from_raw(raw, db_paths)
    tool_generated_test_links = build_tool_generated_test_links(db_paths)

    attempts = build_attempts_dataset(raw)

    # RQ3 assertion guard: attach generated-test assertion counts from the DB.
    assertion_counts = build_assertion_counts(db_paths)
    if not assertion_counts.empty and "attempt_id" in attempts.columns:
        attempts = attempts.merge(assertion_counts, on="attempt_id", how="left")

    candidates = build_candidate_summary(attempts)
    repositories = build_repository_summary(candidates)
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
    )

    print(f"Datasets written to {out}")
    print(f"  attempts:     {len(attempts):,}")
    print(f"  candidates:   {len(candidates):,}")
    print(f"  repositories: {len(repositories):,}")
    print(f"  gen. tests:   {len(generated_tests):,}")
    print(f"  tool links:   {len(tool_generated_test_links):,}")
    print(f"  assertion rows: {len(assertion_counts):,}")
    print(f"  mutation ops:   {len(mutation_operators):,}")
