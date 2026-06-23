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

    /// <summary>
    /// Resolves the persisted test member for a freshly generated test (by method name,
    /// preferring the member in the applied file), marks it <c>IsGenerated</c>, and returns
    /// its DB id. Returns null when no matching member is found (e.g. the test did not validate
    /// and was therefore not persisted). Mirrors the member→object→file lookup used by the
    /// agentic <c>ToolAttemptGeneratedTestService</c>.
    /// </summary>
    public async Task<int?> ResolveAndMarkGeneratedTestMemberAsync(string methodName, string? appliedFilePath)
    {
        if (string.IsNullOrWhiteSpace(methodName))
            return null;

        var candidates = await (
            from member in _context.Members
            join obj in _context.Objects on member.ObjectEntityId equals obj.Id
            join file in _context.Files on obj.FileId equals file.Id
            where member.IsTestMember && member.Kind == "method" && member.Name == methodName
            select new { Member = member, file.FilePath }
        ).ToListAsync();

        if (candidates.Count == 0)
            return null;

        var chosen = candidates[0].Member;
        if (!string.IsNullOrWhiteSpace(appliedFilePath))
        {
            var applied = Path.GetFullPath(appliedFilePath);
            var match = candidates.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.FilePath) &&
                string.Equals(Path.GetFullPath(x.FilePath), applied, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                chosen = match.Member;
        }

        if (!chosen.IsGenerated)
        {
            chosen.IsGenerated = true;
            await _context.SaveChangesAsync();
        }

        return chosen.Id;
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