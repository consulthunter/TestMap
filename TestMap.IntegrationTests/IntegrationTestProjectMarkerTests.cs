namespace TestMap.IntegrationTests;

public class IntegrationTestProjectMarkerTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Execution", "LocalOnly")]
    public void Integration_test_project_is_wired_into_the_solution()
    {
        Assert.True(true);
    }
}
