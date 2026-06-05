"""Reusable figure functions for evaluation notebooks.

All functions accept an optional ``ax`` argument and return the ``Axes``
they drew on, so callers can arrange them in any layout.
"""

from __future__ import annotations

from typing import Optional

import matplotlib.pyplot as plt
import matplotlib.axes
import numpy as np
import pandas as pd
import seaborn as sns

Axes = matplotlib.axes.Axes


def _ax(ax: Optional[Axes] = None) -> Axes:
    if ax is None:
        _, ax = plt.subplots()
    return ax


# ---------------------------------------------------------------------------
# Funnel / outcome charts
# ---------------------------------------------------------------------------

def outcome_funnel(
    df: pd.DataFrame,
    lane: str,
    stage_col: str = "failure_stage",
    success_col: str = "validated_success",
    ax: Optional[Axes] = None,
) -> Axes:
    """Horizontal bar funnel showing attempt counts at each pipeline stage."""
    ax = _ax(ax)
    lane_df = df[df.get("lane", pd.Series()) == lane] if "lane" in df.columns else df
    stages = ["total", "produced_change", "build_passed", "tests_passed", "validated"]
    counts: dict[str, int] = {
        "total": len(lane_df),
        "produced_change": int(lane_df.get("produced_change", pd.Series(dtype=bool)).sum()),
        "validated": int(lane_df.get(success_col, pd.Series(dtype=bool)).sum()),
    }
    labels = [s for s in stages if s in counts]
    values = [counts[s] for s in labels]
    ax.barh(labels[::-1], values[::-1], color=sns.color_palette("Blues_d", len(labels)))
    ax.set_xlabel("Attempts")
    ax.set_title(f"Outcome funnel — {lane} lane")
    return ax


def stacked_outcome_bar(
    df: pd.DataFrame,
    group_col: str,
    outcome_col: str = "failure_kind",
    ax: Optional[Axes] = None,
) -> Axes:
    """Stacked bar chart of outcome counts grouped by *group_col*."""
    ax = _ax(ax)
    if group_col not in df.columns or outcome_col not in df.columns:
        return ax
    ct = df.groupby([group_col, outcome_col]).size().unstack(fill_value=0)
    ct.plot(kind="bar", stacked=True, ax=ax)
    ax.set_ylabel("Attempts")
    ax.set_title(f"Outcomes by {group_col}")
    ax.legend(bbox_to_anchor=(1.05, 1), loc="upper left", fontsize="small")
    return ax


# ---------------------------------------------------------------------------
# Metric delta distributions
# ---------------------------------------------------------------------------

def metric_delta_distribution(
    df: pd.DataFrame,
    metric_col: str,
    group_col: str,
    ax: Optional[Axes] = None,
) -> Axes:
    """Box/violin plot of *metric_col* distributions by *group_col*."""
    ax = _ax(ax)
    if metric_col not in df.columns or group_col not in df.columns:
        return ax
    clean = df[[group_col, metric_col]].dropna()
    order = sorted(clean[group_col].unique())
    sns.boxplot(data=clean, x=group_col, y=metric_col, order=order, ax=ax)
    ax.axhline(0, linestyle="--", color="grey", linewidth=0.8)
    ax.set_title(f"{metric_col} by {group_col}")
    ax.tick_params(axis="x", rotation=45)
    return ax


def before_after_slope(
    df: pd.DataFrame,
    before_col: str,
    after_col: str,
    group_col: Optional[str] = None,
    ax: Optional[Axes] = None,
) -> Axes:
    """Slope (spaghetti) chart showing before → after metric values."""
    ax = _ax(ax)
    if before_col not in df.columns or after_col not in df.columns:
        return ax
    clean = df[[before_col, after_col]].dropna()
    for _, row in clean.iterrows():
        ax.plot([0, 1], [row[before_col], row[after_col]], alpha=0.3, color="steelblue")
    means = [clean[before_col].mean(), clean[after_col].mean()]
    ax.plot([0, 1], means, color="darkblue", linewidth=2, label="mean")
    ax.set_xticks([0, 1])
    ax.set_xticklabels(["Before", "After"])
    ax.set_title(f"{after_col.replace('_after', '')} before vs after")
    return ax


# ---------------------------------------------------------------------------
# Cost / efficiency charts
# ---------------------------------------------------------------------------

def cost_vs_improvement_scatter(
    df: pd.DataFrame,
    cost_col: str,
    improvement_col: str,
    group_col: Optional[str] = None,
    ax: Optional[Axes] = None,
) -> Axes:
    """Scatter plot of cost vs metric improvement, optionally coloured by group."""
    ax = _ax(ax)
    if cost_col not in df.columns or improvement_col not in df.columns:
        return ax
    clean = df[[cost_col, improvement_col] + ([group_col] if group_col else [])].dropna()
    if group_col:
        groups = clean[group_col].unique()
        palette = sns.color_palette(n_colors=len(groups))
        for (grp, color) in zip(groups, palette):
            sub = clean[clean[group_col] == grp]
            ax.scatter(sub[cost_col], sub[improvement_col], label=str(grp),
                       alpha=0.6, color=color, s=30)
        ax.legend(fontsize="small")
    else:
        ax.scatter(clean[cost_col], clean[improvement_col], alpha=0.6, s=30)
    ax.set_xlabel(cost_col)
    ax.set_ylabel(improvement_col)
    ax.set_title("Cost vs improvement")
    return ax


# ---------------------------------------------------------------------------
# Robustness heatmap
# ---------------------------------------------------------------------------

def robustness_heatmap(
    df: pd.DataFrame,
    model_col: str,
    repo_col: str,
    metric_col: str = "validated_success",
    ax: Optional[Axes] = None,
) -> Axes:
    """Heatmap of *metric_col* by model/tool (rows) × repository (columns)."""
    ax = _ax(ax)
    if not all(c in df.columns for c in [model_col, repo_col, metric_col]):
        return ax
    pivot = df.pivot_table(values=metric_col, index=model_col, columns=repo_col,
                           aggfunc="mean")
    sns.heatmap(pivot, ax=ax, cmap="RdYlGn", center=0.5, linewidths=0.5,
                annot=True, fmt=".2f", cbar_kws={"label": metric_col})
    ax.set_title(f"{metric_col} by model/tool × repository")
    return ax


# ---------------------------------------------------------------------------
# Paired comparison matrix
# ---------------------------------------------------------------------------

def paired_win_loss_matrix(
    paired_df: pd.DataFrame,
    winner_col: str = "winner",
    ax: Optional[Axes] = None,
) -> Axes:
    """Bar chart of win/loss/tie counts from the paired comparison table."""
    ax = _ax(ax)
    if winner_col not in paired_df.columns:
        return ax
    counts = paired_df[winner_col].value_counts()
    colors = {
        "llm_won": "steelblue",
        "agentic_won": "darkorange",
        "tie_success": "mediumseagreen",
        "tie_failure": "salmon",
        "llm_only": "lightblue",
        "agentic_only": "moccasin",
        "no_comparable_result": "lightgrey",
    }
    bar_colors = [colors.get(k, "grey") for k in counts.index]
    ax.bar(counts.index, counts.values, color=bar_colors)
    ax.set_ylabel("Candidates")
    ax.set_title("Paired comparison: LLM vs agentic")
    ax.tick_params(axis="x", rotation=30)
    return ax


# ---------------------------------------------------------------------------
# Failure taxonomy charts
# ---------------------------------------------------------------------------

def failure_category_bar(
    df: pd.DataFrame,
    lane_col: str = "lane",
    category_col: str = "failure_category",
    ax: Optional[Axes] = None,
) -> Axes:
    """Grouped bar chart of failure category counts by lane."""
    ax = _ax(ax)
    if lane_col not in df.columns or category_col not in df.columns:
        return ax
    ct = df.groupby([category_col, lane_col]).size().unstack(fill_value=0)
    ct.plot(kind="bar", ax=ax)
    ax.set_ylabel("Attempts")
    ax.set_title("Failure categories by lane")
    ax.tick_params(axis="x", rotation=45)
    ax.legend(title="Lane")
    return ax
