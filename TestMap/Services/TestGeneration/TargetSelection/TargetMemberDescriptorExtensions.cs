using TestMap.Models.Experiment;
using TestMap.Models.Testing;

namespace TestMap.Services.TestGeneration.TargetSelection;

public static class TargetMemberDescriptorExtensions
{
    public static TargetMemberDescriptor ToTargetMemberDescriptor(
        this CandidateMethodContext context,
        CandidateMethod? candidate = null)
    {
        candidate ??= context.Method;
        var location = context.SourceLocation;
        return new TargetMemberDescriptor(
            candidate.MemberId,
            candidate.MethodName,
            string.IsNullOrWhiteSpace(context.MethodSignature) ? candidate.Signature : context.MethodSignature,
            context.ContainingType,
            string.IsNullOrWhiteSpace(location.SourceFilePath) ? context.SourceFilePath : location.SourceFilePath,
            location.StartLine,
            location.EndLine);
    }
}
