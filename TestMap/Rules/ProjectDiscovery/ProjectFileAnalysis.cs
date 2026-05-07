namespace TestMap.Rules.ProjectDiscovery;

internal sealed record ProjectFileAnalysis(
    bool ParseSucceeded,
    Dictionary<string, string> Properties,
    List<string> PackageReferences)
{
    public static ProjectFileAnalysis Empty { get; } =
        new(false, new Dictionary<string, string>(), new List<string>());

    public string GetProperty(string name)
    {
        return Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
