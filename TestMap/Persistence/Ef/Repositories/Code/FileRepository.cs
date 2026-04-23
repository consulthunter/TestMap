using Microsoft.EntityFrameworkCore;
using TestMap.Models.Code;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Persistence.Ef.Repositories.Code;

public class FileRepository
{
    private readonly TestMapDbContext _context;
    
    public FileRepository(TestMapDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<FileModel>> GetAllAsync()
    {
        var entities = await _context.Files.ToListAsync();
        return entities.Select(e => e.ToDomain()).ToList();
    }
    
    public async Task<FileModel?> GetByIdAsync(int id)
    {
        var entity = await _context.Files.FindAsync(id);
        return entity?.ToDomain();
    }

    public async Task<FileModel?> GetByContentHashAsync(string contentHash)
    {
        var entity = await _context.Files.FirstOrDefaultAsync(p => p.ContentHash == contentHash);
        return entity?.ToDomain();   
    }

    public async Task<int> InsertOrUpdateAsync(FileModel model)
    {
        var existing = await _context.Files.FirstOrDefaultAsync(x => x.ContentHash == model.ContentHash);

        if (existing != null)
        {
            if (HasChanged(existing, model))
            {
                existing.FilePath = model.FilePath;
                existing.ContentHash = model.ContentHash;
                await _context.SaveChangesAsync();
            }
            return existing.Id;
        }

        var entity = model.ToEntity();
        _context.Files.Add(entity);
        await _context.SaveChangesAsync();
        return entity.Id;
    }
    
    private bool HasChanged(Entities.Code.FileEntity entity, FileModel model)
    {
        return model.AnalysisProjectId != entity.CSharpProjectId ||
               !model.UsingStatements.SequenceEqual(entity.UsingStatements) ||
               model.FilePath != entity.FilePath ||
               model.ContentHash != entity.ContentHash;
    }
}
