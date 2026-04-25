# How It Works

TestMap combines repository operations, Roslyn-based static analysis, local persistence, and optional AI generation.

## Core flow

For each project in the target file, TestMap can:

1. clone the repository
2. discover solution and project files
3. analyze C# code with Roslyn
4. persist code, coverage, mutation, and test metadata to SQLite through EF Core
5. build and run tests, usually through Docker-backed execution
6. run follow-up analysis or generation steps depending on the selected command

## Main commands

### `collect-tests`

Collects repository, source, build, test, and coverage information for later analysis.

### `generate-tests`

Uses the configured AI provider from `TestingConfig.GenerationConfig`, selects low-coverage methods from stored analysis data, and runs the shared test-generation pipeline to generate or repair tests.

### `experiment`

Selects candidate methods from stored coverage data, iterates providers and strategies from `ExperimentConfig`, executes generated tests, persists results, and exports CSV if `ExperimentConfig.OutputPath` is configured.

## Storage

Each project gets its own SQLite database file. That part of the design is sound and keeps runs isolated.
