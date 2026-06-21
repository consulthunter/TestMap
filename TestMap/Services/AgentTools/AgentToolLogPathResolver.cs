namespace TestMap.Services.AgentTools;

public sealed record AgentToolLogPaths(
    string StdOutLogPath,
    string StdErrLogPath,
    string JsonlLogPath);

public static class AgentToolLogPathResolver
{
    public static AgentToolLogPaths Resolve(
        string artifactPath,
        string toolId,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        if (string.IsNullOrWhiteSpace(artifactPath) || string.IsNullOrWhiteSpace(toolId))
            return new AgentToolLogPaths(string.Empty, string.Empty, string.Empty);

        var normalizedToolId = toolId.Trim().ToLowerInvariant();
        var stderrPath = Path.Combine(artifactPath, $"{normalizedToolId}.stderr.log");

        return normalizedToolId switch
        {
            "codex" or "claude" => JsonlStdOut(artifactPath, normalizedToolId, stderrPath),
            "gemini" => GeminiPaths(artifactPath, stderrPath, environment),
            "openhands" => new AgentToolLogPaths(
                Path.Combine(artifactPath, "openhands.stdout.log"),
                stderrPath,
                Path.Combine(artifactPath, "openhands.events.jsonl")),
            _ => new AgentToolLogPaths(
                Path.Combine(artifactPath, $"{normalizedToolId}.stdout.log"),
                stderrPath,
                string.Empty)
        };
    }

    private static AgentToolLogPaths JsonlStdOut(
        string artifactPath,
        string toolId,
        string stderrPath)
    {
        var jsonlPath = Path.Combine(artifactPath, $"{toolId}.events.jsonl");
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
            "stream-json" => JsonlStdOut(artifactPath, "gemini", stderrPath),
            "json" => new AgentToolLogPaths(
                Path.Combine(artifactPath, "gemini.json"),
                stderrPath,
                string.Empty),
            _ => new AgentToolLogPaths(
                Path.Combine(artifactPath, "gemini.stdout.log"),
                stderrPath,
                string.Empty)
        };
    }
}
