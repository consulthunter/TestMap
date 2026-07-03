using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class MemberMappingExtensions
{
    public static MemberEntity ToEntity(this MemberModel model)
    {
        return new MemberEntity
        {
            ObjectEntityId = model.ObjectEntityId,
            Name = model.Name,
            Kind = model.Kind,
            Attributes = model.Attributes,
            Modifiers = model.Modifiers,
            DocString = model.DocString,
            FullString = model.FullString,
            IsGenerated = model.IsGenerated,
            IsTestMember = model.IsTestMember,
            Location = model.Location,
            TestCategories = model.TestCategories,
            TestIntent = model.TestIntent,
            TestMetadataSource = model.TestMetadataSource,
            TestMetadataConfidence = model.TestMetadataConfidence,
            TestMetadataPromptVersion = model.TestMetadataPromptVersion
        };
    }

    public static MemberModel ToDomain(this MemberEntity entity)
    {
        return new MemberModel(
            entity.Attributes,
            entity.Modifiers,
            entity.TestCategories,
            entity.Location,
            entity.Id,
            entity.ObjectEntityId,
            entity.Name,
            entity.Kind,
            entity.DocString,
            entity.FullString,
            entity.IsTestMember,
            entity.TestIntent,
            entity.IsGenerated,
            entity.TestMetadataSource,
            entity.TestMetadataConfidence,
            entity.TestMetadataPromptVersion
        );
    }
}