namespace TestMap.Models.Code;

public class InvocationModel(
    int targetMethodId = 0,
    int sourceMethodId = 0,
    string guid = "",
    bool isAssertion = false,
    string fullString = "",
    Location? location = null)
{
    public int Id { get; set; } = 0;
    public int TargetMethodId { get; set; } = targetMethodId;
    public int SourceMethodId { get; set; } = sourceMethodId;
    public string Guid { get; set; } = guid;
    public bool IsAssertion { get; set; } = isAssertion;
    public string FullString { get; set; } = fullString;
    public Location Location { get; set; } = location ?? new Location(0, 0, 0, 0);
    
    public string ContentHash { get; set;} = Utilities.Utilities.ComputeSha256(fullString);
}