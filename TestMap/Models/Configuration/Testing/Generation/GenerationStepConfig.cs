namespace TestMap.Models.Configuration.Testing.Generation;

public class GenerationStepConfig
{
    public string VariantId { get; set; } = "baseline";
    public bool EnableEvidencePackage { get; set; } = true;
    public bool EnableContextGraph { get; set; } = false;
    public bool EnableContextResolution { get; set; } = false;
    public bool EnableRoslynValidation { get; set; } = true;
    public bool EnableScenario { get; set; } = true;
    public bool EnableMethodName { get; set; } = true;
    public bool EnableArrangePlan { get; set; } = true;
    public bool EnableInputPlan { get; set; } = true;
    public bool EnableActionPlan { get; set; } = true;
    public bool EnableAssertionPlan { get; set; } = true;
    public bool EnableFinalTest { get; set; } = true;
}
