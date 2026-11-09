# CLI Contract: Pinned Repository Targets

## Create Manifest

```text
testmap targets create
  --input <path>
  --output <path>
  [--delimiter auto|comma|tab]
  [--rejections <path>]
```

Defaults:

- `--delimiter`: `auto`
- `--rejections`: content-addressed `<output-basename>-rejections-<hash-prefix>.csv` beside the manifest

Required source headers: `name`, `lastCommitSHA` (case-insensitive match after BOM/whitespace trim).
All other columns are ignored.

Exit behavior:

- `0`: manifest and complete rejection report written; individual rejected rows are permitted.
- nonzero: unreadable input, ambiguous/unsupported delimiter, missing required headers, no valid
  targets, invalid output path, rejection report publication failure, or manifest publication failure.

The command prints aggregate counts only: input, emitted, deduplicated, rejected. It never prints
credentials or full ignored source rows.

## Verify Manifest

```text
testmap targets verify
  --file <manifest.yaml>
  [--status-output <path>]
  [--max-concurrency <positive integer>]
```

Defaults:

- `--status-output`: `<manifest-directory>/<manifest-basename>-status.csv`
- `--max-concurrency`: bounded conservative default selected in implementation configuration

Exit behavior:

- `0`: a complete one-row-per-target report was atomically written, even when targets are
  unavailable.
- nonzero: manifest/schema failure, invalid concurrency, or inability to write a complete report.

## Experiment Input

Measured `experiment` mode interprets `RuntimeConfig.FilePaths.TargetFilePath` as a target manifest
and rejects URL-only lists. Discovery-only commands may continue to accept the documented legacy URL
list until that surface is separately retired.
