using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class CSharpProjectRepository
{
    private readonly TestMapDbContext _context;

    public CSharpProjectRepository(TestMapDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<CSharpProjectModel>> GetAllAsync()
    {
        var entities = await _context.CSharpProjects.ToListAsync();
        return entities.Select(e => e.ToDomain()).ToList();
    }
    
    public async Task<CSharpProjectModel?> GetByIdAsync(int id)
    {
        var entity = await _context.CSharpProjects.FindAsync(id);
        return entity?.ToDomain();
    }
    
    public async Task<CSharpProjectModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.CSharpProjects.FirstOrDefaultAsync(p => p.ContentHash == contentHash);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(CSharpProjectModel model)
    {
        var existing = await _context.CSharpProjects.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.FilePath = model.FilePath;
                existing.BuildTargets = model.BuildTargets;
                existing.DefaultBuildTarget = model.DefaultBuildTarget;
                existing.BuildMetadata = model.BuildMetadata;
                await _context.SaveChangesAsync();
            }
            return existing.Id;
            
        }
        var solution = await _context.CSharpSolutions.FirstOrDefaultAsync(x => x.Id == model.SolutionId);

        if (solution == null)
        {
            throw new InvalidOperationException("Solution not found for the given ID");
        }
        
        var entity = model.ToEntity(solution.Id);
        _context.CSharpProjects.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }
    
    private bool HasChanged(Entities.Code.CSharpProjectEntity entity, CSharpProjectModel model)
    {
        return model.FilePath != entity.FilePath ||
               model.DefaultBuildTarget != entity.DefaultBuildTarget ||
               model.BuildTargets.Count != entity.BuildTargets.Count ||
               model.ContentHash != entity.ContentHash;
    }
}
