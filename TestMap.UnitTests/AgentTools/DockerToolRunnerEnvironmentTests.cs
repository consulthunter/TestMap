using TestMap.Models.AgentTools;
using TestMap.Models.Configuration.Experiment;
using TestMap.Services.AgentTools;

namespace TestMap.UnitTests.AgentTools;

public sealed class DockerToolRunnerEnvironmentTests
{
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
