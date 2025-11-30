namespace TestMap.Models.Configuration;

public enum RunMode
{
    Setup,
    ValidateProjects,
    CollectTests, // Collect tests and run them
    GenerateTests, // Generate new tests
    FullAnalysis // Analyze test results or projects
}