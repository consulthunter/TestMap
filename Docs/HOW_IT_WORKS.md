# How It Works

TestMap is a pipeline application. Each command builds a project context, prepares a workspace, runs
one or more analysis or generation steps, and persists results to a per-project SQLite database.

## 1. Repository Ingestion

TestMap reads targets from `RuntimeConfig.FilePaths.TargetFilePath`. Experiments require a pinned
YAML manifest that binds each `owner/repository` to a full commit SHA; discovery commands may still
read URL lists. For each repository, it creates a local project workspace under
`RuntimeConfig.FilePaths.TempDirPath` and an output/database area under
`RuntimeConfig.FilePaths.OutputDirPath`.

The workspace is treated as disposable experiment state. Generation and tool attempts are rolled back
between lanes and attempts so the next attempt starts from the pinned commit: a forced checkout and
hard reset to that commit, then removal of untracked files and `bin`/`obj` directories. The workspace
pins `core.autocrlf=false` and `core.eol=lf`, so files are checked out exactly as committed and an
agent container that rewrites a file with LF endings does not leave a phantom modification behind.

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

Members are versioned rather than overwritten. The baseline analysis writes `Baseline` rows. When an
attempt is analyzed, unchanged members reuse their baseline row, and members the attempt added or
changed get new rows tagged with the attempt (`ToolAttempt` with its id, or `LlmAttempt`). Baseline
evidence is never rewritten by an attempt.

Source/test mappings come from a Roslyn trace of each test method: it follows calls through test
helpers (up to `MaxTestCodeDepth`) and stops at the first production member reached. Each analysis
pass traces once, after every project has been analyzed. The baseline pass traces all tests; an
attempt pass traces only the tests that attempt added or changed. Calls resolve only to the member
versions that pass observed on disk. Mappings are updated in place by
`(project, source member, test member, evidence kind)`, so their ids stay stable and links to them
from the candidate inventory and from generated tests remain valid.

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

For experiments split across multiple invocations, candidate cohorts separate sampling from
execution. A `create` run performs a seeded random ordering after eligibility and context resolution,
then persists the exact candidate snapshots and stable source identities. A `reuse` run clones those
snapshots into new run-local candidate rows. This keeps attempt ownership and reporting isolated per
run while ensuring every compared model receives the same methods in the same order.

An experiment series groups those execution runs and provides their default resume namespace.
Compatibility checks bind a cohort to its repository, commit, objective, selection settings,
candidate limit, and coverage thresholds.

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

Before either lane runs, each candidate gets one targeted coverage and mutation baseline. Both lanes
compare against that same baseline, it is stored in `candidate_baselines`, and a resumed run reuses
it rather than measuring again.

After an LLM or tool attempt, TestMap re-analyzes every project in dependency order under the
attempt's origin, traces mappings for the new or changed tests, links generated test members, runs
build/test validation, and measures the effect on coverage and mutation against the shared baseline.
The workspace is then rolled back to the pinned commit.

The key distinction:

- The LLM lane applies one structured test patch per attempt.
- Agent tools can make broader repository changes, usually adding one or more tests.

Both lanes are evaluated through the same persisted evidence and result reporting path.

## 9. Reporting

Experiment mode writes row-level results to CSV and persists richer data to SQLite. The CSV is for
analysis and comparison; the database is the source of detailed attempt, execution, mapping, and
artifact metadata.

Attempt-time mappings persist beside the baseline ones. Consumers that describe the repository's own
tests (prompt context selection, the MSR and training exports) keep only mappings whose test member
is a `Baseline` row. See `Analysis/docs/data_reference.md`.

## 10. Revision and Integrity Provenance

The manifest byte hash is the provenance root. Before SQLite initialization, TestMap validates the
origin, obtains the requested commit, checks it out detached, and verifies a clean tree. Requested
and resolved commits must be identical.

Workspace integrity is observed before extraction, baselines, experiment start, every attempt,
post-attempt analysis, after rollback, and before result publication. A moved `HEAD`, wrong origin,
invalid repository, unexpected dirty state, or failed restore blocks validation and positive-impact
classification. Cleanup always targets the persisted base commit, never whatever `HEAD` happens to
be after an agent exits.

Canonical result schema 3.0 repeats target and revision provenance on attempt, generated-test, and
test-result rows. Analysis uses `repository_family_key` for a project across revisions and
`repository_revision_key` for one exact commit.
