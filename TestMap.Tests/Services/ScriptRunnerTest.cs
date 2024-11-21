using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using TestMap.Services;
using Xunit;

namespace TestMap.Tests.Services;

[TestSubject(typeof(ScriptRunner))]
public class ScriptRunnerTest
{
    private readonly ScriptRunner _mockScriptRunner;

    public ScriptRunnerTest()
    {
        _mockScriptRunner = new ScriptRunner();
    }

    [Fact]
    public async Task RunScriptAsync_ShouldCaptureOutputAndErrors()
    {
        // Arrange
        var commands = new List<string> { "Get-Process", "dir" };

        // Act
        await _mockScriptRunner.RunScriptAsync(commands, "delete.bat");

        // Assert
        Assert.NotEmpty(_mockScriptRunner.Output);
        Assert.Empty(_mockScriptRunner.Errors);
        Assert.False(_mockScriptRunner.HasError);
    }
}