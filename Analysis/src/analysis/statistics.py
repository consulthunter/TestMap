"""Statistical helpers for evaluation analysis.

Recommended tests from the analysis plan:
- Binary success, paired:    McNemar's test
- Binary success, unpaired:  Fisher's exact / chi-square
- Continuous deltas, paired: Wilcoxon signed-rank
- Continuous deltas, unpaired: Mann-Whitney U
- Ratio metrics:             bootstrap confidence intervals
- Repository-weighted:       paired Wilcoxon + repository-blocked bootstrap
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

import numpy as np
import pandas as pd
from scipy import stats


@dataclass
class TestResult:
    statistic: float
    p_value: float
    n: int
    note: str = ""


@dataclass
class BootstrapCI:
    estimate: float
    lower: float
    upper: float
    confidence: float
    n_resamples: int


# ---------------------------------------------------------------------------
# Binary outcome tests
# ---------------------------------------------------------------------------

def mcnemar_test(
    a_success: pd.Series,
    b_success: pd.Series,
) -> TestResult:
    """McNemar's test for paired binary outcomes.

    *a_success* and *b_success* must be boolean Series with matching indices.
    """
    a = a_success.astype(bool)
    b = b_success.astype(bool)
    n01 = int((~a & b).sum())   # A failed, B succeeded
    n10 = int((a & ~b).sum())   # A succeeded, B failed
    table = np.array([[0, n01], [n10, 0]])
    result = stats.contingency.mcnemar(table, exact=(n01 + n10 < 25))
    return TestResult(
        statistic=float(result.statistic),
        p_value=float(result.pvalue),
        n=len(a),
        note=f"discordant pairs: n01={n01}, n10={n10}",
    )


def fisher_exact_test(
    a_success: pd.Series,
    b_success: pd.Series,
) -> TestResult:
    """Fisher's exact test for unpaired binary outcomes."""
    a = a_success.astype(bool)
    b = b_success.astype(bool)
    n_a_yes = int(a.sum())
    n_a_no = len(a) - n_a_yes
    n_b_yes = int(b.sum())
    n_b_no = len(b) - n_b_yes
    table = [[n_a_yes, n_a_no], [n_b_yes, n_b_no]]
    odds_ratio, p_value = stats.fisher_exact(table)
    return TestResult(
        statistic=float(odds_ratio),
        p_value=float(p_value),
        n=len(a) + len(b),
    )


# ---------------------------------------------------------------------------
# Continuous delta tests
# ---------------------------------------------------------------------------

def wilcoxon_signed_rank(
    x: pd.Series,
    y: pd.Series,
    zero_method: str = "wilcox",
) -> TestResult:
    """Wilcoxon signed-rank test for paired continuous deltas.

    *x* and *y* must be aligned Series. NaN pairs are dropped.
    """
    df = pd.DataFrame({"x": x, "y": y}).dropna()
    if len(df) < 2:
        return TestResult(statistic=float("nan"), p_value=float("nan"), n=len(df),
                          note="too few observations")
    result = stats.wilcoxon(df["x"], df["y"], zero_method=zero_method)
    return TestResult(statistic=float(result.statistic), p_value=float(result.pvalue), n=len(df))


def mann_whitney_u(
    x: pd.Series,
    y: pd.Series,
    alternative: str = "two-sided",
) -> TestResult:
    """Mann-Whitney U test for unpaired continuous distributions."""
    xc = x.dropna().to_numpy()
    yc = y.dropna().to_numpy()
    if len(xc) < 1 or len(yc) < 1:
        return TestResult(statistic=float("nan"), p_value=float("nan"), n=len(xc) + len(yc),
                          note="too few observations")
    result = stats.mannwhitneyu(xc, yc, alternative=alternative)
    return TestResult(statistic=float(result.statistic), p_value=float(result.pvalue),
                      n=len(xc) + len(yc))


# ---------------------------------------------------------------------------
# Bootstrap confidence intervals
# ---------------------------------------------------------------------------

def bootstrap_ci(
    data: np.ndarray | pd.Series,
    stat_fn: Callable[[np.ndarray], float] = np.mean,
    n_resamples: int = 9999,
    confidence: float = 0.95,
    random_state: int = 42,
) -> BootstrapCI:
    """Bootstrap confidence interval for a statistic of *data*."""
    arr = np.asarray(data.dropna() if isinstance(data, pd.Series) else data)
    estimate = float(stat_fn(arr))
    rng = np.random.default_rng(random_state)
    boot_stats = np.array([
        stat_fn(rng.choice(arr, size=len(arr), replace=True))
        for _ in range(n_resamples)
    ])
    alpha = 1.0 - confidence
    lower = float(np.percentile(boot_stats, 100 * alpha / 2))
    upper = float(np.percentile(boot_stats, 100 * (1 - alpha / 2)))
    return BootstrapCI(estimate=estimate, lower=lower, upper=upper,
                       confidence=confidence, n_resamples=n_resamples)


def repository_blocked_bootstrap(
    data: pd.DataFrame,
    stat_fn: Callable[[pd.DataFrame], float],
    repo_col: str = "repository_key",
    n_resamples: int = 9999,
    confidence: float = 0.95,
    random_state: int = 42,
) -> BootstrapCI:
    """Bootstrap CI that resamples whole repositories to block within-repo correlation."""
    repos = data[repo_col].unique()
    estimate = float(stat_fn(data))
    rng = np.random.default_rng(random_state)
    boot_stats: list[float] = []
    for _ in range(n_resamples):
        sampled_repos = rng.choice(repos, size=len(repos), replace=True)
        frames = [data[data[repo_col] == r] for r in sampled_repos]
        boot_df = pd.concat(frames, ignore_index=True)
        boot_stats.append(float(stat_fn(boot_df)))
    arr = np.array(boot_stats)
    alpha = 1.0 - confidence
    lower = float(np.percentile(arr, 100 * alpha / 2))
    upper = float(np.percentile(arr, 100 * (1 - alpha / 2)))
    return BootstrapCI(estimate=estimate, lower=lower, upper=upper,
                       confidence=confidence, n_resamples=n_resamples)
