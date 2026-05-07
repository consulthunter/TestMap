namespace TestMap.Models.Code;

public class CodeMetricsModel(
    string entityType,
    int id = 0,
    int entityId = 0,
    int maintainabilityIndex = 0,
    int cyclomaticComplexity = 0,
    int classCoupling = 0,
    int depthOfInheritance = 0,
    int sourceLinesOfCode = 0,
    int executableLinesOfCode = 0)
{
    public int Id { get; set; } = id;
    public int EntityId { get; set; } = entityId;
    public string EntityType { get; set; } = entityType;
    public int MaintainabilityIndex { get; set; } = maintainabilityIndex;
    public int CyclomaticComplexity { get; set; } = cyclomaticComplexity;
    public int ClassCoupling { get; set; } = classCoupling;
    public int DepthOfInheritance { get; set; } = depthOfInheritance;
    public int SourceLinesOfCode { get; set; } = sourceLinesOfCode;
    public int ExecutableLinesOfCode { get; set; } = executableLinesOfCode;
}