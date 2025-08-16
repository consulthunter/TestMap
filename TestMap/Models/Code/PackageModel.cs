namespace TestMap.Models.Code;

public class PackageModel(
    List<FileModel> files,
    int projectId = 0,
    string guid = "",
    string name = "",
    string path = "")
{
    public int Id { get; set; } = 0;
    public int ProjectId { get; set; } = projectId;
    public string Guid { get; set; } = guid;
    public string Name { get; set; } = name;
    public string? Path { get; set; } = path;
    
    public List<FileModel> Files { get; set; } = files;
}
