namespace TestMap.Models.MutationTesting;

public class StrykerTest
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public StrykerLocation location { get; set; } = new();
}
