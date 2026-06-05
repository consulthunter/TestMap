using TestMap.Models.Configuration.Experiment;
using TestMap.Services.AgentTools;

namespace TestMap.Services.Experiment.Execution;

public sealed class AgentToolAvailabilityDecision
{
    public ExperimentToolConfig Tool { get; init; } = new();
    public ToolAvailabilityResult Availability { get; init; } = new();
    public bool ShouldExecute { get; init; }
    public bool ShouldFailSetup { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class AgentToolAvailabilityPlan
{
    public IReadOnlyList<AgentToolAvailabilityDecision> Decisions { get; init; } = [];

    public IReadOnlyList<ExperimentToolConfig> ExecutableTools =>
        Decisions.Where(x => x.ShouldExecute).Select(x => x.Tool).ToList();

    public IReadOnlyList<AgentToolAvailabilityDecision> SkippedTools =>
        Decisions.Where(x => !x.ShouldExecute && !x.ShouldFailSetup).ToList();

    public IReadOnlyList<AgentToolAvailabilityDecision> SetupFailures =>
        Decisions.Where(x => x.ShouldFailSetup).ToList();

    public static AgentToolAvailabilityPlan Create(
        IReadOnlyList<ExperimentToolConfig> tools,
        IReadOnlyList<ToolAvailabilityResult> availabilityResults,
        bool requireAvailabilityInSetup)
    {
        var resultsByToolId = availabilityResults.ToDictionary(x => x.ToolId, StringComparer.OrdinalIgnoreCase);
        var decisions = tools.Select(tool =>
        {
            var availability = resultsByToolId.TryGetValue(tool.Id, out var result)
                ? result
                : new ToolAvailabilityResult
                {
                    ToolId = tool.Id,
                    IsAvailable = false,
                    UnavailableReason = "No availability result was returned."
                };

            if (availability.IsAvailable)
            {
                return new AgentToolAvailabilityDecision
                {
                    Tool = tool,
                    Availability = availability,
                    ShouldExecute = true
                };
            }

            var reason = string.IsNullOrWhiteSpace(availability.UnavailableReason)
                ? $"Agent tool '{tool.Id}' is unavailable."
                : availability.UnavailableReason!;

            return new AgentToolAvailabilityDecision
            {
                Tool = tool,
                Availability = availability,
                ShouldExecute = false,
                ShouldFailSetup = requireAvailabilityInSetup,
                Reason = reason
            };
        }).ToList();

        return new AgentToolAvailabilityPlan { Decisions = decisions };
    }
}
