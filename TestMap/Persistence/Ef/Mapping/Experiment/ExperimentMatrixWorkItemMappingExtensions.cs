using TestMap.Models.Configuration.AiProviders;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class ExperimentMatrixWorkItemMappingExtensions
{
    public static ExperimentMatrixWorkItem ToDomain(this ExperimentMatrixWorkItemEntity entity)
    {
        return new ExperimentMatrixWorkItem
        {
            Id = entity.Id,
            ExperimentRunId = entity.ExperimentRunId,
            CandidateMethodId = entity.CandidateMethodId,
            MemberId = entity.MemberId,
            StableKey = entity.StableKey,
            Status = entity.Status,
            Provider = Enum.TryParse<AiProvider>(entity.ProviderName, true, out var provider) ? provider : AiProvider.OpenAi,
            ModelName = entity.ModelName,
            Objective = Enum.TryParse<TestGenerationObjective>(entity.Objective, true, out var objective) ? objective : TestGenerationObjective.TestSuiteExpansion,
            Approach = Enum.TryParse<TestGenerationApproach>(entity.Approach, true, out var approach) ? approach : TestGenerationApproach.MetricsDriven,
            MetricsPath = Enum.TryParse<MetricsDrivenPath>(entity.MetricsPath, true, out var metricsPath) ? metricsPath : null,
            ContextMode = Enum.TryParse<GenerationContextMode>(entity.ContextMode, true, out var contextMode) ? contextMode : GenerationContextMode.ChainedHistory,
            BudgetMode = Enum.TryParse<GenerationBudgetMode>(entity.BudgetMode, true, out var budgetMode) ? budgetMode : GenerationBudgetMode.PassAt1,
            AblationVariantId = entity.AblationVariantId,
            StepConfigJson = entity.StepConfigJson,
            CreatedAt = entity.CreatedAt,
            StartedAt = entity.StartedAt,
            LastHeartbeatAt = entity.LastHeartbeatAt,
            CompletedAt = entity.CompletedAt,
            ErrorMessage = entity.ErrorMessage
        };
    }

    public static ExperimentMatrixWorkItemEntity ToEntity(this ExperimentMatrixWorkItem item)
    {
        return new ExperimentMatrixWorkItemEntity
        {
            Id = item.Id,
            ExperimentRunId = item.ExperimentRunId,
            CandidateMethodId = item.CandidateMethodId,
            MemberId = item.MemberId,
            StableKey = item.StableKey,
            Status = item.Status,
            ProviderName = item.Provider.ToString(),
            ModelName = item.ModelName,
            Objective = item.Objective.ToString(),
            Approach = item.Approach.ToString(),
            MetricsPath = item.MetricsPath?.ToString() ?? string.Empty,
            ContextMode = item.ContextMode.ToString(),
            BudgetMode = item.BudgetMode.ToString(),
            AblationVariantId = item.AblationVariantId,
            StepConfigJson = item.StepConfigJson,
            CreatedAt = item.CreatedAt == default ? DateTime.UtcNow : item.CreatedAt,
            StartedAt = item.StartedAt,
            LastHeartbeatAt = item.LastHeartbeatAt,
            CompletedAt = item.CompletedAt,
            ErrorMessage = item.ErrorMessage
        };
    }
}
