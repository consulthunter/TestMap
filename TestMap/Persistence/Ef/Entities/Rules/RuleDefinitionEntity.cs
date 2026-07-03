namespace TestMap.Persistence.Ef.Entities.Rules;

public class RuleDefinitionEntity
{
    public int Id { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string RuleVersion { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
