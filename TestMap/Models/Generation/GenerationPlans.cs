namespace TestMap.Models.Generation;

public class ScenarioPlan
{
    public string Scenario { get; set; } = string.Empty;
}

public class ArrangePlan
{
    public List<ArrangeDependencyPlan> Dependencies { get; set; } = new();
    public List<string> RequiredNamespaces { get; set; } = new();
    public List<string> HelperReuse { get; set; } = new();
    public bool RequiresMocks { get; set; }
    public string ArrangeStrategy { get; set; } = string.Empty;
}

public class ArrangeDependencyPlan
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Construction { get; set; } = string.Empty;
    public bool IsMock { get; set; }
}

public class InputPlan
{
    public List<string> Inputs { get; set; } = new();
    public List<string> Preconditions { get; set; } = new();
}

public class ActionPlan
{
    public string Invocation { get; set; } = string.Empty;
    public string? ResultBinding { get; set; }
}

public class AssertionPlan
{
    public List<string> Assertions { get; set; } = new();
    public string ExpectedBehavior { get; set; } = string.Empty;
}