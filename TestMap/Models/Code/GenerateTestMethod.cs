namespace TestMap.Models.Code;

public class GenerateTestMethod
{
    public string Id { get; set; }
    public string TestMethodId { get; set; }
    public string TestRunId { get; set; }
    public string FilePath { get; set; }
    public string GeneratedCode { get; set; }
    public DateTime CreatedAt { get; set; }
}