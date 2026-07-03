"""File I/O helpers for reading evaluation results and artifact folders."""

from __future__ import annotations

import glob as _glob
from pathlib import Path

import pandas as pd


def glob_paths(patterns: list[str] | tuple[str, ...]) -> list[Path]:
    """Expand one or more glob patterns and return sorted unique paths."""
    paths: list[Path] = []
    for pattern in patterns:
        matches = _glob.glob(pattern, recursive=True)
        paths.extend(Path(m) for m in matches)
    seen: set[Path] = set()
    result: list[Path] = []
    for p in paths:
        if p not in seen:
            seen.add(p)
            result.append(p)
    return sorted(result)


def read_results_csvs(patterns: list[str] | tuple[str, ...]) -> pd.DataFrame:
    """Read and concatenate all experiment result CSV files matching *patterns*.

    Returns an empty DataFrame if no files are found.
    """
    paths = glob_paths(patterns)
    if not paths:
        return pd.DataFrame()
    frames: list[pd.DataFrame] = []
    for p in paths:
        try:
            df = pd.read_csv(p, low_memory=False)
            df["_source_file"] = str(p)
            frames.append(df)
        except Exception as exc:
            print(f"[warn] Could not read {p}: {exc}")
    if not frames:
        return pd.DataFrame()
    return pd.concat(frames, ignore_index=True)


def find_databases(patterns: list[str] | tuple[str, ...]) -> list[Path]:
    """Return all SQLite database paths matching *patterns*."""
    return glob_paths(patterns)


def find_artifact_dir(
    artifacts_root: str | Path | None,
    experiment_id: str | int,
    candidate_id: str | int,
    tool_id: str,
) -> Path | None:
    """Return the artifact directory for a specific tool attempt, or None if absent."""
    if artifacts_root is None:
        return None
    p = Path(artifacts_root) / str(experiment_id) / "attempts" / str(candidate_id) / tool_id
    return p if p.is_dir() else None


def read_log_excerpt(
    log_path: str | Path | None,
    max_lines: int = 50,
) -> str:
    """Return the last *max_lines* lines of a log file as a single string."""
    if log_path is None:
        return ""
    p = Path(log_path)
    if not p.exists():
        return ""
    try:
        lines = p.read_text(encoding="utf-8", errors="replace").splitlines()
        return "\n".join(lines[-max_lines:])
    except Exception:
        return ""


def read_text_artifact(
    artifact_dir: Path | None,
    filename: str,
    max_chars: int = 4000,
) -> str:
    """Read a named file from an artifact directory, truncated to *max_chars*."""
    if artifact_dir is None:
        return ""
    p = artifact_dir / filename
    if not p.exists():
        return ""
    try:
        text = p.read_text(encoding="utf-8", errors="replace")
        return text[:max_chars]
    except Exception:
        return ""


def ensure_output_dir(output_dir: str | Path) -> Path:
    """Create *output_dir* (including parents) if it does not exist and return it."""
    p = Path(output_dir)
    p.mkdir(parents=True, exist_ok=True)
    return p
