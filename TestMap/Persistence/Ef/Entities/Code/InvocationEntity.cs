using TestMap.Models.Code;

namespace TestMap.Persistence.Ef.Entities.Code;

public class InvocationEntity
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public int? InvokedMemberId { get; set; }
    public bool IsAssertion { get; set; }
    public string FullString { get; set; } = string.Empty;
    public Location Location { get; set; } = new(0, 0, 0, 0);
    public string ResolutionStatus { get; set; } = "Resolved";
    public string TargetSymbol { get; set; } = string.Empty;
    public string SyntaxKind { get; set; } = string.Empty;
    public string CallerMemberSymbol { get; set; } = string.Empty;
    public string CallerFilePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
}
