using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class CSharpSolutionRepository
{
    private readonly TestMapDbContext _context;

    public CSharpSolutionRepository(TestMapDbContext context)
    {
        _context = context;
    }

    public async Task<List<CSharpSolutionModel>> GetAllAsync()
    {
        return await _context.CSharpSolutions.Select(p => p.ToDomain()).ToListAsync();
    }

    public async Task<CSharpSolutionModel?> GetByIdAsync(int id)
    {
        var entity = await _context.CSharpSolutions.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<CSharpSolutionModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.CSharpSolutions.FirstOrDefaultAsync(p => p.ContentHash == contentHash);
        return entity?.ToDomain();
    }

    public async Task<int> InsertOrUpdateAsync(CSharpSolutionModel model)
    {
        var existing = await _context.CSharpSolutions.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.ProjectId = model.ProjectId;
                existing.FilePath = model.FilePath;
                await _context.SaveChangesAsync();
            }

            return existing.Id;
        }

        var project = await _context.Projects.FindAsync(model.ProjectId);

        if (project == null) throw new InvalidOperationException("Project not found for the given ID");

        var entity = model.ToEntity(project.Id);
        _context.CSharpSolutions.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }

    private bool HasChanged(Entities.Code.CSharpSolutionEntity entity, CSharpSolutionModel model)
    {
        return model.FilePath != entity.FilePath ||
               model.ContentHash != entity.ContentHash;
    }
}