using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IO;
using TestMap.Services;
using TestMap.Services.Configuration;
using Xunit;

namespace TestMap.Tests.IntegrationTests;

public class TestMapIntegrationTest
{
    private readonly IConfiguration _config;
    private readonly ConfigurationService _configurationService;
    private readonly string _testConfigFilePath;
    private readonly TestMapRunner _testMapRunner;

    public TestMapIntegrationTest()
    {
        _testConfigFilePath = "D:\\Projects\\TestMap\\TestMap.Tests\\Config\\test-config.json";
        _config = new ConfigurationBuilder()
            .AddJsonFile(_testConfigFilePath, false, true)
            .Build();
        _configurationService = new ConfigurationService(_config);
        _testMapRunner = new TestMapRunner(_configurationService);
    }

    private async Task RunCollect()
    {
        await _testMapRunner.RunAsync();
    }

    [Fact]
    [Trait("Category", "Local")]
    public async Task TestMap()
    {
        await RunCollect();

        Assert.True(Directory.Exists(_configurationService.GetLogsDirectory()));
        Assert.NotEmpty(_configurationService.GetProjectModels());
    }
}