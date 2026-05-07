using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class ObjectRelationshipRepository
{
    private readonly TestMapDbContext _context;

    public ObjectRelationshipRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<int> InsertOrUpdateAsync(ObjectRelationshipModel model)
    {
        var existing = await _context.ObjectRelationships.FirstOrDefaultAsync(x =>
            x.SourceId == model.SourceId &&
            x.TargetId == model.TargetId &&
            x.RelationshipType == model.RelationshipType);
        if (existing != null) return existing.Id;

        var entity = model.ToEntity();
        _context.ObjectRelationships.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }
}