namespace TestMap.Models.Code;

public class GenerateTestMethod
{
    public int Id { get; set; } = 0;
    public int TestRunId { get; set; }
    public int SourceMethodId { get; set; }
    public int TestMethodId { get; set; }
    public int GenTestMethodId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public double GenerationDuration { get; set; }
}