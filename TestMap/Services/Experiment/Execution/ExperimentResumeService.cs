using System.Text.Json;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Rules;
using TestMap.Rules;
using TestMap.Rules.Generation;

namespace TestMap.Services.Experiment.Execution;

public sealed class ExperimentResumeService : IExperimentResumeService
{
    public string BuildStableKey(
        string resumeGroupId,
        string repositoryIdentity,
        string commitHash,
        TestGenerationObjective objective,
        CandidateMethod candidateMethod,
        GenerationExperimentMatrixItem matrixItem)
    {
        return string.Join(
            "|",
            Normalize(resumeGroupId),
            Normalize(repositoryIdentity),
            Normalize(commitHash),
            objective,
            candidateMethod.MemberId.ToString(),
            matrixItem.Provider,
            Normalize(matrixItem.ModelName),
            matrixItem.Approach,
            matrixItem.MetricsPath?.ToString() ?? "none",
            matrixItem.ContextMode,
            matrixItem.BudgetMode,
            Normalize(matrixItem.Steps.VariantId));
    }

    public ExperimentMatrixWorkItem CreateWorkItem(
        int experimentRunId,
        string resumeGroupId,
        string repositoryIdentity,
        string commitHash,
        TestGenerationObjective objective,
        CandidateMethod candidateMethod,
        GenerationExperimentMatrixItem matrixItem)
    {
        return new ExperimentMatrixWorkItem
        {
            ExperimentRunId = experimentRunId,
            CandidateMethodId = candidateMethod.Id,
            MemberId = candidateMethod.MemberId,
            StableKey = BuildStableKey(resumeGroupId, repositoryIdentity, commitHash, objective, candidateMethod, matrixItem),
            Status = ExperimentMatrixWorkItemStatus.Pending,
            Provider = matrixItem.Provider,
            ModelName = matrixItem.ModelName,
            Objective = objective,
            Approach = matrixItem.Approach,
            MetricsPath = matrixItem.MetricsPath,
            ContextMode = matrixItem.ContextMode,
            BudgetMode = matrixItem.BudgetMode,
            AblationVariantId = matrixItem.Steps.VariantId,
            StepConfigJson = JsonSerializer.Serialize(matrixItem.Steps),
            CreatedAt = DateTime.UtcNow
        };
    }

    public ExperimentResumeDecision Evaluate(
        ExperimentMatrixWorkItem workItem,
        ExperimentResumeConfig config,
        DateTime utcNow)
    {
        var decisions = new List<RuleDecisionRecord>
        {
            Decision(
                GenerationExperimentRuleDefinitions.ResumeAttemptKeyGenerated,
                "ResumeAttemptKeyGenerated",
                "Attempt key generated from deterministic matrix values.",
                RuleDecisionFactory.CreateEvidence("ExperimentMatrixWorkItem", "StableKey", workItem.StableKey))
        };

        if (!config.Enabled)
        {
            return new ExperimentResumeDecision
            {
                WorkItem = workItem,
                ShouldExecute = true,
                RuleDecisions = decisions
            };
        }

        if (workItem.Status == ExperimentMatrixWorkItemStatus.Completed)
        {
            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.ResumeCompletedItemSkipped,
                "ResumeCompletedItemSkipped",
                "Matrix item skipped because a completed result already exists."));

            return new ExperimentResumeDecision
            {
                WorkItem = workItem,
                ShouldExecute = false,
                RuleDecisions = decisions
            };
        }

        if (workItem.Status == ExperimentMatrixWorkItemStatus.Running)
        {
            var lastActive = workItem.LastHeartbeatAt ?? workItem.StartedAt ?? workItem.CreatedAt;
            var timeout = TimeSpan.FromMinutes(Math.Max(1, config.RunningAttemptTimeoutMinutes));

            if (utcNow - lastActive > timeout)
            {
                workItem.Status = ExperimentMatrixWorkItemStatus.Pending;
                decisions.Add(Decision(
                    GenerationExperimentRuleDefinitions.ResumeStaleRunningReset,
                    "ResumeStaleRunningReset",
                    "Stale running matrix item was reset for retry."));

                return new ExperimentResumeDecision
                {
                    WorkItem = workItem,
                    ShouldExecute = true,
                    RuleDecisions = decisions
                };
            }

            decisions.Add(Decision(
                GenerationExperimentRuleDefinitions.ResumeRunningPreserved,
                "ResumeRunningPreserved",
                "Running matrix item was preserved because it has not exceeded the timeout."));

            return new ExperimentResumeDecision
            {
                WorkItem = workItem,
                ShouldExecute = false,
                RuleDecisions = decisions
            };
        }

        return new ExperimentResumeDecision
        {
            WorkItem = workItem,
            ShouldExecute = true,
            RuleDecisions = decisions
        };
    }

    private static RuleDecisionRecord Decision(
        RuleDefinition rule,
        string value,
        string notes,
        params RuleEvidenceRecord[] evidence)
    {
        return RuleDecisionFactory.CreateDecision(
            "ExperimentResume",
            value,
            rule,
            RuleConfidence.High,
            evidence,
            notes);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
    }
}
