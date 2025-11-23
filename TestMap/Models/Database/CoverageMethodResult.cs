namespace TestMap.Models.Database;

public class CoverageMethodResult
{
    // Method info
    public int MethodId { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string MethodBody { get; set; } = string.Empty;

    public double LineRate { get; set; } = 0.0;

    // Production class info
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;

    // Coverage status (enum would be cleaner, but keeping as string for now)
    public string CoverageStatus { get; set; } = string.Empty;

    // Related test class info (null if none)
    public int TestClassId { get; set; }
    public string TestClassName { get; set; }
    public string TestFramework { get; set; }

    // Test class location (nullable since not all methods have tests)
    public int TestClassLineStart { get; set; }
    public int TestClassBodyStart { get; set; }
    public int TestClassLineEnd { get; set; }
    public int TestClassBodyEnd { get; set; }

    // Source file info
    public string TestFilePath { get; set; }
    public string TestDependencies { get; set; }
    
    // Test method info
    public string TestMethodName { get; set; }
    public string TestMethodBody { get; set; }
    
    public string SolutionFilePath { get; set; } = string.Empty;
}