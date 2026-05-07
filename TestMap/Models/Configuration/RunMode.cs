namespace TestMap.Models.Configuration;

public enum RunMode
{
    Setup,
    CheckProjects,
    Results,
    StaticAnalysis, // Run static source analysis and enrichment only
    CollectTests, // Collect tests and run them
    GenerateTests, // Generate new tests
    Experiment // Run AI provider comparison experiments
}