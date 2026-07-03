using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;
using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

public sealed class DockerToolRunnerEnvironmentTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildDockerRunArguments_AddsHostDockerInternalGateway()
    {
        var workspace = Directory.CreateTempSubdirectory("testmap-workspace-");
        var attempt = Directory.CreateTempSubdirectory("testmap-attempt-");
        try
        {
            var args = DockerToolRunner.BuildDockerRunArguments(
                new ToolRunRequest
                {
                    ToolConfig = new ExperimentToolConfig { Id = "mini-swe-agent" },
                    WorkspacePath = workspace.FullName,
                    ArtifactPath = attempt.FullName
                },
                "testmap-agent-eval-mini-swe-agent:latest");

            var addHostIndex = args.IndexOf("--add-host");
            Assert.True(addHostIndex >= 0);
            Assert.Equal("host.docker.internal:host-gateway", args[addHostIndex + 1]);
        }
        finally
        {
            workspace.Delete(recursive: true);
            attempt.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_OpenHandsAnthropic_MapsNormalizedProviderTuple()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "openhands" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "anthropic",
                ["TESTMAP_LLM_MODEL"] = "claude-sonnet-4-5",
                ["TESTMAP_LLM_API_KEY"] = "sk-ant"
            }
        });

        Assert.Equal("anthropic/claude-sonnet-4-5", env["LLM_MODEL"]);
        Assert.Equal("sk-ant", env["LLM_API_KEY"]);
        Assert.Equal("sk-ant", env["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_OpenHandsCustomOpenAi_MapsEndpoint()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "openhands" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "openai",
                ["TESTMAP_LLM_MODEL"] = "unsloth/gemma-4-E4B-it-GGUF/BF16",
                ["TESTMAP_LLM_API_KEY"] = "custom-key",
                ["TESTMAP_LLM_BASE_URL"] = "http://host.docker.internal:8080/v1/"
            }
        });

        Assert.Equal("openai/unsloth/gemma-4-E4B-it-GGUF/BF16", env["LLM_MODEL"]);
        Assert.Equal("custom-key", env["LLM_API_KEY"]);
        Assert.Equal("http://host.docker.internal:8080/v1/", env["LLM_BASE_URL"]);
        Assert.Equal("custom-key", env["OPENAI_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_OpenHandsAlreadyPrefixedModel_DoesNotDoublePrefix()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "openhands" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "openai",
                ["TESTMAP_LLM_MODEL"] = "openai/gpt-oss-120b",
                ["TESTMAP_LLM_API_KEY"] = "custom-key"
            }
        });

        Assert.Equal("openai/gpt-oss-120b", env["LLM_MODEL"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_AiderOpenAi_MapsModelAndApiKey()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "aider" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "openai",
                ["TESTMAP_LLM_MODEL"] = "gpt-5",
                ["TESTMAP_LLM_API_KEY"] = "sk-openai"
            }
        });

        Assert.Equal("openai/gpt-5", env["AIDER_MODEL"]);
        Assert.Equal("sk-openai", env["OPENAI_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_ClaudeAnthropic_MapsModelAndApiKey()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "claude" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "anthropic",
                ["TESTMAP_LLM_MODEL"] = "claude-sonnet-4-6",
                ["TESTMAP_LLM_API_KEY"] = "sk-ant"
            }
        });

        Assert.Equal("claude-sonnet-4-6", env["CLAUDE_MODEL"]);
        Assert.Equal("sk-ant", env["ANTHROPIC_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_Copilot_PreservesCopilotToken()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "copilot" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["GITHUB_COPILOT_TOKEN"] = "copilot-token"
            }
        });

        Assert.Equal("copilot-token", env["GITHUB_COPILOT_TOKEN"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_MiniSweAgentGoogle_MapsModelAndGeminiAliases()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "mini-swe-agent" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "google-gemini",
                ["TESTMAP_LLM_MODEL"] = "gemini-2.5-pro",
                ["TESTMAP_LLM_API_KEY"] = "gemini-key"
            }
        });

        Assert.Equal("gemini/gemini-2.5-pro", env["MINI_MODEL"]);
        Assert.Equal("gemini-key", env["GEMINI_API_KEY"]);
        Assert.Equal("gemini-key", env["GOOGLE_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_MiniSweAgentCustomOpenAi_MapsCustomEndpoint()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig { Id = "mini-swe-agent" },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "openai",
                ["TESTMAP_LLM_MODEL"] = "unsloth/gemma-4-E4B-it-GGUF/BF16",
                ["TESTMAP_LLM_API_KEY"] = "custom-key",
                ["TESTMAP_LLM_BASE_URL"] = "https://models.example.test/v1"
            }
        });

        Assert.Equal("openai/unsloth/gemma-4-E4B-it-GGUF/BF16", env["MINI_MODEL"]);
        Assert.Equal("openai", env["MINI_PROVIDER"]);
        Assert.Equal("https://models.example.test/v1", env["MINI_API_BASE"]);
        Assert.Equal("custom-key", env["OPENAI_API_KEY"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildContainerEnvironment_ToolEnvironment_OverridesDerivedValues()
    {
        var env = DockerToolRunner.BuildContainerEnvironment(new ToolRunRequest
        {
            ToolConfig = new ExperimentToolConfig
            {
                Id = "aider",
                Environment = new Dictionary<string, string>
                {
                    ["AIDER_MODEL"] = "anthropic/custom-model"
                }
            },
            ResolvedEnvironment = new Dictionary<string, string>
            {
                ["TESTMAP_LLM_PROVIDER"] = "anthropic",
                ["TESTMAP_LLM_MODEL"] = "claude-sonnet-4-5",
                ["TESTMAP_LLM_API_KEY"] = "sk-ant"
            }
        });

        Assert.Equal("anthropic/custom-model", env["AIDER_MODEL"]);
    }
}
