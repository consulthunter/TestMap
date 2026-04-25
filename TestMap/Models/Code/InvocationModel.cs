namespace TestMap.Models.Code;

public class InvocationModel(
    Location location,
    int id = 0,
    int memberId = 0,
    int? invokedMemberId = null,
    bool isAssertion = false,
    string fullString = "")
{
    public int Id { get; set; } = id;
    public int MemberId { get; set; } = memberId;
    public int? InvokedMemberId { get; set; } = invokedMemberId;
    public bool IsAssertion { get; set; } = isAssertion;
    public string FullString { get; set; } = fullString;
    public Location Location { get; set; } = location;

    public string ContentHash => Utilities.Utilities.ComputeSha256(
        $"{MemberId}:{InvokedMemberId}:{Location.StartLineNumber}:{Location.BodyStartPosition}:{FullString}");
}