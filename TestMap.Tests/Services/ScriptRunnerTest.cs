using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
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

    private string CreateBatchFile(List<string> commands)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), "delete.bat");
        File.WriteAllLines(tempFilePath, commands);
        return tempFilePath;
    }

    [Fact]
    public async Task RunScriptAsync_ShouldExecuteBatchFile()
    {
        // Arrange
        var commands = new List<string> { "dir" };
        var batchFilePath = CreateBatchFile(commands);

        try
        {
            // Act
            await _mockScriptRunner.RunScriptAsync(commands, batchFilePath);

            // Assert
            Assert.NotEmpty(_mockScriptRunner.Output);
            Assert.Empty(_mockScriptRunner.Errors);
            Assert.False(_mockScriptRunner.HasError);
        }
        finally
        {
            // Clean up
            if (File.Exists(batchFilePath)) File.Delete(batchFilePath);
        }
    }
}