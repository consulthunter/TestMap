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
    """Horizontal bar funnel showing attempt counts at each pipeline stage.

    Stages (requires outcome_classification column):
      total → produced_change → build_passed → tests_passed → validated (VEP)

    build_passed: output compiled (outcome not BuildFailed/ValidationFailed/tool error).
    tests_passed: generated tests ran and passed (VEP + VLI).
    validated:    tests passed AND metrics improved above noise floor (VEP only).
    """
    _BUILD_PASSED_OC = frozenset({
        "ValidatedEvidencePositive", "ValidatedLowImpact",
        "FailedEvidencePositive", "TestsFailed",
    })
    _TESTS_PASSED_OC = frozenset({
        "ValidatedEvidencePositive", "ValidatedLowImpact",
    })

    ax = _ax(ax)
    lane_df = df[df.get("lane", pd.Series()) == lane] if "lane" in df.columns else df
    oc = lane_df.get("outcome_classification", pd.Series(dtype=str))

    stages = ["total", "produced_change", "build_passed", "tests_passed", "validated"]
    counts: dict[str, int] = {
        "total": len(lane_df),
        "produced_change": int(lane_df.get("produced_change", pd.Series(dtype=bool)).fillna(False).sum()),
        "build_passed": int(oc.isin(_BUILD_PASSED_OC).sum()),
        "tests_passed": int(oc.isin(_TESTS_PASSED_OC).sum()),
        "validated": int(oc.eq("ValidatedEvidencePositive").sum()),
    }
    labels = stages
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
    clean = df[[group_col, outcome_col]].dropna()
    if clean.empty:
        ax.set_title(f"No outcome data by {group_col}")
        return ax
    ct = clean.groupby([group_col, outcome_col]).size().unstack(fill_value=0)
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
# Outcome classification
# ---------------------------------------------------------------------------

OUTCOME_COLORS: dict[str, str] = {
    "ValidatedEvidencePositive": "mediumseagreen",
    "FailedEvidencePositive":    "steelblue",
    "ValidationFailed":          "salmon",
}


def outcome_classification_bar(
    df: pd.DataFrame,
    group_col: Optional[str] = None,
    outcome_col: str = "outcome_classification",
    ax: Optional[Axes] = None,
) -> Axes:
    """Stacked bar of outcome_classification counts, optionally grouped by *group_col*."""
    ax = _ax(ax)
    if outcome_col not in df.columns:
        return ax
    if group_col and group_col in df.columns:
        ct = df.groupby([group_col, outcome_col]).size().unstack(fill_value=0)
        colors = [OUTCOME_COLORS.get(c, "grey") for c in ct.columns]
        ct.plot(kind="bar", stacked=True, ax=ax, color=colors)
        ax.tick_params(axis="x", rotation=30)
    else:
        counts = df[outcome_col].value_counts()
        colors = [OUTCOME_COLORS.get(k, "grey") for k in counts.index]
        ax.bar(counts.index, counts.values, color=colors)
        ax.tick_params(axis="x", rotation=20)
    ax.set_ylabel("Attempts")
    ax.set_title(f"Outcome classification{' by ' + group_col if group_col else ''}")
    ax.legend(bbox_to_anchor=(1.05, 1), loc="upper left", fontsize="small")
    return ax


# ---------------------------------------------------------------------------
# Test smell analysis
# ---------------------------------------------------------------------------


def smell_type_breakdown(
    df: pd.DataFrame,
    types_col: str,
    top_n: int = 15,
    ax: Optional[Axes] = None,
) -> Axes:
    """Horizontal bar chart of smell type frequencies from a JSON-array column."""
    import json as _json
    from collections import Counter
    ax = _ax(ax)
    if types_col not in df.columns:
        return ax
    counts: Counter = Counter()
    for val in df[types_col].dropna():
        try:
            for t in _json.loads(val):
                counts[t] += 1
        except (ValueError, TypeError):
            pass
    if not counts:
        ax.set_title(f"No smell data ({types_col})")
        return ax
    top = counts.most_common(top_n)
    labels, values = zip(*top)
    ax.barh(list(reversed(labels)), list(reversed(values)), color="steelblue", alpha=0.8)
    ax.set_xlabel("Attempts with smell type")
    ax.set_title(f"Smell types ({types_col.replace('_smell_types', '')})")
    return ax


# ---------------------------------------------------------------------------
# Timing breakdown
# ---------------------------------------------------------------------------


def timing_stacked_bar(
    df: pd.DataFrame,
    group_col: str,
    gen_col: str = "generation_duration_seconds",
    val_col: str = "validation_duration_seconds",
    ax: Optional[Axes] = None,
) -> Axes:
    """Stacked bar of median generation + validation duration by *group_col*."""
    ax = _ax(ax)
    present = [c for c in [gen_col, val_col] if c in df.columns]
    if not present or group_col not in df.columns:
        return ax
    medians = df.groupby(group_col)[present].median()
    medians.plot(kind="bar", stacked=True, ax=ax,
                 color=["steelblue", "darkorange"][: len(present)])
    ax.set_ylabel("Seconds (median)")
    ax.set_title(f"Timing breakdown by {group_col}")
    ax.tick_params(axis="x", rotation=30)
    ax.legend(title="Phase", bbox_to_anchor=(1.05, 1), loc="upper left", fontsize="small")
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
    clean = df[[category_col, lane_col]].dropna()
    if clean.empty:
        ax.set_title("No failure category data")
        return ax
    ct = clean.groupby([category_col, lane_col]).size().unstack(fill_value=0)
    ct.plot(kind="bar", ax=ax)
    ax.set_ylabel("Attempts")
    ax.set_title("Failure categories by lane")
    ax.tick_params(axis="x", rotation=45)
    ax.legend(title="Lane")
    return ax


def failure_flow_alluvial(
    df: pd.DataFrame,
    left_col: str,
    right_col: str,
    ax: Optional[matplotlib.axes.Axes] = None,
) -> matplotlib.axes.Axes:
    """Two-stage alluvial/flow diagram of counts flowing from *left_col* to *right_col*.

    Each left/right category is a stacked node; bands between them are sized by the
    number of failures taking that path. Degrades to a message when data is absent.
    """
    ax = _ax(ax)
    if df.empty or left_col not in df.columns or right_col not in df.columns:
        ax.text(0.5, 0.5, "No flow data", ha="center", va="center")
        ax.axis("off")
        return ax

    d = df[[left_col, right_col]].fillna("(none)").astype(str)
    ct = d.value_counts().reset_index(name="n")
    if ct.empty:
        ax.text(0.5, 0.5, "No flow data", ha="center", va="center")
        ax.axis("off")
        return ax

    left_tot = ct.groupby(left_col)["n"].sum().sort_values(ascending=False)
    right_tot = ct.groupby(right_col)["n"].sum().sort_values(ascending=False)
    gap = max(0.04 * left_tot.sum(), 0.3)

    def stack(tot):
        pos, y = {}, 0.0
        for k, v in tot.items():
            pos[k] = (y, y + v)
            y += v + gap
        return pos, max(y - gap, 1.0)

    lpos, lh = stack(left_tot)
    rpos, rh = stack(right_tot)
    height = max(lh, rh)
    palette = sns.color_palette("tab10", max(len(left_tot), 1))
    lcolor = {k: palette[i % 10] for i, k in enumerate(left_tot.index)}

    x0, x1, nodew = 0.0, 1.0, 0.04
    for k, (a, b) in lpos.items():
        ax.fill_betweenx([height - b, height - a], x0 - nodew, x0, color=lcolor[k])
        ax.text(x0 - nodew - 0.02, height - (a + b) / 2, f"{k} ({int(left_tot[k])})",
                ha="right", va="center", fontsize=8)
    for k, (a, b) in rpos.items():
        ax.fill_betweenx([height - b, height - a], x1, x1 + nodew, color="0.6")
        ax.text(x1 + nodew + 0.02, height - (a + b) / 2, f"{k} ({int(right_tot[k])})",
                ha="left", va="center", fontsize=8)

    loff = {k: lpos[k][0] for k in left_tot.index}
    roff = {k: rpos[k][0] for k in right_tot.index}
    for r in ct.itertuples():
        lk, rk, n = getattr(r, left_col), getattr(r, right_col), r.n
        ls, rs = loff[lk], roff[rk]
        loff[lk] += n
        roff[rk] += n
        y_l0, y_l1 = height - (ls + n), height - ls
        y_r0, y_r1 = height - (rs + n), height - rs
        ax.fill([x0, x1, x1, x0], [y_l1, y_r1, y_r0, y_l0], color=lcolor[lk], alpha=0.45, linewidth=0)

    ax.set_xlim(-0.55, 1.55)
    ax.set_ylim(-gap, height + gap)
    ax.axis("off")
    ax.set_title(f"Failure flow: {left_col} → {right_col}")
    return ax
