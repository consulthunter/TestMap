using TestMap.Rules.TestExecution;

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
        var decision = TestExecutionDecisionEngine.DecideContainerPath(hostPath, projectDirectory, dockerContext);
        if (decision.RuleId == TestExecutionRuleDefinitions.ContainerPathOutsideProject.Id)
            throw new InvalidOperationException($"Path '{hostPath}' is outside the mounted project directory.");

        return decision.Value;
    }

    public string GetMountArgument(string projectDirectory, string dockerContext)
    {
        return TestExecutionDecisionEngine.DecideMountArgument(projectDirectory, dockerContext).Value;
    }

    public bool IsWindowsContext(string dockerContext)
    {
        return dockerContext.Contains(WindowsContextName, StringComparison.OrdinalIgnoreCase);
    }

    public string ResolveExpectedOs(string dockerContext)
    {
        return TestExecutionDecisionEngine.DecideExpectedOs(dockerContext).Value;
    }
}
