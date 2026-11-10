using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Runtime;
using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

/// <summary>
/// Container naming and timeout cleanup for <see cref="DockerToolRunner"/>, driven by a scripted
/// <c>docker</c> process. No Docker daemon is required.
/// </summary>
public sealed class DockerToolRunnerContainerLifecycleTests : IDisposable
{
    private readonly DirectoryInfo _workspace = Directory.CreateTempSubdirectory("testmap-workspace-");
    private readonly DirectoryInfo _artifacts = Directory.CreateTempSubdirectory("testmap-attempt-");

    /// <summary>
    /// Attempt ids restart at 1 in every repository's database, so parallel repositories ran the
    /// same tool attempt under one container name and Docker refused the second (pilot V7, exit 125).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateContainerName_SameToolAndAttemptId_IsUniquePerRun()
    {
        var request = Request(attemptId: 4);

        var first = DockerToolRunner.CreateContainerName(request);
        var second = DockerToolRunner.CreateContainerName(request);

        Assert.NotEqual(first, second);
        Assert.StartsWith("testmap-aider-custom-glm-4-", first);
        Assert.StartsWith("testmap-aider-custom-glm-4-", second);
    }

    /// <summary>
    /// A timed-out tool must not outlive its attempt: the container is killed and confirmed gone
    /// before the runner returns, so rollback and the next attempt see a quiet workspace.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_ToolTimesOut_KillsContainerAndWaitsForRemoval()
    {
        var docker = new ScriptedDocker { TimeOutRun = true, InspectExitCodes = new Queue<int>([0, 1]) };
        var runner = Runner(docker);

        var result = await runner.RunAsync(Request(attemptId: 4), CancellationToken.None);

        var containerName = docker.RunContainerName;
        Assert.Equal(124, result.ExitCode);
        Assert.True(result.TimedOut);
        Assert.Equal(
            [
                "run",
                $"kill {containerName}",
                $"container inspect {containerName}",
                $"container inspect {containerName}"
            ],
            docker.Commands);
        Assert.Contains($"Killed container '{containerName}'", result.StdErr);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_ContainerOutlivesRemovalWait_ForceRemovesIt()
    {
        var docker = new ScriptedDocker { TimeOutRun = true, InspectExitCodes = new Queue<int>() };
        var runner = Runner(docker, removalTimeout: TimeSpan.FromMilliseconds(50));

        var result = await runner.RunAsync(Request(attemptId: 4), CancellationToken.None);

        Assert.Equal($"rm -f {docker.RunContainerName}", docker.Commands[^1]);
        Assert.Contains("Force-removed container", result.StdErr);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RunAsync_ToolExitsOnItsOwn_LeavesContainerAlone()
    {
        var docker = new ScriptedDocker();
        var runner = Runner(docker);

        var result = await runner.RunAsync(Request(attemptId: 4), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["run"], docker.Commands);
    }

    private DockerToolRunner Runner(ScriptedDocker docker, TimeSpan? removalTimeout = null)
    {
        var runtime = new RuntimeConfig
        {
            Docker = new DockerConfig
            {
                Images = new DockerImageRegistry
                {
                    AgentTools = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["aider"] = "testmap-agent-eval-aider:latest"
                    }
                }
            }
        };
        return new DockerToolRunner(
            runtime,
            docker.RunAsync,
            TimeSpan.Zero,
            removalTimeout ?? TimeSpan.FromSeconds(5));
    }

    private ToolRunRequest Request(int attemptId)
    {
        return new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "aider-custom-glm", ImageKey = "aider" },
            Attempt = new ToolAttempt { Id = attemptId, TimeoutSeconds = 1 },
            WorkspacePath = _workspace.FullName,
            ArtifactPath = _artifacts.FullName
        };
    }

    public void Dispose()
    {
        _workspace.Delete(recursive: true);
        _artifacts.Delete(recursive: true);
    }

    private sealed class ScriptedDocker
    {
        public bool TimeOutRun { get; init; }
        public Queue<int> InspectExitCodes { get; init; } = new();
        public List<string> Commands { get; } = [];
        public string RunContainerName { get; private set; } = string.Empty;

        public Task<DockerToolRunner.ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var args = arguments.SkipWhile(x => x != "run" && x != "kill" && x != "container" && x != "rm").ToList();
            if (args[0] == "run")
            {
                Commands.Add("run");
                RunContainerName = args[args.IndexOf("--name") + 1];
                return Task.FromResult(TimeOutRun
                    ? new DockerToolRunner.ProcessExecutionResult(124, string.Empty, string.Empty, timeout, true)
                    : new DockerToolRunner.ProcessExecutionResult(0, string.Empty, string.Empty, TimeSpan.Zero, false));
            }

            Commands.Add(string.Join(" ", args));
            // An inspect that finds the container exits 0; once it is gone, docker exits non-zero.
            var exitCode = args[0] == "container" && InspectExitCodes.Count > 0 ? InspectExitCodes.Dequeue() : 0;
            return Task.FromResult(
                new DockerToolRunner.ProcessExecutionResult(exitCode, string.Empty, string.Empty, TimeSpan.Zero, false));
        }
    }
}
