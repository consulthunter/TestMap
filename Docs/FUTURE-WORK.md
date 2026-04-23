# Future Work

This file tracks improvements that still make sense after reviewing the current implementation.

## High-value code improvements

- Reduce nullable and dead-code warnings so build output is easier to trust.
- Fix the test runtime dependency issue around `AWSSDK.BedrockRuntime.dll` so `dotnet test` is reliable.
- Replace rollback-based experiment isolation with per-attempt worktrees or another cleaner sandbox.

## Test generation quality

- Improve prompt construction with framework-specific scaffolds instead of defaulting to NUnit-style examples.
- Make repair attempts preserve more context from previous failures.
- Add clearer success criteria than pass/fail plus coverage delta alone.

## Experiment framework

- Support resumable or incremental experiment runs.
- Export richer reports, but keep CSV as the baseline format because it is easy to inspect and analyze.
- Capture per-attempt metrics in a way that is easier to analyze without rehydrating multiple tables.

## Analysis and reporting

- Add repeatable analysis scripts or notebooks for experiment output.
- Track cost, latency, and token efficiency more explicitly across providers.
- Add mutation-testing-based quality signals if the project starts using those results consistently.
