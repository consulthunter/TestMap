using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TestMap.Models.Configuration;
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
        var config = new ConfigurationBuilder()
            .AddJsonFile(_testConfigFilePath, false, true)
            .Build();
        var configObj = new TestMapConfig();
        config.Bind(configObj);
        _configurationService = new ConfigurationService(configObj);
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
    }
}