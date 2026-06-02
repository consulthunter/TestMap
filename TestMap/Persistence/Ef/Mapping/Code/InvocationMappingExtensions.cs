using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class InvocationMappingExtensions
{
    public static InvocationEntity ToEntity(this InvocationModel model)
    {
        return new InvocationEntity
        {
            MemberId = model.MemberId,
            InvokedMemberId = model.InvokedMemberId,
            IsAssertion = model.IsAssertion,
            Location = model.Location,
            FullString = model.FullString,
            ResolutionStatus = model.ResolutionStatus,
            TargetSymbol = model.TargetSymbol,
            SyntaxKind = model.SyntaxKind,
            CallerMemberSymbol = model.CallerMemberSymbol,
            CallerFilePath = model.CallerFilePath,
            ContentHash = model.ContentHash
        };
    }

    public static InvocationModel ToDomain(this InvocationEntity entity)
    {
        return new InvocationModel(
            entity.Location,
            entity.Id,
            entity.MemberId,
            entity.InvokedMemberId,
            entity.IsAssertion,
            entity.FullString,
            entity.ResolutionStatus,
            entity.TargetSymbol,
            entity.SyntaxKind,
            entity.CallerMemberSymbol,
            entity.CallerFilePath
        );
    }
}
