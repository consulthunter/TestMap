using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class ObjectRepository
{
    private readonly TestMapDbContext _context;

    public ObjectRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<ObjectModel>> GetAllAsync()
    {
        var entities = await _context.Objects.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<ObjectModel?> GetByIdAsync(int id)
    {
        var entity = await _context.Objects.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<ObjectModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.Objects.FirstOrDefaultAsync(x => x.ContentHash == contentHash);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(ObjectModel model)
    {
        var existing = await _context.Objects.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.FileId = model.FileId;
                existing.Namespace = model.Namespace;
                existing.Name = model.Name;
                existing.Kind = model.Kind;
                existing.Attributes = model.Attributes;
                existing.Modifiers = model.Modifiers;
                existing.DocString = model.DocString;
                existing.FullString = model.FullString;
                existing.IsTestObject = model.IsTestObject;
                existing.TestFramework = model.TestFramework;
                existing.Location = model.Location;
                existing.ContentHash = model.ContentHash;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity();
        entity.ContentHash = model.ContentHash;
        _context.Objects.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    private static bool HasChanged(Entities.Code.ObjectEntity entity, ObjectModel model)
    {
        return entity.FileId != model.FileId ||
               entity.Namespace != model.Namespace ||
               entity.Name != model.Name ||
               entity.Kind != model.Kind ||
               !entity.Attributes.SequenceEqual(model.Attributes) ||
               !entity.Modifiers.SequenceEqual(model.Modifiers) ||
               entity.DocString != model.DocString ||
               entity.FullString != model.FullString ||
               entity.IsTestObject != model.IsTestObject ||
               entity.TestFramework != model.TestFramework ||
               entity.Location.StartLineNumber != model.Location.StartLineNumber ||
               entity.Location.BodyStartPosition != model.Location.BodyStartPosition ||
               entity.Location.EndLineNumber != model.Location.EndLineNumber ||
               entity.Location.BodyEndPosition != model.Location.BodyEndPosition ||
               entity.ContentHash != model.ContentHash;
    }
}