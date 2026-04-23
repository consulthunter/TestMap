
namespace TestMap.Models.Code;

public class CSharpProjectModel(
    List<string> projectReferences,
    List<string> documentFilePaths,
    List<string> buildTargets,
    ProjectBuildMetadataModel? buildMetadata = null,
    int id = 0,
    int solutionId = 0,
    string filePath = "",
    string defaultBuildTarget = "")
{
    public int Id { get; set; } = id;
    public int SolutionId { get; set; } = solutionId;
    public string FilePath { get; set; } = filePath;
    public List<string> BuildTargets { get; set; } = buildTargets;
    public string DefaultBuildTarget { get; set; } = defaultBuildTarget;
    public ProjectBuildMetadataModel BuildMetadata { get; set; } = buildMetadata ?? new ProjectBuildMetadataModel();
    public string ContentHash => Utilities.Utilities.ComputeSha256(FilePath);

    public List<string> ProjectReferences { get; set; } = projectReferences;
    public List<string> DocumentFilePaths { get; set; } = documentFilePaths;
}
