using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class MemberRelationshipRepository
{
    private readonly TestMapDbContext _context;

    public MemberRelationshipRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<int> InsertOrUpdateAsync(MemberRelationshipModel model)
    {
        var existing = await _context.MemberRelationships.FirstOrDefaultAsync(x =>
            x.SourceId == model.SourceId &&
            x.TargetId == model.TargetId &&
            x.RelationshipType == model.RelationshipType);
        if (existing != null)
        {
            return existing.Id;
        }

        var entity = model.ToEntity();
        _context.MemberRelationships.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }
}
