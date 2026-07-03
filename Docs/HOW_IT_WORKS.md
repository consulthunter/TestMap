# How It Works

TestMap is a pipeline application. Each command builds a project context, prepares a workspace, runs
one or more analysis or generation steps, and persists results to a per-project SQLite database.

## 1. Repository Ingestion

TestMap reads repository URLs from `RuntimeConfig.FilePaths.TargetFilePath`. For each repository, it
creates a local project workspace under `RuntimeConfig.FilePaths.TempDirPath` and an output/database
area under `RuntimeConfig.FilePaths.OutputDirPath`.

The workspace is treated as disposable experiment state. Generation and tool attempts are rolled back
between lanes and attempts so the next attempt starts from the expected baseline.

## 2. Project Discovery

The discovery steps locate solutions, C# projects, test projects, source files, and test files.
Framework configuration tells TestMap which attributes count as tests, such as `Fact`, `Theory`,
`Test`, or `TestMethod`.

## 3. Static Analysis

Roslyn analysis extracts source structure into a database model:

- files
- namespaces
- objects and types
- members and methods
- invocations
- source/test relationships
- context graph access paths

This gives later generation steps a grounded picture of how a test can legally reach a target method.

## 4. Evidence Collection

Depending on the command and project support, TestMap collects:

- build and test results
- coverage reports
- mutation testing reports
- code metrics
- test smells
- source/test mappings

Coverage gaps and surviving or no-coverage mutants become evidence for target selection and prompt
construction.

## 5. Candidate Selection

Candidate methods are selected from stored analysis data. Strategies can focus on existing mapped
tests, low coverage, mutation weakness, or metric-driven improvement. Candidate selection is
intentionally data-driven so experiment rows can be traced back to the same method and evidence.

## 6. Built-In LLM Generation Lane

The TestMap lane uses the configured provider and generation profile to produce tests. The recommended
path is Basic Extension:

1. Build an evidence package.
2. Resolve context graph and access path evidence.
3. Ask the model for one structured patch JSON.
4. Apply the patch deterministically to the existing test file.
5. Capture the modified file snapshot.
6. Run Roslyn validation, build/test validation, coverage, and mutation measurement.
7. Run repair attempts when configured.

Each generation attempt stores the raw patch, repair patch, modified file path, modified file
contents, modified file hash, validation result, timing, token estimate, and result classification.

## 7. Agent Tool Lane

The agent tool lane writes a task card, prompt, and evidence summary, then runs a Docker image for a
tool such as Codex, Claude, Gemini, Aider, OpenHands, mini-swe-agent, or Copilot.

The tool modifies the workspace directly. TestMap collects:

- `patch.diff`
- changed files
- git status before and after
- stdout/stderr paths
- JSONL log path when available
- token usage when the tool exposes it

Agent metadata paths such as `.testmap`, `.codex`, `.claude`, `.aider`, and `.gemini` are excluded
from changed-file counts.

## 8. Validation And Measurement

After an LLM or tool attempt, TestMap refreshes analysis where needed, links generated test members,
runs build/test validation, and measures the effect on coverage and mutation outcomes.

The key distinction:

- The LLM lane applies one structured test patch per attempt.
- Agent tools can make broader repository changes, usually adding one or more tests.

Both lanes are evaluated through the same persisted evidence and result reporting path.

## 9. Reporting

Experiment mode writes row-level results to CSV and persists richer data to SQLite. The CSV is for
analysis and comparison; the database is the source of detailed attempt, execution, mapping, and
artifact metadata.
