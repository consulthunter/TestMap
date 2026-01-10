namespace TestMap.Models.Code;

public class GenerateTestMethod
{
    public int Id { get; set; } = 0;
    public int TestRunId { get; set; }
    public int SourceMethodId { get; set; }
    public int TestMethodId { get; set; }
    public string FilePath { get; set; }
    public string Provider { get; set; }
    public string Model { get; set; }
    public string Strategy { get; set; }
    public int TokenCount { get; set; }
    public double GenerationDuration { get; set; }
    public string GeneratedBody { get; set; }
}