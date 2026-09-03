"""Descriptive statistics for the MSR validation frames.

Two views, both tidy so they render as tables and feed plots directly:

``describe_numeric``  count/min/max/mean/median/std per numeric column
``iqr_table``         quartiles, IQR, Tukey fences, and outlier counts

Kept separate from :mod:`analysis.statistics`, which holds inferential tests.
"""

from __future__ import annotations

import pandas as pd


# Numeric columns worth describing, per construct frame. Identity columns
# (ids, report keys) are excluded: their distributions mean nothing.
# Identity, position, and size columns are deliberately absent: line numbers and
# character counts describe where code sits, not what the tool measured, and their
# distributions say nothing about whether the instrument works.
NUMERIC_COLUMNS = {
    "entities": [],
    "code_metrics": [
        "maintainability_index",
        "cyclomatic_complexity",
        "class_coupling",
        "depth_of_inheritance",
        "source_lines_of_code",
        "executable_lines_of_code",
    ],
    "test_smells": [],
    # Counts are not persisted on per-entity rows (see msr_validation_bugs.md),
    # so the rates are the only coverage values worth describing.
    "coverage": ["line_rate", "branch_rate"],
    "mutants": [
        "mutants_total", "killed", "survived", "nocoverage",
        "compileerror", "ignored", "timeout",
        "static_mutants", "distinct_mutators",
    ],
    # trace_steps duplicates path_length for these mappings; keep one.
    "mappings": ["path_length"],
}


def _numeric_columns(frame: pd.DataFrame, columns: list[str] | None) -> list[str]:
    if columns is None:
        return [c for c in frame.columns if pd.api.types.is_numeric_dtype(frame[c])]
    return [c for c in columns if c in frame.columns]


def describe_numeric(
    frame: pd.DataFrame,
    columns: list[str] | None = None,
    by: str | None = None,
) -> pd.DataFrame:
    """Return count, missing, min, max, mean, median, and std per column.

    Pass ``by`` to compute the same statistics within each group (for example
    ``by="repo_key"`` for a per-repository view, or ``by="entity_kind"``).
    """
    if frame.empty:
        return pd.DataFrame()

    cols = _numeric_columns(frame, columns)
    if not cols:
        return pd.DataFrame()

    def _stats(sub: pd.DataFrame) -> pd.DataFrame:
        rows = []
        for col in cols:
            series = sub[col]
            rows.append({
                "column": col,
                "count": int(series.notna().sum()),
                "missing": int(series.isna().sum()),
                "min": series.min(),
                "max": series.max(),
                "mean": series.mean(),
                "median": series.median(),
                "std": series.std(),
            })
        return pd.DataFrame(rows)

    if by is None:
        return _stats(frame)

    parts = []
    for key, group in frame.groupby(by, sort=True):
        part = _stats(group)
        part.insert(0, by, key)
        parts.append(part)
    return pd.concat(parts, ignore_index=True) if parts else pd.DataFrame()


def iqr_table(
    frame: pd.DataFrame,
    columns: list[str] | None = None,
    by: str | None = None,
    whisker: float = 1.5,
) -> pd.DataFrame:
    """Return quartiles, IQR, Tukey fences, and outlier counts per column.

    Fences are ``q1 - whisker * iqr`` and ``q3 + whisker * iqr``. Outliers are
    counted on both sides separately: for these constructs a long right tail is
    expected (a few very large methods), while left-side outliers usually mean
    an impossible value rather than a real one.
    """
    if frame.empty:
        return pd.DataFrame()

    cols = _numeric_columns(frame, columns)
    if not cols:
        return pd.DataFrame()

    def _stats(sub: pd.DataFrame) -> pd.DataFrame:
        rows = []
        for col in cols:
            series = sub[col].dropna()
            if series.empty:
                rows.append({"column": col, "count": 0})
                continue
            q1 = series.quantile(0.25)
            q3 = series.quantile(0.75)
            iqr = q3 - q1
            low = q1 - whisker * iqr
            high = q3 + whisker * iqr
            below = int((series < low).sum())
            above = int((series > high).sum())
            rows.append({
                "column": col,
                "count": int(series.size),
                "q1": q1,
                "median": series.median(),
                "q3": q3,
                "iqr": iqr,
                "lower_fence": low,
                "upper_fence": high,
                "outliers_low": below,
                "outliers_high": above,
                "outlier_rate": (below + above) / series.size,
            })
        return pd.DataFrame(rows)

    if by is None:
        return _stats(frame)

    parts = []
    for key, group in frame.groupby(by, sort=True):
        part = _stats(group)
        part.insert(0, by, key)
        parts.append(part)
    return pd.concat(parts, ignore_index=True) if parts else pd.DataFrame()


def summarize_checks(checks: pd.DataFrame, by_repo: bool = False) -> pd.DataFrame:
    """Aggregate the structural check frame.

    Corpus view (default) pools numerators and denominators across repositories,
    so the rate is the true corpus rate rather than a mean of per-repo rates.
    Set ``by_repo`` for the per-repository breakdown.
    """
    if checks.empty:
        return checks

    if by_repo:
        out = checks.copy()
        return out.sort_values(["construct", "check", "rate"], ascending=[True, True, False])

    grouped = (
        checks.groupby(["construct", "check", "severity"], as_index=False)
        .agg(
            repositories=("repo_key", "nunique"),
            repositories_affected=("numerator", lambda s: int((s > 0).sum())),
            numerator=("numerator", "sum"),
            denominator=("denominator", "sum"),
        )
    )
    grouped["rate"] = grouped["numerator"] / grouped["denominator"].where(
        grouped["denominator"] > 0
    )
    order = {"defect": 0, "coverage_gap": 1, "suspect": 2, "prevalence": 3}
    grouped["_order"] = grouped["severity"].map(order).fillna(3)
    return (
        grouped.sort_values(["_order", "rate"], ascending=[True, False])
        .drop(columns="_order")
        .reset_index(drop=True)
    )


def worst_repositories(checks: pd.DataFrame, check: str, top_n: int = 10) -> pd.DataFrame:
    """Return the repositories with the highest rate for one check."""
    subset = checks[checks["check"] == check].copy()
    if subset.empty:
        return subset
    return (
        subset.sort_values("rate", ascending=False)
        .head(top_n)
        .loc[:, ["repo_key", "check", "numerator", "denominator", "rate"]]
        .reset_index(drop=True)
    )
