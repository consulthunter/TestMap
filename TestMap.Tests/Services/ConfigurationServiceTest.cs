using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using TestMap.Services;
using Xunit;

namespace TestMap.Tests.Services;

[TestSubject(typeof(ConfigurationService))]
public class ConfigurationServiceTest
{
    private readonly ConfigurationService _configurationService;

    public ConfigurationServiceTest()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("temp.json", optional: false, reloadOnChange: true)
            .Build();
        _configurationService = new ConfigurationService(config);
    }

    [Fact]
    public async Task RunAsync_ShouldInitialize()
    {
        // Arrange & Act
        await _configurationService.ConfigureRunAsync();

        // Act
        Assert.Equal(4, _configurationService.GetConcurrency());
        Assert.True(Path.Exists(_configurationService.GetLogsDirectory()));
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), _configurationService.GetRunDate());
        Assert.NotEmpty(_configurationService.GetProjectModels());
    }
}