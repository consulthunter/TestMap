"""File I/O helpers for reading evaluation results and artifact folders."""

from __future__ import annotations

import glob as _glob
from pathlib import Path

import pandas as pd

from analysis.schema import (
    ASSERTION_RESULTS_SCHEMA_VERSION,
    ASSERTION_SIDECAR_REQUIRED_FIELDS,
    ASSERTION_SIDECAR_SCHEMA_VERSION,
    LEGACY_RESULTS_SCHEMA_VERSION,
)


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


def read_result_grains(
    patterns: list[str] | tuple[str, ...],
    *,
    expected_schema_version: str = LEGACY_RESULTS_SCHEMA_VERSION,
) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """Read one explicitly selected result schema and partition its row grains.

    When an attempt file is selected directly, its generated-test and test-result
    siblings are discovered automatically. Assertion sidecars are deliberately
    excluded from this reader. Result files must declare
    ``results_schema_version``, ``row_kind``, and a non-empty ``attempt_id``.

    Schema 3 remains available as the default legacy path. Callers making
    assertion-lineage claims must explicitly request schema 4.
    """
    if expected_schema_version not in {
        LEGACY_RESULTS_SCHEMA_VERSION,
        ASSERTION_RESULTS_SCHEMA_VERSION,
    }:
        raise ValueError(
            f"Unsupported expected results schema: {expected_schema_version}."
        )

    paths = glob_paths(patterns)
    if not paths:
        return pd.DataFrame(), pd.DataFrame(), pd.DataFrame()

    result_paths = {
        path for path in paths
        if path.suffix.lower() == ".csv" and not path.name.endswith(".assertions.csv")
    }
    discovered = set(result_paths)
    for path in list(result_paths):
        if path.name.endswith(("-generated-tests.csv", "-test-results.csv")):
            continue
        stem = path.stem
        for suffix in ("generated-tests", "test-results"):
            sibling = path.with_name(f"{stem}-{suffix}{path.suffix}")
            if sibling.exists():
                discovered.add(sibling)

    frames: list[pd.DataFrame] = []
    for path in sorted(discovered):
        df = pd.read_csv(path, low_memory=False)
        required = {"results_schema_version", "row_kind", "attempt_id"}
        missing = required - set(df.columns)
        if missing:
            raise ValueError(f"{path} is not a current result CSV; missing {sorted(missing)}.")
        versions = df["results_schema_version"].dropna().astype(str).str.strip()
        if versions.empty or not versions.eq(expected_schema_version).all():
            found = sorted(versions.unique().tolist())
            raise ValueError(
                f"{path} does not contain only results_schema_version "
                f"{expected_schema_version}; found {found}."
            )
        if df["attempt_id"].isna().any() or df["attempt_id"].astype(str).str.strip().eq("").any():
            raise ValueError(f"{path} contains rows without canonical attempt_id values.")
        df["_source_file"] = str(path)
        frames.append(df)

    combined = pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()
    known = {"attempt", "generated_test", "test_result"}
    unknown = set(combined["row_kind"].dropna().astype(str)) - known
    if unknown:
        raise ValueError(f"Unknown result row_kind values: {sorted(unknown)}.")

    def grain(kind: str) -> pd.DataFrame:
        return combined[combined["row_kind"] == kind].copy().reset_index(drop=True)

    return grain("attempt"), grain("generated_test"), grain("test_result")


def assertion_sidecar_path(results_path: str | Path) -> Path:
    """Return the schema-4 assertion sidecar path for an attempt result CSV."""
    path = Path(results_path)
    if path.suffix.lower() != ".csv":
        raise ValueError(f"Results path must end in .csv: {path}")
    return path.with_suffix(".assertions.csv")


def read_assertion_observations(
    patterns: list[str] | tuple[str, ...],
    *,
    require_files: bool = False,
) -> pd.DataFrame:
    """Read assertion schema 1.0 sidecars at their canonical raw grain."""
    selected = glob_paths(patterns)
    discovered: set[Path] = {
        path for path in selected if path.name.endswith(".assertions.csv")
    }
    for path in selected:
        if (
            path.suffix.lower() == ".csv"
            and not path.name.endswith(
                (".assertions.csv", "-generated-tests.csv", "-test-results.csv")
            )
        ):
            sibling = assertion_sidecar_path(path)
            if sibling.exists():
                discovered.add(sibling)

    if not discovered:
        if require_files:
            raise ValueError("Schema 4 assertion evidence requires an assertion sidecar.")
        return pd.DataFrame()

    frames: list[pd.DataFrame] = []
    for path in sorted(discovered):
        frame = pd.read_csv(path, low_memory=False)
        missing = ASSERTION_SIDECAR_REQUIRED_FIELDS - set(frame.columns)
        if missing:
            raise ValueError(
                f"{path} is not an assertion sidecar; missing {sorted(missing)}."
            )
        versions = frame["assertion_schema_version"].dropna().astype(str).str.strip()
        if versions.empty or not versions.eq(ASSERTION_SIDECAR_SCHEMA_VERSION).all():
            found = sorted(versions.unique().tolist())
            raise ValueError(
                f"{path} does not contain only assertion_schema_version "
                f"{ASSERTION_SIDECAR_SCHEMA_VERSION}; found {found}."
            )
        frame["_source_file"] = str(path)
        frames.append(frame)
    return pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()


def read_schema4_result_bundle(
    patterns: list[str] | tuple[str, ...],
    *,
    require_assertion_sidecar: bool = True,
) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame, pd.DataFrame]:
    """Read schema-4 result grains and their assertion schema-1 sidecar."""
    attempts, generated, test_results = read_result_grains(
        patterns,
        expected_schema_version=ASSERTION_RESULTS_SCHEMA_VERSION,
    )
    assertions = read_assertion_observations(
        patterns,
        require_files=require_assertion_sidecar and not attempts.empty,
    )
    return attempts, generated, test_results, assertions


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
