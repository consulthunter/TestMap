namespace TestMap.Models.Database;

public class InvocationDetails
{
    public int InvocationId { get; set; }
    public int TargetMethodId { get; set; }
    public int SourceMethodId { get; set; } // ← added
    public string InvocationGuid { get; set; } = string.Empty;
    public string FullString { get; set; } = string.Empty;

    public int MethodId { get; set; }
    public int MethodClassId { get; set; }
    public string MethodGuid { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;

    public int ClassId { get; set; }
    public int FileId { get; set; }
    public string ClassGuid { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileGuid { get; set; } = string.Empty;

    public int AnalysisProjectId { get; set; }
    public string PackageGuid { get; set; } = string.Empty;

    public int ProjectId { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public int SolutionId { get; set; }
    public string ProjectGuid { get; set; } = string.Empty;

    public int SolutionDbId { get; set; }
    public string SolutionPath { get; set; } = string.Empty;
    public string SolutionGuid { get; set; } = string.Empty;
}