using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TestMap.Services;
using Xunit;

namespace TestMap.Tests.UnitTests.Services;

[TestSubject(typeof(ScriptRunner))]
public class ScriptRunnerTest
{
    private readonly ScriptRunner _mockScriptRunner;

    public ScriptRunnerTest()
    {
        _mockScriptRunner = new ScriptRunner();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task RunScriptAsync_ShouldExecuteBatchFile()
    {
        // Arrange
        var commands = new List<string> { "dir" };

        // Act
        await _mockScriptRunner.RunBatchScriptAsync(commands, "/not/a/path");

        // Assert
        Assert.Empty(_mockScriptRunner.Output);
        Assert.NotEmpty(_mockScriptRunner.Errors);
        Assert.True(_mockScriptRunner.HasError);
    }
}