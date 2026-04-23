namespace TestMap.Models.MutationTesting;

public class StrykerMutant
{
    public string id { get; set; } = string.Empty;
    public string mutatorName { get; set; } = string.Empty;
    public string replacement { get; set; } = string.Empty;
    public StrykerLocation location { get; set; } = new();
    public string status { get; set; } = string.Empty;
    public string statusReason { get; set; } = string.Empty;
    public bool @static { get; set; }
    public List<string> coveredBy { get; set; } = new();
    public List<string> killedBy { get; set; } = new();
}
