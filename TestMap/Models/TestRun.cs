namespace TestMap.Models;

public class TestRun
{
    public string Id { get; set; }
    public string ProjectId { get; set; }
    public DateTime Timestamp { get; set; }
    public string CommitHash { get; set; }
    public bool IsBaseline { get; set; }
    public string Description { get; set; } 
}