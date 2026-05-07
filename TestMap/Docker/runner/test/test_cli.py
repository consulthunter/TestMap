from __future__ import annotations

import argparse
import os
import tempfile
import time
import unittest
from pathlib import Path
from unittest.mock import patch

from testmap_runner import cli


class CliUtilityTests(unittest.TestCase):
    def test_split_csv_trims_items_and_ignores_empty_segments(self) -> None:
        self.assertEqual(
            ["App.sln", "Tests.sln", "Other.sln"],
            cli.split_csv(" App.sln, , Tests.sln,,Other.sln "),
        )

    def test_find_named_file_returns_first_sorted_match(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            later = root / "z" / "Project.sln"
            earlier = root / "a" / "Project.sln"
            later.parent.mkdir()
            earlier.parent.mkdir()
            later.write_text("", encoding="utf-8")
            earlier.write_text("", encoding="utf-8")

            self.assertEqual(earlier, cli.find_named_file(root, "Project.sln"))

    def test_dedupe_by_name_keeps_first_path_for_each_file_name(self) -> None:
        paths = [
            Path("/coverage/first/coverage.cobertura.xml"),
            Path("/coverage/second/coverage.cobertura.xml"),
            Path("/coverage/other/other.cobertura.xml"),
        ]

        self.assertEqual(
            [paths[0], paths[2]],
            cli.dedupe_by_name(paths),
        )

    def test_build_trx_file_prefix_sanitizes_collector_name(self) -> None:
        prefix = cli.build_trx_file_prefix(
            Path("Example.Tests.csproj"),
            "net10.0",
            "Code Coverage;Format=Cobertura",
        )

        self.assertEqual("Example.Tests_net10.0_Code_Coverage_Format_Cobertura", prefix)

    def test_count_recent_files_only_counts_artifacts_newer_than_start_time(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            coverage_dir = Path(temp_dir)
            old_trx = coverage_dir / "Project_default_XPlat_Code_Coverage_old.trx"
            new_trx = coverage_dir / "Project_default_XPlat_Code_Coverage_new.trx"
            old_coverage = coverage_dir / "old.cobertura.xml"
            nested = coverage_dir / "nested"
            nested.mkdir()
            new_coverage = nested / "new.cobertura.xml"

            old_trx.write_text("", encoding="utf-8")
            old_coverage.write_text("", encoding="utf-8")
            started_at = time.time()
            time.sleep(0.02)
            new_trx.write_text("", encoding="utf-8")
            new_coverage.write_text("", encoding="utf-8")

            self.assertEqual(
                1,
                cli.count_recent_trx_files(
                    coverage_dir,
                    "Project_default_XPlat_Code_Coverage",
                    started_at,
                ),
            )
            self.assertEqual(1, cli.count_recent_coverage_files(coverage_dir, started_at))


class DotnetCommandTests(unittest.TestCase):
    def test_run_dotnet_passthrough_returns_one_when_no_arguments_are_provided(self) -> None:
        args = argparse.Namespace(dotnet_args=[], working_directory=None)

        self.assertEqual(1, cli.run_dotnet_passthrough(args))

    def test_run_dotnet_passthrough_invokes_dotnet_with_requested_working_directory(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            working_directory = Path(temp_dir)
            args = argparse.Namespace(
                dotnet_args=["test", "Example.sln", "--no-restore"],
                working_directory=str(working_directory),
            )

            with patch.object(cli, "run_process", return_value=cli.CommandResult(return_code=7)) as run_process:
                exit_code = cli.run_dotnet_passthrough(args)

            self.assertEqual(7, exit_code)
            run_process.assert_called_once_with(
                ["dotnet", "test", "Example.sln", "--no-restore"],
                cwd=working_directory,
                check=False,
            )

    def test_run_dotnet_test_command_falls_back_to_second_collector_when_first_has_no_coverage(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            coverage_dir = Path(temp_dir) / "coverage"
            paths = cli.RunnerPaths(
                project_dir=Path(temp_dir),
                coverage_dir=coverage_dir,
                mutation_dir=Path(temp_dir) / "mutation",
                reportgenerator_executable=None,
            )
            coverage_dir.mkdir()
            target_path = Path(temp_dir) / "Example.Tests.csproj"
            target_path.write_text("<Project />", encoding="utf-8")
            calls: list[list[str]] = []

            def fake_run_process(command, *, cwd, output_file=None, check, capture_output=False):
                calls.append(command)
                prefix = command[command.index("--logger") + 1].split("LogFilePrefix=", 1)[1]
                trx_file = coverage_dir / f"{prefix}.trx"
                trx_file.write_text("", encoding="utf-8")
                os.utime(trx_file, (time.time() + 1, time.time() + 1))
                if len(calls) == 2:
                    coverage_file = coverage_dir / "nested" / "coverage.cobertura.xml"
                    coverage_file.parent.mkdir(parents=True)
                    coverage_file.write_text("<coverage />", encoding="utf-8")
                    os.utime(coverage_file, (time.time() + 1, time.time() + 1))
                return cli.CommandResult(return_code=0)

            with patch.object(cli, "run_process", side_effect=fake_run_process), patch.object(
                cli,
                "wait_for_artifacts",
                return_value=None,
            ):
                result = cli.run_dotnet_test_command(
                    target_path,
                    paths=paths,
                    framework=None,
                    collector=None,
                )

            self.assertEqual(0, result.return_code)
            self.assertEqual(2, result.test_result_count)
            self.assertEqual(1, result.coverage_file_count)
            self.assertEqual("XPlat Code Coverage", calls[0][3].removeprefix("--collect:"))
            self.assertEqual("Code Coverage;Format=Cobertura", calls[1][3].removeprefix("--collect:"))

    def test_run_dotnet_stryker_project_runs_from_test_project_directory_without_project_args(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            project_dir = Path(temp_dir)
            test_project_dir = project_dir / "tests" / "Example.Tests"
            test_project_dir.mkdir(parents=True)
            test_project = test_project_dir / "Example.Tests.csproj"
            test_project.write_text("<Project />", encoding="utf-8")
            paths = cli.RunnerPaths(
                project_dir=project_dir,
                coverage_dir=project_dir / "coverage",
                mutation_dir=project_dir / "mutation",
                reportgenerator_executable=None,
            )
            args = argparse.Namespace(
                run_id="iteration_123",
                report_name="Example",
                test_project=str(test_project),
            )

            with patch.object(cli, "get_paths", return_value=paths), patch.object(
                cli,
                "run_process",
                return_value=cli.CommandResult(return_code=0),
            ) as run_process:
                exit_code = cli.run_dotnet_stryker_project(args)

            self.assertEqual(0, exit_code)
            run_process.assert_called_once()
            command = run_process.call_args.args[0]
            self.assertEqual(["dotnet", "stryker"], command[:2])
            self.assertNotIn("--solution", command)
            self.assertNotIn("--project", command)
            self.assertNotIn("--test-projects", command)
            self.assertEqual(test_project_dir, run_process.call_args.kwargs["cwd"])


if __name__ == "__main__":
    unittest.main()
