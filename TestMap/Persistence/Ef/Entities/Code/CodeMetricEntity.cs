namespace TestMap.Persistence.Ef.Entities.Code;

public class CodeMetricEntity
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int MaintainabilityIndex { get; set; }
    public int CyclomaticComplexity { get; set; }
    public int ClassCoupling { get; set; }
    public int DepthOfInheritance { get; set; }   
    public int SourceLinesOfCode { get; set; }
    public int ExecutableLinesOfCode { get; set; }   
}