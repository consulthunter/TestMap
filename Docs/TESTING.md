# Testing

## Solution tests

The solution contains one test project: `TestMap.Tests`.

Run tests from the repository root:

```sh
dotnet test TestMap.sln
```

Build only:

```sh
dotnet build TestMap.sln
```

## Current status

- `dotnet build TestMap.sln` succeeds.
- `dotnet test TestMap.sln` currently fails because the test host cannot load `AWSSDK.BedrockRuntime.dll`.
- The solution also reports an existing `AWSSDK.Core` vulnerability warning.

## Gaps

- Experiment orchestration needs more automated coverage.
- A large number of nullable warnings still make build output noisy.
