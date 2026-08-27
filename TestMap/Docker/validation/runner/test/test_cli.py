from __future__ import annotations

import argparse
import json
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

    def test_dedupe_by_name_preserves_distinct_same_name_paths(self) -> None:
        paths = [
            Path("/coverage/first/coverage.cobertura.xml"),
            Path("/coverage/second/coverage.cobertura.xml"),
            Path("/coverage/other/other.cobertura.xml"),
        ]

        self.assertEqual(
            paths,
            cli.dedupe_by_name(paths),
        )

    def test_build_trx_file_prefix_sanitizes_collector_name(self) -> None:
        prefix = cli.build_trx_file_prefix(
            Path("Example.Tests.csproj"),
            "net10.0",
            "Code Coverage;Format=Cobertura",
        )

        self.assertEqual("Example.Tests_net10.0_Code_Coverage_Format_Cobertura", prefix)

    def test_explicit_collector_order_always_includes_remaining_builtin(self) -> None:
        self.assertEqual(
            ["XPlat Code Coverage", "Code Coverage;Format=Cobertura"],
            cli.build_collector_order("XPlat Code Coverage"),
        )
        self.assertEqual(
            ["Code Coverage;Format=Cobertura", "XPlat Code Coverage"],
            cli.build_collector_order("Code Coverage;Format=Cobertura"),
        )

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
            os.utime(old_coverage, (time.time() - 10, time.time() - 10))
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
                return cli.CommandResult(return_code=1 if len(calls) == 1 else 0)

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
            self.assertEqual([1, 0], [attempt.return_code for attempt in result.collector_attempts])
            self.assertEqual("Code Coverage;Format=Cobertura", result.successful_collector)

    def test_explicit_collector_is_followed_by_remaining_builtin_after_no_artifact(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            coverage_dir = Path(temp_dir) / "coverage"
            coverage_dir.mkdir()
            paths = cli.RunnerPaths(Path(temp_dir), coverage_dir, Path(temp_dir) / "mutation", None)
            target = Path(temp_dir) / "Example.Tests.csproj"
            target.write_text("<Project />", encoding="utf-8")
            calls: list[list[str]] = []

            def fake_run_process(command, *, cwd, output_file=None, check, capture_output=False):
                calls.append(command)
                prefix = command[command.index("--logger") + 1].split("LogFilePrefix=", 1)[1]
                trx = coverage_dir / f"{prefix}.trx"
                trx.write_text("", encoding="utf-8")
                os.utime(trx, (time.time() + 1, time.time() + 1))
                if len(calls) == 2:
                    artifact = coverage_dir / "fallback" / "coverage.cobertura.xml"
                    artifact.parent.mkdir()
                    artifact.write_text("<coverage />", encoding="utf-8")
                    os.utime(artifact, (time.time() + 1, time.time() + 1))
                return cli.CommandResult(return_code=0)

            with patch.object(cli, "run_process", side_effect=fake_run_process), patch.object(
                cli, "wait_for_artifacts", return_value=None
            ):
                result = cli.run_dotnet_test_command(
                    target,
                    paths=paths,
                    framework="net10.0",
                    collector="Code Coverage;Format=Cobertura",
                )

            self.assertEqual(
                ["Code Coverage;Format=Cobertura", "XPlat Code Coverage"],
                [command[3].removeprefix("--collect:") for command in calls],
            )
            self.assertEqual("XPlat Code Coverage", result.successful_collector)

    def test_collection_sidecar_records_attempts_artifacts_and_separate_test_result(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            project_dir = Path(temp_dir)
            coverage_dir = project_dir / "coverage"
            coverage_dir.mkdir()
            artifact_path = coverage_dir / "nested" / "coverage.cobertura.xml"
            artifact_path.parent.mkdir()
            artifact_path.write_text("<coverage />", encoding="utf-8")
            artifact = cli.CoverageArtifact(
                artifact_path,
                cli.sha256_file(artifact_path),
                "tests/Example.Tests.csproj",
                "net10.0",
            )
            attempt = cli.CollectorAttempt(1, "XPlat Code Coverage", 1, (), (artifact,))
            paths = cli.RunnerPaths(project_dir, coverage_dir, project_dir / "mutation", None)

            cli.write_collection_sidecar(
                paths,
                "run-1",
                "Merged",
                "",
                1,
                "XPlat Code Coverage",
                [attempt],
                [artifact],
                [artifact],
                [],
            )

            document = json.loads((coverage_dir / "collection_run-1.json").read_text(encoding="utf-8"))
            self.assertEqual("coverage-collection-v1", document["schemaVersion"])
            self.assertEqual(1, document["testReturnCode"])
            self.assertEqual(1, document["providerAttempts"][0]["returnCode"])
            self.assertEqual(artifact.sha256, document["providerAttempts"][0]["coverageArtifacts"][0]["sha256"])
            self.assertFalse((coverage_dir / "collection_run-1.json.tmp").exists())

    def test_no_artifact_status_distinguishes_unavailable_failed_and_empty(self) -> None:
        unavailable = cli.CollectorAttempt(1, "XPlat Code Coverage", 1, (), ())
        failed = cli.CollectorAttempt(1, "XPlat Code Coverage", 1, (Path("result.trx"),), ())
        empty = cli.CollectorAttempt(1, "XPlat Code Coverage", 0, (Path("result.trx"),), ())

        self.assertEqual("ProviderUnavailable", cli.classify_no_artifact_status([unavailable]))
        self.assertEqual("CollectionFailed", cli.classify_no_artifact_status([failed]))
        self.assertEqual("NoArtifact", cli.classify_no_artifact_status([empty]))

    def test_artifact_identity_retains_cross_scope_and_reports_same_scope_duplicate(self) -> None:
        first = cli.CoverageArtifact(Path("a/coverage.cobertura.xml"), "abc", "tests/A.csproj", "net10.0")
        duplicate = cli.CoverageArtifact(Path("b/coverage.cobertura.xml"), "abc", "tests/A.csproj", "net10.0")
        cross_scope = cli.CoverageArtifact(Path("c/coverage.cobertura.xml"), "abc", "tests/B.csproj", "net10.0")
        distinct_same_name = cli.CoverageArtifact(Path("d/coverage.cobertura.xml"), "def", "tests/A.csproj", "net10.0")

        inputs, duplicates = cli.dedupe_artifacts([first, duplicate, cross_scope, distinct_same_name])

        self.assertEqual([first, cross_scope, distinct_same_name], inputs)
        self.assertEqual([duplicate], duplicates)

    def test_merge_uses_only_explicit_current_run_inputs(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            project_dir = Path(temp_dir)
            coverage_dir = project_dir / "coverage"
            coverage_dir.mkdir()
            first = coverage_dir / "one" / "coverage.cobertura.xml"
            second = coverage_dir / "two" / "coverage.cobertura.xml"
            stale = coverage_dir / "stale" / "coverage.cobertura.xml"
            for path in (first, second, stale):
                path.parent.mkdir()
                path.write_text("<coverage />", encoding="utf-8")
            paths = cli.RunnerPaths(project_dir, coverage_dir, project_dir / "mutation", None)

            def fake_run_process(command, *, cwd, output_file=None, check, capture_output=False):
                output = Path(command[command.index("--output") + 1])
                output.write_text("<coverage />", encoding="utf-8")
                return cli.CommandResult(return_code=0)

            with patch.object(cli, "run_process", side_effect=fake_run_process) as run_process:
                self.assertEqual(0, cli.merge_coverage_reports(paths, "run-1", [first, second]))

            command = run_process.call_args.args[0]
            self.assertIn(str(first), command)
            self.assertIn(str(second), command)
            self.assertNotIn(str(stale), command)
            self.assertTrue((coverage_dir / "merged_run-1.cobertura.xml").exists())

    def test_run_dotnet_stryker_project_runs_from_source_project_directory_with_test_project(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            project_dir = Path(temp_dir)
            test_project_dir = project_dir / "tests" / "Example.Tests"
            test_project_dir.mkdir(parents=True)
            test_project = test_project_dir / "Example.Tests.csproj"
            test_project.write_text("<Project />", encoding="utf-8")
            source_project_dir = project_dir / "src" / "Example"
            source_project_dir.mkdir(parents=True)
            source_project = source_project_dir / "Example.csproj"
            source_project.write_text("<Project />", encoding="utf-8")
            paths = cli.RunnerPaths(
                project_dir=project_dir,
                coverage_dir=project_dir / "coverage",
                mutation_dir=project_dir / "mutation",
                reportgenerator_executable=None,
            )
            args = argparse.Namespace(
                run_id="iteration_123",
                report_name="Example",
                project=str(source_project),
                test_project=str(test_project),
                target_framework="net9.0",
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
            self.assertIn("--test-project", command)
            self.assertEqual(
                str(test_project.resolve()),
                command[command.index("--test-project") + 1],
            )
            self.assertIn("--target-framework", command)
            self.assertEqual("net9.0", command[command.index("--target-framework") + 1])
            self.assertNotIn("--test-projects", command)
            self.assertEqual(source_project_dir, run_process.call_args.kwargs["cwd"])

    def test_run_dotnet_stryker_project_resolves_test_project_argument_to_project_reference(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            project_dir = Path(temp_dir)
            source_project_dir = project_dir / "src" / "Example"
            test_project_dir = project_dir / "tests" / "Example.Tests"
            source_project_dir.mkdir(parents=True)
            test_project_dir.mkdir(parents=True)
            source_project = source_project_dir / "Example.csproj"
            test_project = test_project_dir / "Example.Tests.csproj"
            source_project.write_text("<Project />", encoding="utf-8")
            test_project.write_text(
                """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\\..\\src\\Example\\Example.csproj" />
  </ItemGroup>
</Project>""",
                encoding="utf-8",
            )
            paths = cli.RunnerPaths(
                project_dir=project_dir,
                coverage_dir=project_dir / "coverage",
                mutation_dir=project_dir / "mutation",
                reportgenerator_executable=None,
            )
            args = argparse.Namespace(
                run_id="iteration_123",
                report_name="Example",
                project=str(test_project),
                test_project=str(test_project),
                target_framework=None,
            )

            with patch.object(cli, "get_paths", return_value=paths), patch.object(
                cli,
                "run_process",
                return_value=cli.CommandResult(return_code=0),
            ) as run_process:
                exit_code = cli.run_dotnet_stryker_project(args)

            self.assertEqual(0, exit_code)
            command = run_process.call_args.args[0]
            self.assertNotIn("--project", command)
            self.assertIn("--test-project", command)
            self.assertEqual(
                str(test_project.resolve()),
                command[command.index("--test-project") + 1],
            )
            self.assertNotIn("--target-framework", command)
            self.assertEqual(source_project_dir.resolve(), run_process.call_args.kwargs["cwd"])

    def test_normalize_project_reference_include_uses_host_separator(self) -> None:
        self.assertEqual(
            os.path.join("..", "..", "src", "Example", "Example.csproj"),
            cli.normalize_project_reference_include(r"..\..\src\Example\Example.csproj"),
        )


if __name__ == "__main__":
    unittest.main()
