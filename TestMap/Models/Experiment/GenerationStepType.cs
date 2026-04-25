namespace TestMap.Models.Experiment;

/// <summary>
/// Types of steps in the decomposed test generation process.
/// </summary>
public enum GenerationStepType
{
    Scenario,
    MethodName,
    ArrangePlan,
    InputPlan,
    ActionPlan,
    AssertionPlan,
    FinalTest,
    CompileRepair,
    BehaviorRepair
}