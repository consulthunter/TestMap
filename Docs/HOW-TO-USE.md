# How To Use

## 1. Generate a config file

Run from the repository root:

```sh
dotnet run --project ./TestMap/TestMap.csproj -- setup
```

This creates `./TestMap/Config/default-config.json`.

## 2. Update the config

The main config sections are:

- `RuntimeConfig`
- `TestingConfig`
- `AiProviderConfig`
- `ExperimentConfig`

Important fields:

- `RuntimeConfig.FilePaths.TargetFilePath`
- `RuntimeConfig.FilePaths.LogsDirPath`
- `RuntimeConfig.FilePaths.TempDirPath`
- `RuntimeConfig.FilePaths.OutputDirPath`
- `RuntimeConfig.Frameworks`
- `TestingConfig.GenerationConfig`
- `AiProviderConfig.OpenAi`
- `AiProviderConfig.GoogleGemini`
- `AiProviderConfig.GoogleCloud`
- `AiProviderConfig.CustomOpenAi`
- `AiProviderConfig.Ollama`
- `AiProviderConfig.Amazon`

Use the JSON config as the source of truth. The CLI does not provide experiment-specific overrides.

## 3. Run a pipeline

```sh
dotnet run --project ./TestMap/TestMap.csproj -- collect-tests --config ./TestMap/Config/default-config.json
dotnet run --project ./TestMap/TestMap.csproj -- generate-tests --config ./TestMap/Config/default-config.json
dotnet run --project ./TestMap/TestMap.csproj -- experiment --config ./TestMap/Config/default-config.json
dotnet run --project ./TestMap/TestMap.csproj -- experiment --config ./TestMap/Config/default-config.json --experiment-config ./TestMap/Config/experiment-config.json
```

Supporting commands:

```sh
dotnet run --project ./TestMap/TestMap.csproj -- check-projects --config ./TestMap/Config/default-config.json
```

## Supported AI providers

Use the enum names from code in both `TestingConfig.GenerationConfig.Provider` and `ExperimentConfig.IncludeProviders`:

- `Amazon`
- `GoogleGemini`
- `GoogleCloud`
- `CustomOpenAi`
- `Ollama`
- `OpenAi`

## Provider config shape

`AiProviderConfig` is strongly typed by provider. `GoogleGemini` and `GoogleCloud` are separate configs and can use different endpoints and credentials.

```json
{
  "AiProviderConfig": {
    "GoogleGemini": {
      "Model": "gemini-1.5-flash",
      "ApiKey": "gemini-key",
      "Endpoint": ""
    },
    "GoogleCloud": {
      "Model": "gemini-1.5-flash",
      "ProjectId": "my-gcp-project",
      "Location": "us-central1",
      "ApiKey": "vertex-key",
      "AccessToken": "",
      "TokenPath": "",
      "Endpoint": ""
    }
  }
}
```

Notes:

- `GoogleGemini` targets the Gemini API endpoint.
- `GoogleCloud` targets Vertex AI / Google Cloud endpoints.
- `GoogleCloud` can authenticate with its own `ApiKey`, `AccessToken`, `TokenPath`, or application default credentials.

## Experiment config

```json
{
  "ExperimentConfig": {
    "IncludeProviders": ["OpenAi", "GoogleGemini"],
    "PreferredProvider": "OpenAi",
    "CandidateLimit": 5,
    "Strategies": ["Pass1", "Pass5", "Repair5"],
    "MinCoverageThreshold": 0.0,
    "MaxCoverageThreshold": 0.99,
    "OutputPath": "D:\\Output\\experiment-results.csv",
    "IncludeDetailedErrors": true
  }
}
```

Notes:

- If `IncludeProviders` is empty, experiment mode uses the usable providers configured in `AiProviderConfig.ProviderConfigs`.
- `PreferredProvider` is used to order providers first when it is configured and usable.
- `CandidateLimit` must be greater than `0`, and at least one strategy must be configured.
- `IncludeDetailedErrors` controls whether error text is written to CSV.
- `experiment --config` always points to the main TestMap config. Use `--experiment-config` only if you want to split the experiment section into a separate file.
