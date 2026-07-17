namespace TestMap.Services.ProjectDiscovery;

public sealed record ProjectCheckOutputPaths(string BundlePath, string Directory, string Stem);

public sealed class ProjectCheckOutputPathResolver
{
    public ProjectCheckOutputPaths Resolve(string inputPath, string? outputPath)
    {
        var input = Path.GetFullPath(inputPath);
        var bundle = Path.GetFullPath(string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(Path.GetDirectoryName(input)!, Path.GetFileNameWithoutExtension(input) + "-project-check.yaml")
            : outputPath);
        if (string.Equals(input, bundle, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Project-check output must not overwrite the input manifest.");
        var directory = Path.GetDirectoryName(bundle) ?? throw new InvalidDataException("Project-check output has no directory.");
        return new ProjectCheckOutputPaths(bundle, directory, Path.GetFileNameWithoutExtension(bundle));
    }

    public static string ContentAddressedPath(ProjectCheckOutputPaths paths, string member, string sha256) =>
        Path.Combine(paths.Directory, $"{paths.Stem}-{member}-{sha256[..12]}.yaml");
}
