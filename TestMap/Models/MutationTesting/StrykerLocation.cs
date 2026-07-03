namespace TestMap.Models.MutationTesting;

public class StrykerLocation
{
    public StrykerPosition start { get; set; } = new();
    public StrykerPosition end { get; set; } = new();
}