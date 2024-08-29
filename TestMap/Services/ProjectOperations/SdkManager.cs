using TestMap.Models;

namespace TestMap.Services.ProjectOperations;

public class SdkManager
{
    private ProjectModel _projectModel;

    public SdkManager(ProjectModel projectModel)
    {
        _projectModel = projectModel;
    }
}