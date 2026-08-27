"""Figures for the MSR validation notebook.

Same convention as :mod:`analysis.plots`: every function takes an optional ``ax``
and returns the ``Axes`` it drew on, so callers control the layout.

These are diagnostic figures, not presentation ones. They exist to make a shape
visible next to its table — a funnel that drops off a cliff, a defect rate that is
one repository rather than all of them, a distribution with an impossible tail.
"""

from __future__ import annotations

from typing import Optional

import matplotlib.axes
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

Axes = matplotlib.axes.Axes

SEVERITY_COLORS = {
    "defect": "#b2182b",
    "coverage_gap": "#ef8a62",
    "suspect": "#67a9cf",
}

BAR_COLOR = "steelblue"


def _ax(ax: Optional[Axes] = None, figsize: tuple = (8, 4)) -> Axes:
    if ax is None:
        _, ax = plt.subplots(figsize=figsize)
    return ax


def _annotate_shares(ax: Axes, values, labels, horizontal: bool = True) -> None:
    """Write the share next to each bar so the table isn't needed to read it."""
    for index, (value, label) in enumerate(zip(values, labels)):
        if horizontal:
            ax.text(value, index, f"  {label}", va="center", fontsize=9)
        else:
            ax.text(index, value, f"{label}", ha="center", va="bottom", fontsize=9)


# ---------------------------------------------------------------------------
# Population
# ---------------------------------------------------------------------------

def outcome_bar(outcomes: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Target outcomes: completed, failed, timed out."""
    ax = _ax(ax, (8, 2.6))
    if outcomes.empty:
        return ax
    data = outcomes.sort_values("targets")
    ax.barh(data["outcome"], data["targets"], color=BAR_COLOR, alpha=0.85)
    _annotate_shares(ax, data["targets"], [f"{s:.1%}" for s in data["share"]])
    ax.set_xlabel("Targets")
    ax.set_title("Pipeline outcome")
    ax.set_xlim(0, data["targets"].max() * 1.18)
    return ax


def failure_reason_bar(
    failures: pd.DataFrame, top_n: int = 10, ax: Optional[Axes] = None
) -> Axes:
    """Why targets did not complete, most common first."""
    ax = _ax(ax, (8, 4))
    if failures.empty:
        return ax
    data = failures.nlargest(top_n, "targets").sort_values("targets")
    colors = ["#ef8a62" if o == "timeout" else BAR_COLOR for o in data["outcome"]]
    ax.barh(data["failure_reason"], data["targets"], color=colors, alpha=0.85)
    _annotate_shares(ax, data["targets"], [f"{s:.1%}" for s in data["share_of_failed"]])
    ax.set_xlabel("Targets (share of all non-completions labelled)")
    ax.set_title("Why targets did not complete")
    ax.set_xlim(0, data["targets"].max() * 1.25)
    return ax


def capability_funnel(capability: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Pipeline capabilities in cascade order — a funnel, so keep the given order."""
    ax = _ax(ax, (8, 3.2))
    if capability.empty:
        return ax
    ax.bar(capability["capability"], capability["share"], color=BAR_COLOR, alpha=0.85)
    for index, row in enumerate(capability.itertuples()):
        ax.text(index, row.share, f"{row.share:.0%}\n({row.repositories:,})",
                ha="center", va="bottom", fontsize=9)
    ax.set_ylabel("Share of denominator")
    ax.set_ylim(0, min(1.0, capability["share"].max() * 1.45))
    ax.set_title("Pipeline capability funnel")
    ax.tick_params(axis="x", rotation=20)
    return ax


def availability_bar(availability: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Share of repositories carrying each construct."""
    ax = _ax(ax, (8, 3))
    if availability.empty:
        return ax
    data = availability.sort_values("share")
    ax.barh(data.index, data["share"], color=BAR_COLOR, alpha=0.85)
    _annotate_shares(
        ax, data["share"], [f"{s:.0%} ({n:,})" for s, n in zip(data["share"], data["with_data"])]
    )
    ax.set_xlabel("Share of repositories")
    ax.set_xlim(0, 1.25)
    ax.set_title("Construct availability")
    return ax


# ---------------------------------------------------------------------------
# Distributions
# ---------------------------------------------------------------------------

def distribution_box(
    frame: pd.DataFrame,
    columns: list[str],
    log: bool = True,
    ax: Optional[Axes] = None,
) -> Axes:
    """Boxplots for several numeric columns of one construct.

    Log scale by default: these distributions are heavy-tailed enough that a
    linear axis collapses every box into a line. Columns with non-positive values
    fall back to a linear axis, since a log axis would silently drop them.
    """
    ax = _ax(ax, (8, max(2.2, 0.5 * len(columns) + 1)))
    present = [c for c in columns if c in frame.columns and frame[c].notna().any()]
    if not present:
        # Never hand back a silently blank panel: say why it is empty.
        ax.set_title("No numeric columns to describe")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax

    series = [frame[c].dropna().to_numpy() for c in present]
    use_log = log and all((values > 0).all() for values in series if values.size)

    ax.boxplot(series, orientation="horizontal", tick_labels=present, showfliers=False,
               medianprops={"color": "#b2182b"})
    if use_log:
        ax.set_xscale("log")
        ax.set_xlabel("value (log scale)")
    else:
        ax.set_xlabel("value")
    ax.set_title("Distribution (box: quartiles, whiskers: 1.5 IQR, outliers hidden)")
    return ax


def outlier_bar(iqr_frame: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Tukey outliers per column, split by side.

    The low side is the diagnostic one — these columns are bounded below at zero,
    so a left-side outlier is usually a value that should not exist.
    """
    ax = _ax(ax, (8, max(2.2, 0.45 * len(iqr_frame) + 1)))
    data = (
        iqr_frame.dropna(subset=["outliers_low", "outliers_high"])
        if not iqr_frame.empty and "outliers_low" in iqr_frame.columns
        else pd.DataFrame()
    )
    if data.empty:
        ax.set_title("No numeric columns to describe")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax
    positions = np.arange(len(data))
    ax.barh(positions - 0.2, data["outliers_high"], height=0.4,
            color=BAR_COLOR, alpha=0.85, label="high side")
    ax.barh(positions + 0.2, data["outliers_low"], height=0.4,
            color="#b2182b", alpha=0.85, label="low side")
    ax.set_yticks(positions, data["column"])
    ax.set_xlabel("Rows outside the Tukey fence")
    ax.set_title("Outliers by side")
    ax.legend(fontsize=8)
    return ax


# ---------------------------------------------------------------------------
# Structural checks
# ---------------------------------------------------------------------------

def check_rate_bar(summary: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Corpus rate for every check that fired, coloured by severity."""
    ax = _ax(ax, (9, max(3, 0.4 * len(summary) + 1)))
    fired = summary[summary["numerator"] > 0].sort_values("rate")
    if fired.empty:
        ax.set_title("No structural check fired")
        return ax

    colors = [SEVERITY_COLORS.get(s, BAR_COLOR) for s in fired["severity"]]
    ax.barh(fired["check"], fired["rate"], color=colors, alpha=0.9)
    _annotate_shares(
        ax, fired["rate"],
        [f"{r:.1%} ({n:,}/{d:,})" for r, n, d in
         zip(fired["rate"], fired["numerator"], fired["denominator"])],
    )
    ax.set_xlabel("Rate (numerator / denominator, pooled across repositories)")
    ax.set_xlim(0, 1.55)
    ax.set_title("Structural checks that fired")
    handles = [plt.Rectangle((0, 0), 1, 1, color=c) for c in SEVERITY_COLORS.values()]
    ax.legend(handles, SEVERITY_COLORS.keys(), fontsize=8, loc="lower right")
    return ax


def per_repo_rate_strip(
    checks: pd.DataFrame,
    check_names: list[str],
    ax: Optional[Axes] = None,
) -> Axes:
    """Per-repository rates for selected checks.

    Corpus rates hide concentration. A band spread across every repository is a
    systematic fault in the extractor; a few points at the top with the rest at
    zero is a property of those repositories.
    """
    ax = _ax(ax, (9, max(2.5, 0.6 * len(check_names) + 1)))
    present = [c for c in check_names if (checks["check"] == c).any()]
    if not present:
        return ax

    rng = np.random.default_rng(0)
    for index, name in enumerate(present):
        rates = checks.loc[checks["check"] == name, "rate"].dropna()
        jitter = rng.uniform(-0.16, 0.16, size=len(rates))
        ax.scatter(rates, np.full(len(rates), index) + jitter,
                   s=14, alpha=0.5, color=BAR_COLOR, edgecolors="none")
        ax.scatter([rates.median()], [index], marker="|", s=400, color="#b2182b", zorder=3)

    ax.set_yticks(range(len(present)), present)
    ax.set_xlabel("Per-repository rate (red bar: median)")
    ax.set_xlim(-0.03, 1.03)
    ax.set_title("Concentration across repositories")
    return ax


# ---------------------------------------------------------------------------
# Per-construct
# ---------------------------------------------------------------------------

def category_bar(
    counts: pd.Series,
    title: str,
    xlabel: str = "Rows",
    top_n: int = 15,
    ax: Optional[Axes] = None,
) -> Axes:
    """Horizontal bar of a value-count series, largest first."""
    ax = _ax(ax, (8, max(2.2, 0.4 * min(len(counts), top_n) + 1)))
    if counts.empty:
        return ax
    data = counts.nlargest(top_n).sort_values()
    total = counts.sum()
    ax.barh(data.index.astype(str), data.to_numpy(), color=BAR_COLOR, alpha=0.85)
    _annotate_shares(ax, data.to_numpy(), [f"{v:,} ({v / total:.0%})" for v in data])
    ax.set_xlabel(xlabel)
    ax.set_xlim(0, data.max() * 1.3)
    ax.set_title(title)
    return ax


def grouped_box(
    frame: pd.DataFrame,
    value_col: str,
    group_col: str,
    log: bool = True,
    title: str = "",
    xlabel: str = "",
    ax: Optional[Axes] = None,
) -> Axes:
    """Boxplot of *value_col* split by *group_col*, ordered by median.

    Used for per-category distributions — how a smell type's frequency varies
    across repositories, rather than just its corpus total.
    """
    groups = [(name, g[value_col].dropna().to_numpy()) for name, g in frame.groupby(group_col)]
    groups = [(name, values) for name, values in groups if values.size]
    if not groups:
        ax = _ax(ax)
        ax.set_title(title or "No data")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax

    groups.sort(key=lambda pair: np.median(pair[1]))
    labels = [name for name, _ in groups]
    series = [values for _, values in groups]

    ax = _ax(ax, (8, max(2.4, 0.45 * len(groups) + 1)))
    ax.boxplot(series, orientation="horizontal", tick_labels=labels, showfliers=False,
               medianprops={"color": "#b2182b"})
    if log and all((values > 0).all() for values in series):
        ax.set_xscale("log")
        xlabel = f"{xlabel} (log scale)" if xlabel else "value (log scale)"
    ax.set_xlabel(xlabel or value_col)
    ax.set_title(title or f"{value_col} by {group_col}")
    return ax


def confidence_hist(mappings: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Mapping confidence, split by the grounded flag."""
    ax = _ax(ax, (8, 3))
    if mappings.empty or "confidence" not in mappings.columns:
        return ax
    for flag, label, color in ((1, "grounded", BAR_COLOR), (0, "not grounded", "#b2182b")):
        subset = mappings.loc[mappings["is_grounded"] == flag, "confidence"].dropna()
        if subset.empty:
            continue
        ax.hist(subset, bins=20, alpha=0.6, label=f"{label} ({len(subset):,})", color=color)
    ax.set_xlabel("confidence")
    ax.set_ylabel("mappings")
    ax.set_title("Mapping confidence by grounded flag")
    ax.legend(fontsize=8)
    return ax
