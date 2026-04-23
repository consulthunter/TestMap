using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class InvocationRepository
{
    private readonly TestMapDbContext _context;

    public InvocationRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<InvocationModel>> GetAllAsync()
    {
        var entities = await _context.Invocations.ToListAsync();
        return entities.Select(x => x.ToDomain()).ToList();
    }

    public async Task<InvocationModel?> GetByIdAsync(int id)
    {
        var entity = await _context.Invocations.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<InvocationModel?> GetByContentHashAsync(string contenHash)
    {
        var entity = await _context.Invocations.FirstOrDefaultAsync(x => x.ContentHash == contenHash);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(InvocationModel model)
    {
        var existing = await _context.Invocations.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);
        if (existing == null)
        {
            var candidates = await _context.Invocations
                .Where(x =>
                    x.MemberId == model.MemberId &&
                    x.InvokedMemberId == model.InvokedMemberId &&
                    x.FullString == model.FullString)
                .ToListAsync();

            existing = candidates.FirstOrDefault(x =>
                x.Location.StartLineNumber == model.Location.StartLineNumber &&
                x.Location.BodyStartPosition == model.Location.BodyStartPosition);
        }

        if (existing != null)
        {
            if (existing.ContentHash != model.ContentHash || existing.IsAssertion != model.IsAssertion)
            {
                existing.ContentHash = model.ContentHash;
                existing.IsAssertion = model.IsAssertion;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var entity = model.ToEntity();
        _context.Invocations.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }
}
