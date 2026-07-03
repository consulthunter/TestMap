using TestMap.Services.TestExecution;

namespace TestMap.UnitTests.TestExecution;

public sealed class DockerRuntimePathMapperTests
{
    /// <summary>
    /// Verifies that Linux container paths are rooted at the mounted project path and use forward slashes.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GetContainerPath_WithLinuxContext_ReturnsLinuxContainerPath()
    {
        // Arrange
        var mapper = new DockerRuntimePathMapper();
        var projectDirectory = Path.Combine(Path.GetTempPath(), "TestMapMapper", "repo");
        var projectFile = Path.Combine(projectDirectory, "tests", "Sample.Tests.csproj");

        // Act
        var containerPath = mapper.GetContainerPath(
            projectFile,
            projectDirectory,
            DockerRuntimePathMapper.LinuxContextName);

        // Assert
        Assert.Equal("/app/project/tests/Sample.Tests.csproj", containerPath);
    }

    /// <summary>
    /// Verifies that Windows container paths are rooted at the mounted project path and use backslashes.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GetContainerPath_WithWindowsContext_ReturnsWindowsContainerPath()
    {
        // Arrange
        var mapper = new DockerRuntimePathMapper();
        var projectDirectory = Path.Combine(Path.GetTempPath(), "TestMapMapper", "repo");
        var projectFile = Path.Combine(projectDirectory, "tests", "Sample.Tests.csproj");

        // Act
        var containerPath = mapper.GetContainerPath(
            projectFile,
            projectDirectory,
            DockerRuntimePathMapper.WindowsContextName);

        // Assert
        Assert.Equal(@"C:\app\project\tests\Sample.Tests.csproj", containerPath);
    }

    /// <summary>
    /// Verifies that paths outside the mounted project directory are rejected.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GetContainerPath_WithPathOutsideProject_Throws()
    {
        // Arrange
        var mapper = new DockerRuntimePathMapper();
        var projectDirectory = Path.Combine(Path.GetTempPath(), "TestMapMapper", "repo");
        var outsidePath = Path.Combine(Path.GetTempPath(), "OtherRepo", "Sample.Tests.csproj");

        // Act + Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.GetContainerPath(outsidePath, projectDirectory, DockerRuntimePathMapper.LinuxContextName));
        Assert.Contains("outside the mounted project directory", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetContainerPath_WithSiblingPathSharingProjectPrefix_Throws()
    {
        var mapper = new DockerRuntimePathMapper();
        var tempRoot = Path.Combine(Path.GetTempPath(), "TestMapMapper", Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(tempRoot, "repo");
        var siblingPath = Path.Combine(tempRoot, "repo-other", "Sample.Tests.csproj");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            mapper.GetContainerPath(siblingPath, projectDirectory, DockerRuntimePathMapper.LinuxContextName));

        Assert.Contains("outside the mounted project directory", exception.Message);
    }

    /// <summary>
    /// Verifies mount argument and expected OS selection for Linux and Windows contexts.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(DockerRuntimePathMapper.LinuxContextName, "/app/project", "linux")]
    [InlineData(DockerRuntimePathMapper.WindowsContextName, @"C:\app\project", "windows")]
    public void GetMountArgumentAndResolveExpectedOs_WithContext_ReturnExpectedValues(
        string dockerContext,
        string containerRoot,
        string expectedOs)
    {
        // Arrange
        var mapper = new DockerRuntimePathMapper();

        // Act
        var mount = mapper.GetMountArgument(@"D:\repo", dockerContext);
        var os = mapper.ResolveExpectedOs(dockerContext);

        // Assert
        Assert.Equal($"-v \"D:\\repo:{containerRoot}\"", mount);
        Assert.Equal(expectedOs, os);
    }
}
