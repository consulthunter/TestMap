namespace TestMap.Models.Code;

public class ImportModel(
    int fileId = 0,
    string guid = "",
    string importName = "",
    string importPath = "",
    string fullString = "",
    bool isLocal = false)
{
    public int Id { get; set; } = 0;
    public int FileId { get; set; } = fileId;
    public string Guid { get; set; } = guid;
    public string ImportName { get; set; } = importName;
    public string ImportPath { get; set; } = importPath;
    public string FullString { get; set; } = fullString;
    public bool IsLocal { get; set; } = isLocal;
}
