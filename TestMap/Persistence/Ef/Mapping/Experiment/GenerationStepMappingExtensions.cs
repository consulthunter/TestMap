using System.Text.Json;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class GenerationStepMappingExtensions
{
    public static GenerationStep ToDomain(this GenerationStepEntity entity)
    {
        var persistence = DeserializeMetadata(entity.ValidationResult);
        return new GenerationStep
        {
            Id = entity.Id,
            GenerationAttemptId = entity.GenerationAttemptId,
            StepType = Enum.TryParse<GenerationStepType>(entity.StepName, true, out var stepType)
                ? stepType
                : GenerationStepType.Scenario,
            Status = Enum.TryParse<GenerationStepStatus>(entity.Status, true, out var status)
                ? status
                : GenerationStepStatus.Executed,
            SkipReason = EmptyToNull(entity.SkipReason),
            Prompt = entity.Prompt ?? string.Empty,
            Response = entity.Response ?? string.Empty,
            TokenCount = entity.TokensUsed,
            DurationSeconds = entity.EndTime.HasValue ? (entity.EndTime.Value - entity.StartTime).TotalSeconds : 0,
            StartedAt = entity.StartTime,
            CompletedAt = entity.EndTime,
            Success = entity.Success,
            ErrorMessage = entity.ErrorMessage,
            ResponseFormat = persistence?.ResponseFormat,
            StructuredResponseJson = persistence?.StructuredResponseJson,
            PromptVersion = persistence?.PromptVersion,
            ValidationStatus = persistence?.ValidationStatus,
            InputTokens = entity.InputTokens,
            OutputTokens = entity.OutputTokens,
            RuleDecisionJson = entity.RuleDecisionJson
        };
    }

    public static GenerationStepEntity ToEntity(this GenerationStep step)
    {
        var start = step.StartedAt == default ? DateTime.UtcNow : step.StartedAt;
        var end = step.CompletedAt ?? (step.DurationSeconds > 0 ? start.AddSeconds(step.DurationSeconds) : start);
        return new GenerationStepEntity
        {
            Id = step.Id,
            GenerationAttemptId = step.GenerationAttemptId,
            StepName = step.StepType.ToString(),
            Status = step.Status.ToString(),
            SkipReason = step.SkipReason ?? string.Empty,
            StepOrder = (int)step.StepType,
            StartTime = start,
            EndTime = end,
            Prompt = step.Prompt,
            Response = step.Response,
            TokensUsed = step.TokenCount,
            Success = step.Success,
            ErrorMessage = step.ErrorMessage ?? string.Empty,
            ValidationResult = SerializeMetadata(step),
            InputTokens = step.InputTokens,
            OutputTokens = step.OutputTokens,
            RuleDecisionJson = step.RuleDecisionJson
        };
    }

    private static string SerializeMetadata(GenerationStep step)
    {
        if (string.IsNullOrWhiteSpace(step.ResponseFormat) &&
            string.IsNullOrWhiteSpace(step.StructuredResponseJson) &&
            string.IsNullOrWhiteSpace(step.PromptVersion) &&
            string.IsNullOrWhiteSpace(step.ValidationStatus))
            return string.Empty;

        return JsonSerializer.Serialize(new GenerationStepPersistenceMetadata
        {
            ResponseFormat = step.ResponseFormat,
            StructuredResponseJson = step.StructuredResponseJson,
            PromptVersion = step.PromptVersion,
            ValidationStatus = step.ValidationStatus
        });
    }

    private static GenerationStepPersistenceMetadata? DeserializeMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            return JsonSerializer.Deserialize<GenerationStepPersistenceMetadata>(value);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GenerationStepPersistenceMetadata
    {
        public string? ResponseFormat { get; set; }
        public string? StructuredResponseJson { get; set; }
        public string? PromptVersion { get; set; }
        public string? ValidationStatus { get; set; }
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
