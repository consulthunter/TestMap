using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class MemberRepository
{
    private readonly TestMapDbContext _context;

    public MemberRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<MemberModel>> GetAllAsync()
    {
        var entities = await _context.Members.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<MemberModel?> GetByIdAsync(int id)
    {
        var entity = await _context.Members.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<MemberModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.Members.FirstOrDefaultAsync(x => x.ContentHash == contentHash);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(MemberModel model)
    {
        var existing = await _context.Members.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.ObjectEntityId = model.ObjectEntityId;
                existing.Name = model.Name;
                existing.Kind = model.Kind;
                existing.Attributes = model.Attributes;
                existing.Modifiers = model.Modifiers;
                existing.DocString = model.DocString;
                existing.FullString = model.FullString;
                existing.IsGenerated = model.IsGenerated;
                existing.IsTestMember = model.IsTestMember;
                existing.Location = model.Location;
                existing.TestCategories = model.TestCategories;
                existing.TestIntent = model.TestIntent;
                existing.TestMetadataSource = model.TestMetadataSource;
                existing.TestMetadataConfidence = model.TestMetadataConfidence;
                existing.TestMetadataPromptVersion = model.TestMetadataPromptVersion;
                existing.ContentHash = model.ContentHash;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity();
        entity.ContentHash = model.ContentHash;
        _context.Members.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    private static bool HasChanged(Entities.Code.MemberEntity entity, MemberModel model)
    {
        return entity.ObjectEntityId != model.ObjectEntityId ||
               entity.Name != model.Name ||
               entity.Kind != model.Kind ||
               !entity.Attributes.SequenceEqual(model.Attributes) ||
               !entity.Modifiers.SequenceEqual(model.Modifiers) ||
               entity.DocString != model.DocString ||
               entity.FullString != model.FullString ||
               entity.IsGenerated != model.IsGenerated ||
               entity.IsTestMember != model.IsTestMember ||
               !entity.TestCategories.SequenceEqual(model.TestCategories) ||
               entity.TestIntent != model.TestIntent ||
               entity.TestMetadataSource != model.TestMetadataSource ||
               entity.TestMetadataConfidence != model.TestMetadataConfidence ||
               entity.TestMetadataPromptVersion != model.TestMetadataPromptVersion ||
               entity.Location.StartLineNumber != model.Location.StartLineNumber ||
               entity.Location.BodyStartPosition != model.Location.BodyStartPosition ||
               entity.Location.EndLineNumber != model.Location.EndLineNumber ||
               entity.Location.BodyEndPosition != model.Location.BodyEndPosition ||
               entity.ContentHash != model.ContentHash;
    }
}
