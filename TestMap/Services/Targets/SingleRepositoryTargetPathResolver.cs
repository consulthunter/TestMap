using TestMap.Models.Targets;

namespace TestMap.Services.Targets;

public sealed record SingleRepositoryTargetPaths(
    string ManifestPath,
    string ResolutionBasePath);

public sealed class SingleRepositoryTargetPathResolver
{
    public SingleRepositoryTargetPaths Resolve(
        SingleRepositoryRequest request,
        string? outputPath,
        string? resolutionOutputPath)
    {
        var defaultStem = request.Repository.Replace('/', '-');
        var manifest = Path.GetFullPath(string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), defaultStem + "-targets.yaml")
            : outputPath);
        var manifestDirectory = Path.GetDirectoryName(manifest)
            ?? throw new InvalidDataException("Target manifest output has no directory.");
        var resolution = Path.GetFullPath(string.IsNullOrWhiteSpace(resolutionOutputPath)
            ? Path.Combine(manifestDirectory, Path.GetFileNameWithoutExtension(manifest) + "-resolution.yaml")
            : resolutionOutputPath);
        if (string.Equals(manifest, resolution, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifest and resolution output paths must be distinct.");
        return new SingleRepositoryTargetPaths(manifest, resolution);
    }

    public static string ContentAddressedResolutionPath(string basePath, string sha256)
    {
        var directory = Path.GetDirectoryName(basePath)
            ?? throw new InvalidDataException("Resolution output has no directory.");
        var stem = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".yaml";
        var suffix = sha256[..12];
        return Path.Combine(directory,
            stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? stem + extension
                : stem + "-" + suffix + extension);
    }
}
