using Microsoft.EntityFrameworkCore;
using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Persistence.Ef.Mappings;

namespace TestMap.Persistence.Ef.Repositories.Testing;

public class TestSmellRepository
{
    private readonly TestMapDbContext _context;

    public TestSmellRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<TestSmellModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestSmells.ToListAsync(cancellationToken);
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<TestSmellModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TestSmells.FindAsync([id], cancellationToken);
        return entity?.ToDomain();
    }

    public async Task<List<TestSmellModel>> GetByMemberIdAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.TestSmells
            .Where(x => x.MemberId == memberId)
            .ToListAsync(cancellationToken);

        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<int> InsertOrUpdateAsync(
        TestSmellModel model,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.TestSmells.FirstOrDefaultAsync(x =>
                x.ProjectId == model.ProjectId &&
                x.MemberId == model.MemberId &&
                x.ObjectId == model.ObjectId &&
                x.SmellId == model.SmellId &&
                x.FilePath == model.FilePath &&
                x.Line == model.Line &&
                x.Column == model.Column,
            cancellationToken);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.MemberId = model.MemberId;
                existing.ObjectId = model.ObjectId;
                existing.SmellName = model.SmellName;
                existing.Message = model.Message;
                existing.ContainingTypeName = model.ContainingTypeName;
                existing.TestMethodName = model.TestMethodName;
                existing.AnalyzedAtUtc = model.AnalyzedAtUtc;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        var entity = model.ToEntity();
        _context.TestSmells.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public static bool HasChanged(TestSmellEntity entity, TestSmellModel model)
    {
        return entity.MemberId != model.MemberId ||
               entity.ObjectId != model.ObjectId ||
               entity.SmellName != model.SmellName ||
               entity.Message != model.Message ||
               entity.ContainingTypeName != model.ContainingTypeName ||
               entity.TestMethodName != model.TestMethodName ||
               entity.AnalyzedAtUtc != model.AnalyzedAtUtc;
    }

    public static void Apply(TestSmellEntity entity, TestSmellModel model)
    {
        entity.MemberId = model.MemberId;
        entity.ObjectId = model.ObjectId;
        entity.SmellName = model.SmellName;
        entity.Message = model.Message;
        entity.ContainingTypeName = model.ContainingTypeName;
        entity.TestMethodName = model.TestMethodName;
        entity.AnalyzedAtUtc = model.AnalyzedAtUtc;
    }
}