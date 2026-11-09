# CLI Contract: Single Repository Target Creation

**Contract version**: 1  
**Feature**: [Single Repository Target](../spec.md)

## URL Mode Invocation

```text
testmap targets create --url <https://github.com/owner/repository[.git]>
                       [--output <target-manifest.yaml>]
                       [--resolution-output <resolution-report.yaml>]
```

Existing file mode remains:

```text
testmap targets create --input <delimited-file>
                       [--output <target-manifest.yaml>]
                       [--delimiter auto|comma|tab]
                       [--rejections <rejection-report.csv>]
```

## Input Arbitration

- Exactly one of `--url` and `--input` is required.
- Supplying both or neither returns exit code 1 before reading a file or contacting GitHub.
- `--delimiter` and `--rejections` are valid only with `--input`; using them with `--url` returns
  exit code 1 before remote access.
- `--resolution-output` is valid only with `--url`; using it with `--input` returns exit code 1.
- `--url` accepts one value and cannot be repeated.

## URL Grammar

Accepted:

```text
https://github.com/owner/repository
https://github.com/owner/repository.git
https://github.com/owner/repository/
```

Rejected before remote access:

- non-HTTPS or non-GitHub hosts;
- credentials/user information or explicit non-default ports;
- query strings or fragments;
- fewer or more than two repository path segments;
- issue, pull, commit, tree, branch, release, or other repository subpages;
- dot segments or percent-encoded separators;
- owner/repository components outside the target identity grammar.

## Output Defaults

When `--output` is omitted in URL mode:

```text
<current-directory>/<owner>-<repository>-targets.yaml
```

When `--resolution-output` is omitted, its base name is:

```text
<manifest-directory>/<manifest-stem>-resolution.yaml
```

The actual resolution record is always content-addressed:

```text
<resolution-stem>-<resolution-sha12>.yaml
```

Input/output paths and published artifacts must be distinct. A failed resolution may publish its
content-addressed resolution record but never creates or replaces the target manifest.

## Authentication

- If `GITHUB_TOKEN` is non-empty, the provider request is authenticated.
- Otherwise public resolution is attempted anonymously.
- The summary and record say only `authenticated` or `anonymous`.
- The token, token prefix, authorization header, scopes, and raw response bodies are never emitted.

## Success Summary

On exit code 0, print:

```text
Repository: <owner/repository>; branch: <default-branch>; commit: <40-char-sha>;
access: <authenticated|anonymous>; manifest: <absolute-path>;
resolution: <absolute-content-addressed-path>.
```

## Failure Summary

For a locally invalid URL, print a stable `InvalidUrl` message and no artifact path.

For a valid URL whose provider resolution fails, print:

```text
Target resolution failed: <status>; reason: <reason-kind>;
resolution: <absolute-content-addressed-path>.
```

The message is sanitized and bounded.

## Exit Codes

| Code | Meaning | Manifest behavior |
|---:|---|---|
| `0` | Resolution succeeded and schema-3 manifest was published | New completed manifest published last |
| `1` | Invalid options/URL, failed resolution, invalid provenance, or publication failure | Requested manifest absent or previous completed manifest unchanged |

## Compatibility

- File mode keeps its existing schema-1 manifest, rejection CSV, defaults, and summary.
- URL mode emits schema 3 and no rejection CSV.
- All target consumers accept schema 3 after validating its linked resolution record.
- URL mode never writes schema 2, which remains reserved for categorized project-check subsets.
