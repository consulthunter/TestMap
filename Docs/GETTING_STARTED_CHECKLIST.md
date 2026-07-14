# Getting Started Checklist

- [ ] Install .NET SDK 10, Git, and Docker Desktop.
- [ ] Run `dotnet run --project ./TestMap/TestMap.csproj -- setup`.
- [ ] Update `./TestMap/Config/default-config.json` file paths under `RuntimeConfig.FilePaths`.
- [ ] Confirm `RuntimeConfig.Frameworks` matches the test attributes you care about.
- [ ] Fill in the AI provider entries you plan to use under `AiProviderConfig.ProviderConfigs`.
- [ ] Run `collect-tests` before using `experiment`.
- [ ] Add an `ExperimentConfig` section if you want to use `experiment`.
- [ ] Start with a small `CandidateLimit`.

## Example

```json
{
  "ExperimentConfig": {
    "IncludeProviders": ["OpenAi"],
    "PreferredProvider": "OpenAi",
    "CandidateLimit": 3,
    "Strategies": ["Pass1"],
    "MinCoverageThreshold": 0.0,
    "MaxCoverageThreshold": 0.99,
    "OutputPath": "D:\\Projects\\TestMap\\TestMap\\Output\\experiment-results.csv",
    "IncludeDetailedErrors": true
  }
}
```
