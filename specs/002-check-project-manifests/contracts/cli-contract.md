# CLI Contract: `check-projects`

**Contract version**: 1  
**Feature**: [Check Project Manifests](../spec.md)

## Invocation

```text
testmap check-projects --file <target-manifest.yaml>
                       [--output <bundle-pointer.yaml>]
                       [--max-concurrency <positive-integer>]
                       [--policy project-test-presence/v1]
                       [--config <testmap-config.json>]
```

Short aliases:

- `-f` for `--file`
- `-c` for `--config`

## Input Resolution

1. If `--file` is provided, it is the target input.
2. Otherwise, `--config` is required and the configured target path is used.
3. If neither resolves a path, argument validation fails.
4. The resolved path must end in `.yaml` or `.yml` and contain a supported strict target manifest.
5. A plain-text or URL-only target list is rejected even when supplied through configuration.
6. Environment discovery is anchored at the explicit config path when present, otherwise at the
   target manifest path. `GITHUB_TOKEN` is read from the resulting environment and never written.

Direct options override corresponding configured defaults. Configuration compatibility does not
relax the YAML contract.

## Output Resolution

`--output` names the stable bundle pointer. When omitted, the default is:

```text
<input-directory>/<input-stem>-project-check.yaml
```

The pointer directory contains three content-addressed immutable members:

```text
<input-stem>-project-check-report-<sha12>.yaml
<input-stem>-tests-detected-<sha12>.yaml
<input-stem>-no-tests-detected-<sha12>.yaml
```

The bundle pointer, input path, and member paths must all be distinct. The command validates every
member hash and publishes the pointer last.

## Policy

The only policy accepted by this contract is:

```text
project-test-presence/v1
```

An unknown policy fails argument validation. Future policies require their own version and do not
change existing reports.

## Console Summary

On successful publication, print:

```text
Checked: <input>; tests detected: <positive>; no tests detected: <negative>;
indeterminate: <missing>; bundle: <absolute pointer path>.
```

When indeterminate targets exist, also print a concise status breakdown and state that the screening
set is incomplete. Do not print credentials, authorization headers, or raw provider response bodies.

## Exit Codes

| Code | Meaning | Bundle behavior |
|---:|---|---|
| `0` | Every input target has a determinate classification | Complete bundle published with `classification_complete: true` |
| `1` | Invalid arguments/input, coordination failure, publication failure, or invalid output bundle | No new completed pointer is published |
| `2` | Bundle published, but one or more targets are indeterminate | Complete bundle published with `classification_complete: false` |

Per-target access and provider failures are normally represented in the report and produce exit code
2. A failure that prevents complete one-record-per-target accounting is fatal and produces exit code
1.

## Removed Canonical Behavior

- The command no longer appends to `repos_with_tests.txt`.
- The command no longer appends to `repos_without_tests.txt`.
- The command no longer checks the repository's default branch.
- Existing text files are neither read nor deleted; they are simply outside this contract.
