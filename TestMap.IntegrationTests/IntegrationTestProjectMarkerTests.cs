namespace TestMap.IntegrationTests;

public class IntegrationTestProjectMarkerTests
{
    /// <summary>
    /// Verifies that the integration test project is wired into the solution and can execute tests.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void Integration_test_project_is_wired_into_the_solution()
    {
        // Arrange

        // Act

        // Assert
        Assert.True(true);
    }
}
