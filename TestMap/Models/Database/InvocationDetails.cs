namespace TestMap.Models.Database;

public class InvocationDetails
{
    public int InvocationId { get; set; }
    public int TargetMethodId { get; set; }
    public int SourceMethodId { get; set; } // ← added
    public string InvocationGuid { get; set; }
    public string FullString { get; set; }

    public int MethodId { get; set; }
    public int MethodClassId { get; set; }
    public string MethodGuid { get; set; }
    public string MethodName { get; set; }

    public int ClassId { get; set; }
    public int FileId { get; set; }
    public string ClassGuid { get; set; }
    public string ClassName { get; set; }

    public string FileName { get; set; }
    public string FilePath { get; set; }
    public int PackageId { get; set; }
    public string FileGuid { get; set; }

    public int SourcePackageId { get; set; }
    public string PackageName { get; set; }
    public int AnalysisProjectId { get; set; }
    public string PackageGuid { get; set; }

    public int ProjectId { get; set; }
    public string ProjectPath { get; set; }
    public int SolutionId { get; set; }
    public string ProjectGuid { get; set; }

    public int SolutionDbId { get; set; }
    public string SolutionPath { get; set; }
    public string SolutionGuid { get; set; }
}