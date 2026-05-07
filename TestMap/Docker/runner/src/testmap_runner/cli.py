from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence


@dataclass(frozen=True)
class RunnerPaths:
    project_dir: Path
    coverage_dir: Path
    mutation_dir: Path
    reportgenerator_executable: str | None


@dataclass(frozen=True)
class CommandResult:
    return_code: int
    test_result_count: int = 0
    coverage_file_count: int = 0
    stdout: str = ""
    stderr: str = ""


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="testmap-runner")
    subparsers = parser.add_subparsers(dest="command", required=True)

    main_parser = subparsers.add_parser("main", help="Run the standard TestMap container workflow.")
    add_common_run_arguments(main_parser, include_solutions=True)
    main_parser.add_argument("--include-stryker", action="store_true", help="Run mutation testing.")
    main_parser.set_defaults(func=run_main)

    build_parser = subparsers.add_parser("dotnet-build", help="Run dotnet build for the provided solutions.")
    add_common_run_arguments(build_parser, include_solutions=True)
    build_parser.set_defaults(func=run_dotnet_build)

    tests_parser = subparsers.add_parser("dotnet-tests", help="Run test execution and coverage merge.")
    add_common_run_arguments(tests_parser, include_solutions=True)
    tests_parser.add_argument("--framework", default=None, help="Optional target framework.")
    tests_parser.set_defaults(func=run_dotnet_tests)

    targeted_tests_parser = subparsers.add_parser(
        "dotnet-test-project",
        help="Run test execution and coverage merge for a single test project.",
    )
    targeted_tests_parser.add_argument("--run-id", required=True)
    targeted_tests_parser.add_argument("--project", required=True, help="Project path inside the container.")
    targeted_tests_parser.add_argument("--framework", default=None, help="Optional target framework.")
    targeted_tests_parser.add_argument(
        "--collector",
        default=None,
        help="Optional coverage collector override.",
    )
    targeted_tests_parser.set_defaults(func=run_dotnet_test_project)

    stryker_parser = subparsers.add_parser("dotnet-stryker", help="Run Stryker for the provided solutions.")
    add_common_run_arguments(stryker_parser, include_solutions=True)
    stryker_parser.set_defaults(func=run_dotnet_stryker)

    targeted_stryker_parser = subparsers.add_parser(
        "dotnet-stryker-project",
        help="Run Stryker from one test project directory.",
    )
    targeted_stryker_parser.add_argument("--run-id", required=True)
    targeted_stryker_parser.add_argument("--report-name", required=True,
                                         help="Report directory prefix inside the mutation output directory.")
    targeted_stryker_parser.add_argument("--test-project", required=True,
                                         help="Test project path inside the container.")
    targeted_stryker_parser.set_defaults(func=run_dotnet_stryker_project)

    dotnet_parser = subparsers.add_parser("dotnet", help="Run an arbitrary dotnet command in the mounted project.")
    dotnet_parser.add_argument("dotnet_args", nargs=argparse.REMAINDER, help="Arguments passed to dotnet.")
    dotnet_parser.add_argument(
        "--working-directory",
        default=None,
        help="Optional working directory. Defaults to the mounted project root.",
    )
    dotnet_parser.set_defaults(func=run_dotnet_passthrough)

    return parser


def add_common_run_arguments(parser: argparse.ArgumentParser, *, include_solutions: bool) -> None:
    parser.add_argument("--run-id", required=True)
    if include_solutions:
        parser.add_argument("--solutions", required=True, help="Comma-separated solution names.")


def get_paths() -> RunnerPaths:
    if os.name == "nt":
        project_dir = Path(r"C:\app\project")
    else:
        project_dir = Path("/app/project")

    reportgenerator_executable = shutil.which("reportgenerator")

    return RunnerPaths(
        project_dir=project_dir,
        coverage_dir=project_dir / "coverage",
        mutation_dir=project_dir / "mutation",
        reportgenerator_executable=reportgenerator_executable,
    )


def run_main(args: argparse.Namespace) -> int:
    print_header(args.run_id, args.solutions)
    failures: list[str] = []

    build_exit_code = run_step("Build", lambda: run_dotnet_build(args), failures)
    if build_exit_code == 0:
        run_step("Unit Tests + Coverage", lambda: run_dotnet_tests(args), failures)
        if args.include_stryker:
            run_step("Mutation Testing (Stryker)", lambda: run_dotnet_stryker(args), failures)
    else:
        print("Skipping Unit Tests + Coverage because the build step failed.")
        if args.include_stryker:
            print("Skipping Mutation Testing (Stryker) because the build step failed.")
    print("==============================")
    print("           Summary")
    print("==============================")
    if not failures:
        print("All analysis steps completed successfully.")
    else:
        print("The following components failed:")
        for failure in failures:
            print(f" - {failure}")
    print("Done.")
    return 0 if not failures else 1


def run_step(name: str, action, failures: list[str]) -> int:
    print("--------------------------------")
    print(f" Running: {name}")
    print("--------------------------------")
    try:
        exit_code = action()
    except Exception as ex:  # pragma: no cover - defensive container logging
        print(f"[FAIL] {name} failed: {ex}", file=sys.stderr)
        failures.append(name)
        print()
        return 1

    if exit_code == 0:
        print(f"[OK] {name} completed successfully")
    else:
        print(f"[FAIL] {name} failed")
        failures.append(name)
    print()
    return exit_code


def run_dotnet_tests(args: argparse.Namespace) -> int:
    paths = get_paths()
    ensure_directory(paths.coverage_dir)
    ensure_directory(paths.project_dir)

    solution_names = split_csv(args.solutions)
    overall_exit_code = 0
    produced_any_test_results = False
    produced_any_coverage_files = False

    for solution_name in solution_names:
        solution_path = find_named_file(paths.project_dir, solution_name)
        if solution_path is None:
            print(f"Solution not found in container: {solution_name}")
            overall_exit_code = overall_exit_code or 1
            continue

        print(f"Processing solution: {solution_path}")

        result = run_dotnet_test_command(
            solution_path,
            paths=paths,
            framework=args.framework,
            collector=None,
        )
        if result.test_result_count == 0:
            print(f"No TRX test results were produced for solution: {solution_path}")
            overall_exit_code = overall_exit_code or result.return_code or 1
            continue
        if result.coverage_file_count == 0:
            print(f"No coverage files were produced for solution: {solution_path}")
            overall_exit_code = overall_exit_code or result.return_code or 1
            continue

        produced_any_test_results = True
        produced_any_coverage_files = True

        if result.return_code != 0:
            print(
                f"Testing reported failures for solution: {solution_path}. "
                "Continuing so produced coverage can still be merged."
            )
            overall_exit_code = overall_exit_code or result.return_code

    if not produced_any_test_results:
        print("No TRX test results were produced for any specified solution.")
        return overall_exit_code or 1

    if not produced_any_coverage_files:
        print("No coverage files were produced for any specified solution.")
        return overall_exit_code or 1

    merge_exit_code = merge_coverage_reports(paths, args.run_id)
    if merge_exit_code != 0:
        return merge_exit_code

    print("All specified solutions processed.")
    return overall_exit_code


def run_dotnet_test_project(args: argparse.Namespace) -> int:
    paths = get_paths()
    ensure_directory(paths.coverage_dir)
    ensure_directory(paths.project_dir)

    project_path = Path(args.project)
    if not project_path.is_absolute():
        project_path = paths.project_dir / project_path

    if not project_path.exists():
        print(f"Project not found in container: {project_path}")
        return 1

    print(f"Processing test project: {project_path}")
    result = run_dotnet_test_command(
        project_path,
        paths=paths,
        framework=args.framework,
        collector=args.collector,
    )
    if result.test_result_count == 0:
        print(f"No TRX test results were produced for project: {project_path}")
        return result.return_code or 1
    if result.coverage_file_count == 0:
        print(f"No coverage files were produced for project: {project_path}")
        return result.return_code or 1

    if result.return_code != 0:
        print(
            f"Testing reported failures for project: {project_path}. "
            "Continuing so produced coverage can still be merged."
        )

    merge_exit_code = merge_coverage_reports(paths, args.run_id)
    if merge_exit_code != 0:
        return merge_exit_code

    print("Targeted test project processed.")
    return result.return_code


def run_dotnet_build(args: argparse.Namespace) -> int:
    paths = get_paths()
    ensure_directory(paths.project_dir)

    overall_exit_code = 0
    for solution_name in split_csv(args.solutions):
        solution_path = find_named_file(paths.project_dir, solution_name)
        if solution_path is None:
            print(f"Solution not found in container: {solution_name}")
            overall_exit_code = 1
            continue

        print(f"Building solution: {solution_path}")
        result = run_process(
            [
                "dotnet",
                "build",
                str(solution_path),
                "--nologo",
            ],
            cwd=paths.project_dir,
            check=False,
        )

        if result.return_code != 0:
            print(f"Build failed for solution: {solution_path}")
            overall_exit_code = result.return_code

    return overall_exit_code


def run_dotnet_stryker(args: argparse.Namespace) -> int:
    paths = get_paths()
    ensure_directory(paths.mutation_dir)
    ensure_directory(paths.project_dir)

    print("=== Running Mutation Tests (dotnet-stryker) ===")
    overall_exit_code = 0
    for solution_name in split_csv(args.solutions):
        solution_path = find_named_file(paths.project_dir, solution_name)
        if solution_path is None:
            print(f"Solution not found: {solution_name}")
            overall_exit_code = 1
            continue

        output_dir = paths.mutation_dir / f"{solution_path.stem}_{args.run_id}"
        ensure_directory(output_dir)
        print(f"Running Stryker for solution: {solution_path}")
        result = run_process(
            [
                "dotnet",
                "stryker",
                "--solution",
                str(solution_path),
                "-r",
                "html",
                "-r",
                "markdown",
                "-r",
                "json",
                "--output",
                str(output_dir),
            ],
            cwd=paths.project_dir,
            check=False,
        )
        if result.return_code == 0:
            print(f"Reports saved in: {output_dir}")
        else:
            print(f"Stryker failed for: {solution_path}")
            overall_exit_code = result.return_code

    print("=== Mutation Testing Complete ===")
    return overall_exit_code


def run_dotnet_stryker_project(args: argparse.Namespace) -> int:
    paths = get_paths()
    ensure_directory(paths.mutation_dir)
    ensure_directory(paths.project_dir)

    test_project_path = Path(args.test_project)
    if not test_project_path.is_absolute():
        test_project_path = paths.project_dir / test_project_path

    if not test_project_path.exists():
        print(f"Test project not found in container: {test_project_path}")
        return 1

    test_project_dir = test_project_path.parent
    output_dir = paths.mutation_dir / f"{args.report_name}_{args.run_id}"
    ensure_directory(output_dir)

    print("=== Running Targeted Mutation Tests (dotnet-stryker) ===")
    print(f"Test project: {test_project_path}")

    result = run_process(
        [
            "dotnet",
            "stryker",
            "-r",
            "html",
            "-r",
            "markdown",
            "-r",
            "json",
            "--output",
            str(output_dir),
        ],
        cwd=test_project_dir,
        check=False,
    )

    if result.return_code == 0:
        print(f"Reports saved in: {output_dir}")
    else:
        print(f"Stryker failed for test project: {test_project_path}")

    print("=== Targeted Mutation Testing Complete ===")
    return result.return_code


def run_dotnet_passthrough(args: argparse.Namespace) -> int:
    if not args.dotnet_args:
        print("No dotnet arguments provided.", file=sys.stderr)
        return 1

    paths = get_paths()
    working_directory = Path(args.working_directory) if args.working_directory else paths.project_dir
    ensure_directory(working_directory)

    command = ["dotnet", *args.dotnet_args]
    return run_process(command, cwd=working_directory, check=False).return_code


def run_dotnet_test_command(
        target_path: Path,
        *,
        paths: RunnerPaths,
        framework: str | None,
        collector: str | None,
) -> CommandResult:
    collectors = [collector] if collector else [
        "XPlat Code Coverage",
        "Code Coverage;Format=Cobertura",
    ]

    overall_trx_count = 0
    overall_coverage_count = 0
    last_return_code = 0

    for index, collector in enumerate(dict.fromkeys(collectors)):
        started_at = time.time()
        trx_file_prefix = build_trx_file_prefix(target_path, framework, collector)

        command = [
            "dotnet",
            "test",
            str(target_path),
            f"--collect:{collector}",
            "--logger",
            f"trx;LogFilePrefix={trx_file_prefix}",
            "--results-directory",
            str(paths.coverage_dir),
        ]

        if framework:
            command.extend(["--framework", framework])

        print(f"Running dotnet test with collector: {collector}")
        result = run_process(command, cwd=paths.project_dir, check=False, capture_output=True)
        last_return_code = result.return_code

        if result.stdout:
            print(result.stdout, end="" if result.stdout.endswith("\n") else "\n")
        if result.stderr:
            print(result.stderr, file=sys.stderr, end="" if result.stderr.endswith("\n") else "\n")

        wait_for_artifacts(paths.coverage_dir, trx_file_prefix, started_at)

        trx_file_count = count_recent_trx_files(paths.coverage_dir, trx_file_prefix, started_at)
        coverage_file_count = count_recent_coverage_files(paths.coverage_dir, started_at)

        overall_trx_count += trx_file_count
        overall_coverage_count += coverage_file_count

        print(
            f"Collector '{collector}' produced {trx_file_count} TRX file(s) "
            f"and {coverage_file_count} coverage file(s)."
        )

        if result.return_code != 0:
            return CommandResult(
                return_code=result.return_code,
                test_result_count=overall_trx_count,
                coverage_file_count=overall_coverage_count,
            )

        if coverage_file_count > 0:
            break

        if index < len(collectors) - 1:
            print(f"No coverage files produced with collector '{collector}'. Trying fallback collector.")

    return CommandResult(
        return_code=last_return_code,
        test_result_count=overall_trx_count,
        coverage_file_count=overall_coverage_count,
    )


def merge_coverage_reports(paths: RunnerPaths, run_id: str) -> int:
    coverage_files = dedupe_by_name(paths.coverage_dir.rglob("*.cobertura.xml"))
    if not coverage_files:
        print("No coverage files found to merge.")
        return 0

    merged_raw = paths.coverage_dir / f"merged_{run_id}_raw.cobertura.xml"
    merged_normalized = paths.coverage_dir / f"merged_{run_id}.cobertura.xml"
    report_dir = paths.coverage_dir / f"report_{run_id}"
    ensure_directory(report_dir)

    merge_command = [
        "dotnet-coverage",
        "merge",
        *[str(path) for path in coverage_files],
        "--output",
        str(merged_raw),
        "--output-format",
        "cobertura",
    ]
    merge_result = run_process(merge_command, cwd=paths.project_dir, check=False)
    if merge_result.return_code != 0:
        return merge_result.return_code

    print(f"Merged raw coverage saved to: {merged_raw}")

    if paths.reportgenerator_executable:
        report_result = run_process(
            [
                paths.reportgenerator_executable,
                f"-reports:{merged_raw}",
                f"-targetdir:{report_dir}",
                "-reporttypes:Cobertura",
                "-verbosity:Verbose",
            ],
            cwd=paths.project_dir,
            check=False,
        )
        if report_result.return_code == 0:
            generated_file = report_dir / "Cobertura.xml"
            if generated_file.exists():
                shutil.copyfile(generated_file, merged_normalized)
                print(f"Normalized coverage saved to: {merged_normalized}")
        else:
            print("ReportGenerator failed; keeping only merged raw coverage.")

    return 0


def run_process(
        command: Sequence[str],
        *,
        cwd: Path,
        output_file: Path | None = None,
        check: bool,
        capture_output: bool = False,
) -> CommandResult:
    if output_file is None:
        completed = subprocess.run(
            command,
            cwd=cwd,
            check=False,
            capture_output=capture_output,
            text=capture_output,
        )
    else:
        with output_file.open("w", encoding="utf-8", newline="") as handle:
            completed = subprocess.run(command, cwd=cwd, check=False, stdout=handle, stderr=subprocess.STDOUT)

    if check and completed.returncode != 0:
        raise subprocess.CalledProcessError(completed.returncode, command)

    return CommandResult(
        return_code=completed.returncode,
        stdout=completed.stdout or "",
        stderr=completed.stderr or "",
    )


def print_header(run_id: str, solutions: str) -> None:
    print("==============================")
    print("   TestMap Execution Runner")
    print("==============================")
    print(f"Run ID: {run_id}")
    print(f"Solutions: {solutions}")
    print()


def split_csv(value: str) -> list[str]:
    return [part.strip() for part in value.split(",") if part.strip()]


def find_named_file(root: Path, file_name: str) -> Path | None:
    matches = sorted(root.rglob(file_name))
    return matches[0] if matches else None


def dedupe_by_name(paths: Sequence[Path] | list[Path] | object) -> list[Path]:
    unique: dict[str, Path] = {}
    for path in paths:
        path = Path(path)
        if path.name not in unique:
            unique[path.name] = path
        else:
            print(f"Skipping duplicate coverage file: {path}")
    return list(unique.values())


def ensure_directory(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)


def build_trx_file_prefix(target_path: Path, framework: str | None, collector: str) -> str:
    framework_segment = framework or "default"
    collector_segment = "".join(ch if ch.isalnum() else "_" for ch in collector).strip("_")
    return f"{target_path.stem}_{framework_segment}_{collector_segment}"


def wait_for_artifacts(coverage_dir: Path, trx_file_prefix: str, started_at: float) -> None:
    for _ in range(10):
        if count_recent_trx_files(coverage_dir, trx_file_prefix, started_at) > 0:
            return
        if count_recent_coverage_files(coverage_dir, started_at) > 0:
            return
        time.sleep(0.2)


def count_recent_trx_files(coverage_dir: Path, trx_file_prefix: str, started_at: float) -> int:
    return sum(
        1
        for path in coverage_dir.glob(f"{trx_file_prefix}*.trx")
        if path.exists() and path.stat().st_mtime >= started_at
    )


def count_recent_coverage_files(coverage_dir: Path, started_at: float) -> int:
    return sum(
        1
        for path in coverage_dir.rglob("*.cobertura.xml")
        if path.exists() and path.stat().st_mtime >= started_at
    )
