"""Figures for the MSR validation notebook.

Same convention as :mod:`analysis.plots`: every function takes an optional ``ax``
and returns the ``Axes`` it drew on, so callers control the layout.

These are diagnostic figures, not presentation ones. They exist to make a shape
visible next to its table — a funnel that drops off a cliff, a defect rate that is
one repository rather than all of them, a distribution with an impossible tail.

Every figure carries a short bold title and a grey subtitle underneath: the title
names the thing, the subtitle says how to read it. Values are annotated on the
marks themselves so a figure can be read without its table.
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
MEDIAN_COLOR = "#b2182b"


def _ax(ax: Optional[Axes] = None, figsize: tuple = (8, 4)) -> Axes:
    if ax is None:
        _, ax = plt.subplots(figsize=figsize)
    return ax


def _titled(ax: Axes, title: str, subtitle: str = "") -> Axes:
    """Set a bold left-aligned title with a grey subtitle beneath it.

    The subtitle is offset in points rather than axes fractions: an axes-fraction
    offset shrinks as the figure grows taller, so on the tall charts it collided
    with the title.
    """
    if not subtitle:
        ax.set_title(title, fontsize=12, fontweight="bold", loc="left", pad=8)
        return ax

    ax.set_title(title, fontsize=12, fontweight="bold", loc="left", pad=26)
    ax.annotate(subtitle, xy=(0, 1), xycoords="axes fraction",
                xytext=(0, 7), textcoords="offset points",
                fontsize=9, color="#555555", va="bottom", ha="left")
    return ax


def _fmt(value) -> str:
    """Compact number formatting for on-chart labels."""
    if value is None or (isinstance(value, float) and value != value):
        return ""
    if abs(value) >= 1000:
        return f"{value:,.0f}"
    if float(value).is_integer():
        return f"{int(value)}"
    if abs(value) < 1:
        return f"{value:.3g}"
    return f"{value:.1f}"


def _annotate_bars(ax: Axes, values, labels, horizontal: bool = True) -> None:
    """Write a label at the end of each bar."""
    for index, (value, label) in enumerate(zip(values, labels)):
        if horizontal:
            ax.text(value, index, f"  {label}", va="center", fontsize=9)
        else:
            ax.text(index, value, f"{label}", ha="center", va="bottom", fontsize=9)


def _annotate_box(ax: Axes, artists: dict, series: list) -> None:
    """Label the median of each box, and the upper whisker tip.

    The whisker tip matters because outliers are hidden: without it there is no
    way to tell a short whisker from a short distribution.
    """
    for index, median_line in enumerate(artists.get("medians", [])):
        x = median_line.get_xdata()[0]
        y = median_line.get_ydata()
        ax.text(x, max(y) + 0.06, _fmt(x), ha="center", va="bottom",
                fontsize=8, color=MEDIAN_COLOR, fontweight="bold")

    caps = artists.get("caps", [])
    for index in range(len(series)):
        if 2 * index + 1 >= len(caps):
            break
        upper = caps[2 * index + 1]
        x = upper.get_xdata()[0]
        y = upper.get_ydata()[0]
        maximum = series[index].max() if series[index].size else None
        label = f" {_fmt(x)}"
        if maximum is not None and maximum > x:
            label += f" (max {_fmt(maximum)})"
        ax.text(x, y, label, va="center", ha="left", fontsize=8, color="#555555")


# ---------------------------------------------------------------------------
# Population
# ---------------------------------------------------------------------------

def outcome_bar(outcomes: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Target outcomes: completed, failed, timed out."""
    ax = _ax(ax, (8, 2.8))
    if outcomes.empty:
        return ax
    data = outcomes.sort_values("targets")
    ax.barh(data["outcome"], data["targets"], color=BAR_COLOR, alpha=0.85)
    _annotate_bars(ax, data["targets"],
                   [f"{t:,} ({s:.1%})" for t, s in zip(data["targets"], data["share"])])
    ax.set_xlabel("Targets")
    ax.set_xlim(0, data["targets"].max() * 1.28)
    return _titled(ax, "Pipeline outcome", "every target in the manifest, by terminal state")


def failure_reason_bar(
    failures: pd.DataFrame, top_n: int = 10, ax: Optional[Axes] = None
) -> Axes:
    """Why targets did not complete, most common first."""
    ax = _ax(ax, (8, 4.2))
    if failures.empty:
        return ax
    data = failures.nlargest(top_n, "targets").sort_values("targets")
    colors = ["#ef8a62" if o == "timeout" else BAR_COLOR for o in data["outcome"]]
    ax.barh(data["failure_reason"], data["targets"], color=colors, alpha=0.85)
    _annotate_bars(ax, data["targets"],
                   [f"{t:,} ({s:.1%})" for t, s in zip(data["targets"], data["share_of_failed"])])
    ax.set_xlabel("Targets")
    ax.set_xlim(0, data["targets"].max() * 1.35)
    return _titled(ax, "Why targets did not complete",
                   "count and share of all non-completions; orange = timeout")


def capability_funnel(capability: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Pipeline capabilities in cascade order — a funnel, so keep the given order."""
    ax = _ax(ax, (8, 3.6))
    if capability.empty:
        return ax
    ax.bar(capability["capability"], capability["share"], color=BAR_COLOR, alpha=0.85)
    for index, row in enumerate(capability.itertuples()):
        ax.text(index, row.share, f"{row.share:.0%}\n{row.repositories:,}",
                ha="center", va="bottom", fontsize=9)
    ax.set_ylabel("Share of denominator")
    ax.set_ylim(0, min(1.0, capability["share"].max() * 1.5))
    ax.tick_params(axis="x", rotation=20)
    return _titled(ax, "Pipeline capability funnel",
                   "stages cascade, so each bar can only lose repositories")


def availability_bar(availability: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Share of repositories carrying each construct."""
    ax = _ax(ax, (8, 3.2))
    if availability.empty:
        return ax
    data = availability.sort_values("share")
    ax.barh(data.index, data["share"], color=BAR_COLOR, alpha=0.85)
    _annotate_bars(ax, data["share"],
                   [f"{s:.0%} ({n:,} repos)" for s, n in zip(data["share"], data["with_data"])])
    ax.set_xlabel("Share of repositories")
    ax.set_xlim(0, 1.3)
    return _titled(ax, "Construct availability",
                   "repositories holding any data for each construct")


# ---------------------------------------------------------------------------
# Distributions
# ---------------------------------------------------------------------------

BOX_SUBTITLE = ("box: Q1 to Q3  ·  red line: median  ·  whisker: last value within "
                "1.5x IQR  ·  outliers hidden")


def distribution_box(
    frame: pd.DataFrame,
    columns: list[str],
    log: bool = True,
    title: str = "Distribution",
    ax: Optional[Axes] = None,
) -> Axes:
    """Boxplots for several numeric columns of one construct.

    Log scale by default: these distributions are heavy-tailed enough that a
    linear axis collapses every box into a line. Columns with non-positive values
    fall back to a linear axis, since a log axis would silently drop them.
    """
    ax = _ax(ax, (9, max(2.6, 0.62 * len(columns) + 1.4)))
    present = [c for c in columns if c in frame.columns and frame[c].notna().any()]
    if not present:
        _titled(ax, title, "no numeric columns to describe")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax

    series = [frame[c].dropna().to_numpy() for c in present]
    use_log = log and all((values > 0).all() for values in series if values.size)

    artists = ax.boxplot(series, orientation="horizontal", tick_labels=present,
                         showfliers=False, medianprops={"color": MEDIAN_COLOR})
    _annotate_box(ax, artists, series)

    if use_log:
        ax.set_xscale("log")
        ax.set_xlabel("value (log scale)")
    else:
        ax.set_xlabel("value")
        ax.set_xlim(right=ax.get_xlim()[1] * 1.22)
    return _titled(ax, title, f"{len(frame):,} rows  ·  {BOX_SUBTITLE}")


def outlier_bar(
    iqr_frame: pd.DataFrame,
    title: str = "Outliers by side",
    ax: Optional[Axes] = None,
) -> Axes:
    """Tukey outliers per column, split by side.

    The low side is the diagnostic one — these columns are bounded below at zero,
    so a left-side outlier is usually a value that should not exist. Read a count
    only after checking the fence lies inside the column's possible range.
    """
    ax = _ax(ax, (9, max(2.6, 0.55 * len(iqr_frame) + 1.4)))
    data = (
        iqr_frame.dropna(subset=["outliers_low", "outliers_high"])
        if not iqr_frame.empty and "outliers_low" in iqr_frame.columns
        else pd.DataFrame()
    )
    if data.empty:
        _titled(ax, title, "no numeric columns to describe")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax

    positions = np.arange(len(data))
    ax.barh(positions - 0.2, data["outliers_high"], height=0.4,
            color=BAR_COLOR, alpha=0.85, label="high side")
    ax.barh(positions + 0.2, data["outliers_low"], height=0.4,
            color=MEDIAN_COLOR, alpha=0.85, label="low side")

    for index, row in enumerate(data.itertuples()):
        if row.outliers_high:
            ax.text(row.outliers_high, index - 0.2, f"  {row.outliers_high:,}",
                    va="center", fontsize=8)
        if row.outliers_low:
            ax.text(row.outliers_low, index + 0.2, f"  {row.outliers_low:,}",
                    va="center", fontsize=8, color=MEDIAN_COLOR)

    ax.set_yticks(positions, data["column"])
    ax.set_xlabel("Rows beyond the fence")
    ax.set_xlim(0, max(1, data[["outliers_low", "outliers_high"]].to_numpy().max()) * 1.25)
    ax.legend(fontsize=8, loc="lower right")
    return _titled(ax, title,
                   "fences: Q1 - 1.5x IQR and Q3 + 1.5x IQR  ·  a count means nothing "
                   "if its fence sits outside the column's range")


def grouped_box(
    frame: pd.DataFrame,
    value_col: str,
    group_col: str,
    log: bool = True,
    title: str = "",
    subtitle: str = "",
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
        _titled(ax, title or "No data", "")
        ax.set_xticks([])
        ax.set_yticks([])
        return ax

    groups.sort(key=lambda pair: np.median(pair[1]))
    labels = [name for name, _ in groups]
    series = [values for _, values in groups]

    ax = _ax(ax, (9, max(2.8, 0.5 * len(groups) + 1.6)))
    artists = ax.boxplot(series, orientation="horizontal", tick_labels=labels,
                         showfliers=False, medianprops={"color": MEDIAN_COLOR})
    _annotate_box(ax, artists, series)

    if log and all((values > 0).all() for values in series):
        ax.set_xscale("log")
        xlabel = f"{xlabel} (log scale)" if xlabel else "value (log scale)"
    else:
        ax.set_xlim(right=ax.get_xlim()[1] * 1.25)
    ax.set_xlabel(xlabel or value_col)
    return _titled(ax, title or f"{value_col} by {group_col}",
                   subtitle or f"one box per {group_col}  ·  {BOX_SUBTITLE}")


# ---------------------------------------------------------------------------
# Structural checks
# ---------------------------------------------------------------------------

def check_rate_bar(summary: pd.DataFrame, ax: Optional[Axes] = None) -> Axes:
    """Corpus rate for every check that fired, coloured by severity."""
    ax = _ax(ax, (9.5, max(3.2, 0.42 * len(summary) + 1.4)))
    fired = summary[summary["numerator"] > 0].sort_values("rate")
    if fired.empty:
        return _titled(ax, "Structural checks", "no check fired")

    colors = [SEVERITY_COLORS.get(s, BAR_COLOR) for s in fired["severity"]]
    ax.barh(fired["check"], fired["rate"], color=colors, alpha=0.9)
    _annotate_bars(
        ax, fired["rate"],
        [f"{r:.1%}  ({n:,} / {d:,})" for r, n, d in
         zip(fired["rate"], fired["numerator"], fired["denominator"])],
    )
    ax.set_xlabel("Rate")
    ax.set_xlim(0, 1.62)
    handles = [plt.Rectangle((0, 0), 1, 1, color=c) for c in SEVERITY_COLORS.values()]
    ax.legend(handles, SEVERITY_COLORS.keys(), fontsize=8, loc="lower right")
    return _titled(ax, "Structural checks that fired",
                   "numerator / denominator pooled across repositories")


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
    ax = _ax(ax, (9.5, max(2.8, 0.62 * len(check_names) + 1.4)))
    present = [c for c in check_names if (checks["check"] == c).any()]
    if not present:
        return _titled(ax, "Concentration across repositories", "no check selected")

    rng = np.random.default_rng(0)
    for index, name in enumerate(present):
        rates = checks.loc[checks["check"] == name, "rate"].dropna()
        jitter = rng.uniform(-0.16, 0.16, size=len(rates))
        ax.scatter(rates, np.full(len(rates), index) + jitter,
                   s=14, alpha=0.5, color=BAR_COLOR, edgecolors="none")
        median = rates.median()
        ax.scatter([median], [index], marker="|", s=400, color=MEDIAN_COLOR, zorder=3)
        ax.text(1.04, index, f"median {median:.0%}  n={len(rates)}",
                va="center", fontsize=8, color="#555555")

    ax.set_yticks(range(len(present)), present)
    ax.set_xlabel("Per-repository rate")
    ax.set_xlim(-0.03, 1.03)
    return _titled(ax, "Concentration across repositories",
                   "one point per repository  ·  red bar: median  ·  "
                   "spread out = systematic, clustered at zero = local")


# ---------------------------------------------------------------------------
# Per-construct
# ---------------------------------------------------------------------------

def stacked_share_bar(
    shares: pd.DataFrame,
    title: str,
    subtitle: str = "",
    xlabel: str = "Share",
    colors: Optional[list] = None,
    ax: Optional[Axes] = None,
) -> Axes:
    """Horizontal stacked bars of row-wise shares.

    *shares* is indexed by group with one column per outcome, each row summing to
    1. Segment labels are written inside any segment wide enough to hold them.
    """
    ax = _ax(ax, (9.5, max(2.8, 0.55 * len(shares) + 1.6)))
    if shares.empty:
        return _titled(ax, title, "no data")

    palette = colors or ["#4393c3", "#92c5de", "#f4a582", "#b2182b"]
    positions = np.arange(len(shares))
    left = np.zeros(len(shares))

    for index, column in enumerate(shares.columns):
        values = shares[column].to_numpy(dtype=float)
        ax.barh(positions, values, left=left, height=0.68,
                color=palette[index % len(palette)], label=str(column))
        for row, (value, start) in enumerate(zip(values, left)):
            if value >= 0.07:
                ax.text(start + value / 2, row, f"{value:.0%}",
                        ha="center", va="center", fontsize=8, color="white")
        left += values

    ax.set_yticks(positions, [str(i) for i in shares.index])
    ax.set_xlim(0, 1)
    ax.set_xlabel(xlabel)
    ax.legend(fontsize=8, ncol=len(shares.columns), loc="upper center",
              bbox_to_anchor=(0.5, -0.22), frameon=False)
    return _titled(ax, title, subtitle)


def category_bar(
    counts: pd.Series,
    title: str,
    xlabel: str = "Rows",
    subtitle: str = "",
    top_n: int = 15,
    ax: Optional[Axes] = None,
) -> Axes:
    """Horizontal bar of a value-count series, largest first."""
    ax = _ax(ax, (9, max(2.6, 0.42 * min(len(counts), top_n) + 1.4)))
    if counts.empty:
        return _titled(ax, title, "no data")
    data = counts.nlargest(top_n).sort_values()
    total = counts.sum()
    ax.barh(data.index.astype(str), data.to_numpy(), color=BAR_COLOR, alpha=0.85)
    _annotate_bars(ax, data.to_numpy(), [f"{_fmt(v)} ({v / total:.1%})" for v in data])
    ax.set_xlabel(xlabel)
    ax.set_xlim(0, data.max() * 1.32)
    shown = min(len(counts), top_n)
    return _titled(ax, title,
                   subtitle or f"{shown} of {len(counts)} categories  ·  "
                               f"share of {_fmt(total)} total")
