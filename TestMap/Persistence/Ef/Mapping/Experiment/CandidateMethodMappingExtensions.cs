using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Entities.Experiment;

namespace TestMap.Persistence.Ef.Mapping.Experiment;

public static class CandidateMethodMappingExtensions
{
    public static CandidateMethod ToDomain(this CandidateMethodEntity entity)
    {
        return new CandidateMethod
        {
            Id = entity.Id,
            ExperimentRunId = entity.ExperimentRunId,
            MemberId = entity.SourceMemberId,
            ExistingTestMemberId = entity.ExistingTestMemberId,
            MethodName = entity.SourceMethodName,
            Signature = entity.SourceMethodSignature,
            ExistingTestMethodName = entity.ExistingTestMethodName,
            BaselineCoverage = entity.InitialCoverage,
            ComplexityScore = 0.0,
            SelectionTime = entity.SelectionTime
        };
    }

    public static CandidateMethodEntity ToEntity(this CandidateMethod candidateMethod)
    {
        return new CandidateMethodEntity
        {
            Id = candidateMethod.Id,
            ExperimentRunId = candidateMethod.ExperimentRunId,
            SourceMemberId = candidateMethod.MemberId,
            ExistingTestMemberId = candidateMethod.ExistingTestMemberId,
            SourceMethodName = candidateMethod.MethodName,
            SourceMethodSignature = candidateMethod.Signature,
            ExistingTestMethodName = candidateMethod.ExistingTestMethodName ?? string.Empty,
            InitialCoverage = candidateMethod.BaselineCoverage,
            InitialCoveredLines = 0,
            InitialTotalLines = 0,
            SelectionTime = candidateMethod.SelectionTime == default ? DateTime.UtcNow : candidateMethod.SelectionTime
        };
    }
}
