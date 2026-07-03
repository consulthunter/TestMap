using TestMap.Models.Experiment;

namespace TestMap.Models.Configuration.Testing.Generation;

public class StepAblationConfig
{
    public bool Enabled { get; set; }
    public List<GenerationStepType> Steps { get; set; } = new();
    public bool IncludeBaseline { get; set; } = true;
    public bool IncludeAllDisabled { get; set; } = false;
    public int MaxVariants { get; set; } = 32;
}
