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
# Pinned Pilot Checks

- [ ] Source rows contain valid `owner/repository` names and full commit SHAs.
- [ ] Manifest and content-addressed rejection report hashes verify.
- [ ] Verification report has exactly one row per manifest target.
- [ ] Unavailable commits and authentication/rate-limit failures are retained in scope counts.
- [ ] Temporary, output, artifact, and database paths include the resolved commit; logs use the readable date/time/repository hierarchy and retain pinned revision provenance in run metadata.
- [ ] Existing revision workspaces have the expected origin and no researcher changes.
- [ ] Target execution report has no `Pending` rows after the run.
- [ ] Schema 3.0 audit reports no commit, cohort, integrity, or cross-revision path errors.

Do not delete a lock file merely because it is old; first establish that no process owns the
workspace. A dirty workspace is evidence to inspect, not a condition to erase automatically.
