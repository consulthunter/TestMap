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
            attributes: entity.Attributes,
            modifiers: entity.Modifiers,
            testCategories: entity.TestCategories,
            location: entity.Location,
            id: entity.Id,
            objectEntityId: entity.ObjectEntityId,
            name: entity.Name,
            kind: entity.Kind,
            docString: entity.DocString,
            fullString: entity.FullString,
            isTestMember: entity.IsTestMember,
            testIntent: entity.TestIntent,
            isGenerated: entity.IsGenerated,
            testMetadataSource: entity.TestMetadataSource,
            testMetadataConfidence: entity.TestMetadataConfidence,
            testMetadataPromptVersion: entity.TestMetadataPromptVersion
        );
    }
}
