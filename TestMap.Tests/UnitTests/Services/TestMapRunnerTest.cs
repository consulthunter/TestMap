using System.IO;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Moq;
using TestMap.Services;
using TestMap.Services.Configuration;
using Xunit;

namespace TestMap.Tests.UnitTests.Services;

[TestSubject(typeof(TestMapRunner))]
public class TestMapRunnerTest
{
    private readonly Mock<ConfigurationService> _mockConfigurationService;
    private readonly Mock<TestMapRunner> _mockTestMapRunner;

    public TestMapRunnerTest()
    {
        var jsonConfig = """

                         {
                           "FilePaths": {
                             "TargetFilePath": "D:\\Projects\\TestMap\\TestMap.Tests\\Data\\example_project.txt",
                             "LogsDirPath": "D:\\Projects\\TestMap\\TestMap.Tests\\Logs",
                             "TempDirPath": "D:\\Projects\\TestMap\\Temp",
                             "OutputDirPath": "D:\\Projects\\TestMap\\TestMap.Tests\\Output"
                           },
                           "Settings": {
                             "MaxConcurrency": 5,
                             "RunDateFormat": "yyyy-MM-dd"
                           },
                           "Frameworks": {
                             "NUnit": [
                               "Test",
                               "Theory"
                             ],
                             "xUnit": [
                               "Fact",
                               "Theory"
                             ],
                             "MSTest": [
                               "TestMethod",
                               "DataSource"
                             ],
                             "Microsoft.VisualStudio.TestTools.UnitTesting": [
                               "TestMethod",
                               "DataSource"
                             ]
                           },
                           "Scripts": {
                             "Clone": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_git_clone.bat",
                             "Clean": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_dotnet_clean.bat",
                             "Restore": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_dotnet_restore.bat",
                             "Build": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_dotnet_build.bat",
                             "Test": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_dotnet_coverage_dotnet_test.bat",
                             "Stryker": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_dotnet_stryker.bat",
                             "Delete": "D:\\Projects\\TestMap\\TestMap\\Scripts\\run_rm.bat"
                           },
                           "EnvironmentVariables": {
                             "DOTNET_ROOT": "C:\\Program Files\\dotnet\\",
                             "DOTNET_HOST_PATH": "C:\\Program Files\\dotnet\\dotnet.exe",
                             "MSBuildExtensionsPath": "C:\\Program Files\\dotnet\\sdk\\8.0.302",
                             "MSBUILD_EXE_PATH": "C:\\Program Files\\dotnet\\sdk\\8.0.302\\MSBuild.dll",
                             "MSBuildSDKsPath": "C:\\Program Files\\dotnet\\sdk\\8.0.302\\Sdks"
                           }
                         }
                         """;

        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(jsonConfig)))
            .Build();

        _mockConfigurationService = new Mock<ConfigurationService>(MockBehavior.Default, config);
        _mockTestMapRunner = new Mock<TestMapRunner>();
    }

    [Fact]
    [Trait("Category", "CI")]
    public async Task RunAsync_ShouldStartTestMap()
    {
        // Arrange & Act
        await _mockTestMapRunner.Object.RunAsync();

        // Assert
        _mockTestMapRunner.VerifyAll();
    }
}