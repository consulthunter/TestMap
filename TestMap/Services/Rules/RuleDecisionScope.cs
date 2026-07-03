namespace TestMap.Services.Rules;

public sealed class RuleDecisionScope
{
    private RuleDecisionScope(string kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    public string Kind { get; }
    public string Id { get; }

    public static RuleDecisionScope Project(int id) => new("Project", id.ToString());
    public static RuleDecisionScope CSharpProject(int id) => new("CSharpProject", id.ToString());
    public static RuleDecisionScope CandidateMethod(int id) => new("CandidateMethod", id.ToString());
    public static RuleDecisionScope ExperimentRun(int id) => new("ExperimentRun", id.ToString());
    public static RuleDecisionScope ExperimentMatrixWorkItem(int id) => new("ExperimentMatrixWorkItem", id.ToString());
    public static RuleDecisionScope GenerationAttempt(int id) => new("GenerationAttempt", id.ToString());
    public static RuleDecisionScope GenerationStep(int id) => new("GenerationStep", id.ToString());
    public static RuleDecisionScope TestExecution(int id) => new("TestExecution", id.ToString());
}
