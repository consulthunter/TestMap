namespace TestMap.Models.Configuration;

public enum RunMode
{
    CollectTests,       // Collect tests and run them
    GenerateTests,      // Generate new tests
    FullAnalysis,       // Analyze test results or projects
}