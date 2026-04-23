using TestMap.Models.Code;
using TestMap.Persistence.Ef.Entities.Code;

namespace TestMap.Persistence.Ef.Mapping.Code;

public static class ObjectMappingExtensions
{
    public static ObjectEntity ToEntity(this ObjectModel model)
    {
        return new ObjectEntity
        {
            FileId = model.FileId,
            Name = model.Name,
            Kind = model.Kind,
            Namespace = model.Namespace,
            Attributes = model.Attributes,
            Modifiers = model.Modifiers,
            DocString = model.DocString,
            FullString = model.FullString,
            IsTestObject = model.IsTestObject,
            TestFramework = model.TestFramework,
            Location = model.Location
        };
    }

    public static ObjectModel ToDomain(this ObjectEntity entity)
    {
        return new ObjectModel(
            attributes: entity.Attributes,
            modifiers: entity.Modifiers,
            location: entity.Location,
            id: entity.Id,
            fileId: entity.FileId,
            @namespace: entity.Namespace,
            name: entity.Name,
            kind: entity.Kind,
            docString: entity.DocString,
            fullString: entity.FullString,
            isTestObject: entity.IsTestObject,
            testFramework: entity.TestFramework
        );
    }
}