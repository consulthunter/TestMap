using Microsoft.Extensions.DependencyInjection;
using TestMap.Services;
using TestMap.Services.Targets;
using TestMap.Services.Targets.Contracts;
using TestMap.Services.Logging;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryServiceRegistrationTests
{
    [Fact]
    public void AddProjectServices_ResolvesCompleteSingleRepositoryCreationGraph()
    {
        using var provider = new ServiceCollection()
            .AddProjectServices()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<SingleRepositoryUrlParser>(
            scope.ServiceProvider.GetRequiredService<ISingleRepositoryUrlParser>());
        Assert.IsType<GitHubRepositoryResolutionClient>(
            scope.ServiceProvider.GetRequiredService<IRepositoryResolutionClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SingleRepositoryTargetCreationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectLogDirectoryAllocator>());
    }
}
