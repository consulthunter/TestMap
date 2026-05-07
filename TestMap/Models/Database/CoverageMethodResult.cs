namespace TestMap.Models.Database;

public class CoverageMethodResult
{
    // Method info
    public int MethodId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MethodBody { get; set; } = string.Empty;

    public double LineRate { get; set; } = 0.0;
    public double BranchRate { get; set; } = 0.0;

    // Production class info
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;

    // Coverage status (enum would be cleaner, but keeping as string for now)
    public string CoverageStatus { get; set; } = string.Empty;

    // Related test class info (null if none)
    public int TestClassId { get; set; }
    public string TestClassName { get; set; } = string.Empty;
    public string TestFramework { get; set; } = string.Empty;
    public string TestClassBody { get; set; } = string.Empty;

    // Test class location (nullable since not all methods have tests)
    public int TestClassLineStart { get; set; }
    public int TestClassBodyStart { get; set; }
    public int TestClassLineEnd { get; set; }
    public int TestClassBodyEnd { get; set; }

    // Source file info
    public string TestFilePath { get; set; } = string.Empty;
    public string TestDependencies { get; set; } = string.Empty;
    public string TestNamespace { get; set; } = string.Empty;

    // Test method info
    public int TestMethodId { get; set; }
    public string TestMethodName { get; set; } = string.Empty;
    public string TestMethodBody { get; set; } = string.Empty;

    public string SolutionFilePath { get; set; } = string.Empty;
}