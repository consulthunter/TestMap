namespace TestMap.Models.Testing;

public sealed record TargetMemberDescriptor(
    int SourceMemberId,
    string MethodName,
    string MethodSignature,
    string ContainingType,
    string SourceFilePath,
    int StartLine,
    int EndLine);
