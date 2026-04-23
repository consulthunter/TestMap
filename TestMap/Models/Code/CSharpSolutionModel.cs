namespace TestMap.Models.Code;

public class CSharpSolutionModel(
    List<string> projects,
    int id = 0,
    int projectId = 0,
    string filePath = "")
{
    public int Id { get; set; } = id;
    public int ProjectId { get; set; } = projectId;
    public string FilePath { get; set; } = filePath;
    public string ContentHash => Utilities.Utilities.ComputeSha256(FilePath);
    public List<string> Projects { get; set; } = projects;
}
