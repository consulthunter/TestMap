namespace TestMap.Services.AgentTools;

public sealed record AgentToolLogPaths(
    string StdOutLogPath,
    string StdErrLogPath,
    string JsonlLogPath);

public static class AgentToolLogPathResolver
{
    /// <summary>
    /// Resolves the log paths for an attempt. Takes the tool family (ExperimentToolConfig.Family),
    /// not the entry id: the runner scripts name their artifacts after the family, so a
    /// provider-suffixed entry such as copilot-custom still writes copilot.stderr.log.
    /// </summary>
    public static AgentToolLogPaths Resolve(
        string artifactPath,
        string toolFamily,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) || string.IsNullOrWhiteSpace(toolFamily))
            return new AgentToolLogPaths(string.Empty, string.Empty, string.Empty);

        var normalizedFamily = toolFamily.Trim().ToLowerInvariant();
        var stderrPath = Path.Combine(artifactPath, $"{normalizedFamily}.stderr.log");

        return normalizedFamily switch
        {
            "codex" or "claude" or "copilot" => JsonlStdOut(artifactPath, normalizedFamily, stderrPath),
            "gemini" => GeminiPaths(artifactPath, stderrPath, environment),
            "openhands" => new AgentToolLogPaths(
                Path.Combine(artifactPath, "openhands.stdout.log"),
                stderrPath,
                Path.Combine(artifactPath, "openhands.events.jsonl")),
            _ => new AgentToolLogPaths(
                Path.Combine(artifactPath, $"{normalizedFamily}.stdout.log"),
                stderrPath,
                string.Empty)
        };
    }

    private static AgentToolLogPaths JsonlStdOut(
        string artifactPath,
        string toolFamily,
        string stderrPath)
    {
        var jsonlPath = Path.Combine(artifactPath, $"{toolFamily}.events.jsonl");
        return new AgentToolLogPaths(jsonlPath, stderrPath, jsonlPath);
    }

    private static AgentToolLogPaths GeminiPaths(
        string artifactPath,
        string stderrPath,
        IReadOnlyDictionary<string, string>? environment)
    {
        var outputFormat = environment?
            .FirstOrDefault(x => x.Key.Equals("GEMINI_OUTPUT_FORMAT", StringComparison.OrdinalIgnoreCase))
            .Value;
        return outputFormat?.Trim().ToLowerInvariant() switch
        {
            null or "" or "stream-json" => JsonlStdOut(artifactPath, "gemini", stderrPath),
            "json" => new AgentToolLogPaths(
                Path.Combine(artifactPath, "gemini.json"),
                stderrPath,
                Path.Combine(artifactPath, "gemini.json")),
            _ => new AgentToolLogPaths(
                Path.Combine(artifactPath, "gemini.stdout.log"),
                stderrPath,
                string.Empty)
        };
    }
}
