using Microsoft.EntityFrameworkCore;
using TestMap.Models;
using TestMap.Persistence.Ef.Mapping;

namespace TestMap.Persistence.Ef.Repositories;

public class ProjectRepository
{
    private readonly TestMapDbContext _context;

    public ProjectRepository(TestMapDbContext context)
    {
        _context = context;
    }

    // Queries
    public async Task<List<ProjectModel>> GetAllAsync()
    {
        var entities = await _context.Projects.ToListAsync();
        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<ProjectModel?> GetByIdAsync(int id)
    {
        var entity = await _context.Projects.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<ProjectModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.Projects
            .FirstOrDefaultAsync(p => p.ContentHash == contentHash);
        return entity?.ToDomain();
    }

    // Insert or Update - returns the ID
    public async Task<int> InsertOrUpdateAsync(ProjectModel model)
    {
        var existing = await _context.Projects
            .FirstOrDefaultAsync(p => p.ContentHash == model.ContentHash);

        if (existing != null)
        {
            // Update only if changed
            if (HasChanged(existing, model))
            {
                existing.Owner = model.Owner;
                existing.RepoName = model.RepoName;
                existing.Branch = model.Branch ?? existing.Branch;
                existing.LastAnalyzedCommit = model.LastAnalyzedCommit ?? existing.LastAnalyzedCommit;
                existing.DatabasePath = model.DatabasePath ?? existing.DatabasePath;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            model.DbId = existing.Id;
            return existing.Id;
        }

        // Insert new
        var entity = model.ToEntity();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Projects.Add(entity);
        await _context.SaveChangesAsync();

        model.DbId = entity.Id;
        return entity.Id;
    }

    // Check if entity has changed compared to model
    private bool HasChanged(Entities.ProjectEntity existing, ProjectModel model)
    {
        return existing.Owner != model.Owner ||
               existing.RepoName != model.RepoName ||
               existing.Branch != model.Branch ||
               existing.LastAnalyzedCommit != model.LastAnalyzedCommit ||
               existing.DatabasePath != model.DatabasePath;
    }

    // Delete
    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Projects.FindAsync(id);
        if (entity != null)
        {
            _context.Projects.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}