namespace TestMap.EndToEndTests;

public class EndToEndTestProjectMarkerTests
{
    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Execution", "LocalOnly")]
    public void End_to_end_test_project_is_wired_into_the_solution()
    {
        Assert.True(true);
    }
}
