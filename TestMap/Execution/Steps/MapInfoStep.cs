using TestMap.App;
using TestMap.Services.Mapping;

namespace TestMap.Execution.Steps;

public class MapInfoStep : IPipelineStep
{
    private IMapUnresolvedService _mapUnresolvedService;
    
    public MapInfoStep(IMapUnresolvedService mapUnresolvedService)
    {
        _mapUnresolvedService = mapUnresolvedService;
    }
    
    public async Task ExecuteAsync(ProjectContext? context = null)
    {
        await _mapUnresolvedService.MapUnresolvedAsync();
    }
}
