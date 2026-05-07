using TestMap.Models.Code;
using TestMap.Rules.TestExecution;

namespace TestMap.Services.TestExecution;

internal static class BuildTestDockerCommandFactory
{
    public static string ResolveDockerContext(
        string configuredContext,
        bool requiresWindows,
        DockerRuntimePathMapper pathMapper)
    {
        return TestExecutionDecisionEngine.DecideDockerContext(configuredContext, requiresWindows, pathMapper).Value;
    }

    public static bool SolutionSetRequiresWindows(IEnumerable<CSharpProjectModel> solutionProjects)
    {
        return bool.Parse(TestExecutionDecisionEngine.DecideSolutionWindowsRequirement(solutionProjects).Value);
    }

    public static string? TryResolveCommonBaselineTestFramework(IEnumerable<CSharpProjectModel> solutionProjects)
    {
        var decision = TestExecutionDecisionEngine.DecideCommonBaselineTestFramework(solutionProjects);
        return string.IsNullOrWhiteSpace(decision.Value) ? null : decision.Value;
    }

    public static string? ChoosePreferredTargetFramework(CSharpProjectModel project)
    {
        var decision = TestExecutionDecisionEngine.DecidePreferredTargetFramework(project);
        return string.IsNullOrWhiteSpace(decision.Value) ? null : decision.Value;
    }

    public static string ResolveCoverageCollectorArgument(CoverageCollectorType collectorType)
    {
        return TestExecutionDecisionEngine.DecideCoverageCollectorArgument(collectorType).Value;
    }

    public static string CreateBaselineBuildArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runId,
        IReadOnlyCollection<string> solutionFilenames)
    {
        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet-build --run-id {Quote(runId)} --solutions {Quote(string.Join(",", solutionFilenames))}");
    }

    public static string CreateBaselineTestsArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runId,
        IReadOnlyCollection<string> solutionFilenames,
        string? targetFramework)
    {
        var frameworkArgs = string.IsNullOrWhiteSpace(targetFramework)
            ? string.Empty
            : $" --framework {Quote(targetFramework)}";

        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet-tests --run-id {Quote(runId)} --solutions {Quote(string.Join(",", solutionFilenames))}{frameworkArgs}");
    }

    public static string CreateBaselineMutationArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runId,
        IReadOnlyCollection<string> solutionFilenames)
    {
        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet-stryker --run-id {Quote(runId)} --solutions {Quote(string.Join(",", solutionFilenames))}");
    }

    public static string CreateTargetedTestsArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runId,
        string containerProjectPath,
        string? targetFramework,
        string? collector)
    {
        var frameworkArgs = string.IsNullOrWhiteSpace(targetFramework)
            ? string.Empty
            : $" --framework {Quote(targetFramework)}";
        var collectorArgs = string.IsNullOrWhiteSpace(collector)
            ? string.Empty
            : $" --collector {Quote(collector)}";

        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet-test-project --run-id {Quote(runId)} --project {Quote(containerProjectPath)}{frameworkArgs}{collectorArgs}");
    }

    public static string CreateTargetedMutationArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runId,
        string containerSourceProjectPath,
        string containerTestProjectPath)
    {
        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet-stryker-project --run-id {Quote(runId)} --report-name {Quote(Path.GetFileNameWithoutExtension(containerSourceProjectPath))} --test-project {Quote(containerTestProjectPath)}");
    }

    public static string CreateDotnetPassthroughArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        IReadOnlyList<string> dotnetArgs,
        string? containerWorkingDirectory)
    {
        var workingDirectoryArg = string.IsNullOrWhiteSpace(containerWorkingDirectory)
            ? string.Empty
            : $" --working-directory {Quote(containerWorkingDirectory)}";
        var dotnetArgumentText = string.Join(" ", dotnetArgs.Select(Quote));

        return CreateRunnerArgs(
            dockerContext,
            containerName,
            mount,
            imageName,
            $"dotnet{workingDirectoryArg} {dotnetArgumentText}");
    }

    private static string CreateRunnerArgs(
        string dockerContext,
        string containerName,
        string mount,
        string imageName,
        string runnerArguments)
    {
        var pythonCommand = IsWindowsContext(dockerContext)
            ? DockerRuntimePathMapper.WindowsPythonCommand
            : "python3";
        return $"--context {dockerContext} run -d --name {containerName} {mount} {imageName} {pythonCommand} -m testmap_runner {runnerArguments}";
    }

    private static bool IsWindowsContext(string dockerContext)
    {
        return dockerContext.Contains(DockerRuntimePathMapper.WindowsContextName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value)
    {
        return DockerCommandRunner.QuoteDockerArgument(value);
    }

}
