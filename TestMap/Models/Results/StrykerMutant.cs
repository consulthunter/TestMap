namespace TestMap.Models.Results;

public class StrykerMutant
{
    public string id { get; set; }
    public string mutatorName { get; set; }
    public string replacement { get; set; }
    public StrykerLocation location { get; set; }
    public string status { get; set; }
    public string statusReason { get; set; }
    public bool @static { get; set; }
    public List<string> coveredBy { get; set; }
    public List<string> killedBy { get; set; }
}