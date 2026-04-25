namespace TestMap.Services.TestExecution;

public class DockerRuntimePathMapper
{
    public const string LinuxProjectRoot = "/app/project";
    public const string WindowsProjectRoot = @"C:\app\project";
    public const string WindowsPythonCommand = @"C:\Python312\python.exe";
    public const string WindowsContextName = "desktop-windows";
    public const string LinuxContextName = "desktop-linux";

    public string GetContainerPath(string hostPath, string projectDirectory, string dockerContext)
    {
        var projectRoot = Path.GetFullPath(projectDirectory);
        var fullPath = Path.GetFullPath(hostPath);

        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path '{hostPath}' is outside the mounted project directory.");

        var relativePath = Path.GetRelativePath(projectRoot, fullPath);
        return IsWindowsContext(dockerContext)
            ? $@"{WindowsProjectRoot}\{relativePath.Replace('/', '\\')}"
            : $"{LinuxProjectRoot}/{relativePath.Replace('\\', '/')}";
    }

    public string GetMountArgument(string projectDirectory, string dockerContext)
    {
        return IsWindowsContext(dockerContext)
            ? $"-v \"{projectDirectory}:{WindowsProjectRoot}\""
            : $"-v \"{projectDirectory}:{LinuxProjectRoot}\"";
    }

    public bool IsWindowsContext(string dockerContext)
    {
        return dockerContext.Contains(WindowsContextName, StringComparison.OrdinalIgnoreCase);
    }

    public string ResolveExpectedOs(string dockerContext)
    {
        return IsWindowsContext(dockerContext) ? "windows" : "linux";
    }
}