namespace TestMap.EndToEndTests;

public class EndToEndTestProjectMarkerTests
{
    /// <summary>
    /// Verifies that the end-to-end test project is wired into the solution and can execute tests.
    /// </summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Execution", "LocalOnly")]
    public void End_to_end_test_project_is_wired_into_the_solution()
    {
        // Arrange

        // Act

        // Assert
        Assert.True(true);
    }
}
