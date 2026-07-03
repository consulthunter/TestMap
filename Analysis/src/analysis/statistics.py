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
    effect_size: float = float("nan")
    effect_size_label: str = ""
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
    Drops rows where either value is NA before testing.

    Effect size: odds ratio = n10 / n01 (how many more times A won than B among
    discordant pairs). Values > 1 favour A, < 1 favour B.
    """
    valid = pd.DataFrame({"a": a_success, "b": b_success}).dropna()
    a = valid["a"].astype(bool)
    b = valid["b"].astype(bool)
    n01 = int((~a & b).sum())   # A failed, B succeeded
    n10 = int((a & ~b).sum())   # A succeeded, B failed
    discordant = n01 + n10
    if discordant == 0:
        return TestResult(
            statistic=0.0,
            p_value=1.0,
            n=len(a),
            effect_size=float("nan"),
            effect_size_label="odds_ratio",
            note="no discordant pairs",
        )
    # Exact two-sided McNemar test under H0: n01 and n10 are equally likely.
    p_value = stats.binomtest(min(n01, n10), discordant, 0.5).pvalue
    statistic = float((abs(n01 - n10) - 1) ** 2 / discordant)
    odds_ratio = float(n10) / float(n01) if n01 > 0 else float("inf")
    return TestResult(
        statistic=statistic,
        p_value=float(p_value),
        n=len(a),
        effect_size=odds_ratio,
        effect_size_label="odds_ratio(a_wins/b_wins)",
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

    Effect size: matched-pairs rank-biserial correlation r in [-1, 1].
    r > 0 means x tends to exceed y; r < 0 means y tends to exceed x.
    Computed as (W+ - W-) / (n*(n+1)/2) from the raw rank sums.
    """
    df = pd.DataFrame({"x": x, "y": y}).dropna()
    if len(df) < 2:
        return TestResult(statistic=float("nan"), p_value=float("nan"), n=len(df),
                          effect_size=float("nan"), effect_size_label="rank_biserial_r",
                          note="too few observations")
    result = stats.wilcoxon(df["x"], df["y"], zero_method=zero_method)
    diff = (df["x"] - df["y"]).values
    nonzero = diff[diff != 0]
    if len(nonzero) == 0:
        r_rb = 0.0
    else:
        ranked = stats.rankdata(np.abs(nonzero))
        w_plus = float(np.sum(ranked[nonzero > 0]))
        w_minus = float(np.sum(ranked[nonzero < 0]))
        total = len(nonzero) * (len(nonzero) + 1) / 2
        r_rb = float((w_plus - w_minus) / total)
    return TestResult(
        statistic=float(result.statistic),
        p_value=float(result.pvalue),
        n=len(df),
        effect_size=r_rb,
        effect_size_label="rank_biserial_r",
    )


def mann_whitney_u(
    x: pd.Series,
    y: pd.Series,
    alternative: str = "two-sided",
) -> TestResult:
    """Mann-Whitney U test for unpaired continuous distributions.

    Effect size: Cliff's delta in [-1, 1].
    delta > 0 means x tends to exceed y; delta < 0 means y tends to exceed x.
    Computed as 2U/(n1*n2) - 1 from the U statistic for x vs y.
    """
    xc = x.dropna().to_numpy()
    yc = y.dropna().to_numpy()
    n1, n2 = len(xc), len(yc)
    if n1 < 1 or n2 < 1:
        return TestResult(statistic=float("nan"), p_value=float("nan"), n=n1 + n2,
                          effect_size=float("nan"), effect_size_label="cliffs_delta",
                          note="too few observations")
    result = stats.mannwhitneyu(xc, yc, alternative=alternative)
    cliffs_delta = float(2 * result.statistic / (n1 * n2) - 1)
    return TestResult(
        statistic=float(result.statistic),
        p_value=float(result.pvalue),
        n=n1 + n2,
        effect_size=cliffs_delta,
        effect_size_label="cliffs_delta",
    )


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
    if len(arr) == 0:
        return BootstrapCI(float("nan"), float("nan"), float("nan"), confidence, n_resamples)
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


def bonferroni_correct(
    p_values: list[float],
    alpha: float = 0.05,
) -> dict:
    """Bonferroni correction for a family of k hypothesis tests.

    Divides the family-wise alpha by the number of tests k so that the
    probability of at least one false positive across the family stays at
    *alpha*. Returns the corrected threshold and a rejection mask.

    With small pilot data almost nothing will be rejected — that is the correct
    and honest result, not a bug.
    """
    k = len(p_values)
    if k == 0:
        return {"alpha": alpha, "k": 0, "alpha_corrected": alpha, "p_values": [], "reject": []}
    alpha_corrected = alpha / k
    reject = [float(p) <= alpha_corrected for p in p_values]
    return {
        "alpha": alpha,
        "k": k,
        "alpha_corrected": alpha_corrected,
        "p_values": [float(p) for p in p_values],
        "reject": reject,
    }


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
