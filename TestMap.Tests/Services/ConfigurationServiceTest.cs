using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using TestMap.Services;
using Xunit;

namespace TestMap.Tests.Services;

[TestSubject(typeof(ConfigurationService))]
public class ConfigurationServiceTest
{
    private readonly ConfigurationService _configurationService;

    public ConfigurationServiceTest()
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
                             "NUnit": ["Test", "Theory"],
                             "xUnit": ["Fact", "Theory"],
                             "MSTest": ["TestMethod", "DataSource"],
                             "Microsoft.VisualStudio.TestTools.UnitTesting": ["TestMethod", "DataSource"]
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

        _configurationService = new ConfigurationService(config);
    }

    [Fact]
    public async Task RunAsync_ShouldInitialize()
    {
        // Arrange & Act
        await _configurationService.ConfigureRunAsync();

        // Act & Assert
        Assert.Equal(5, _configurationService.GetConcurrency());
        Assert.True(Directory.Exists(_configurationService.GetLogsDirectory()));
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), _configurationService.GetRunDate());
        Assert.NotEmpty(_configurationService.GetProjectModels());
    }
}