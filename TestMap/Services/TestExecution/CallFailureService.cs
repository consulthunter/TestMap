using TestMap.Models.Results;
using TestMap.Rules.TestExecution;

namespace TestMap.Services.TestExecution;

public class CallFailureService
{
    public FailureAnalysisModel? Analyze(string? logs, string? processDiagnostics = null)
    {
        return TestExecutionDecisionEngine.DecideFailureAnalysis(logs, processDiagnostics);
    }
}
