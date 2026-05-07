using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;

namespace TestMap.Services.Experiment.Execution;

public interface IExperimentResumeService
{
    string BuildStableKey(
        string resumeGroupId,
        string repositoryIdentity,
        string commitHash,
        TestGenerationObjective objective,
        CandidateMethod candidateMethod,
        GenerationExperimentMatrixItem matrixItem);

    ExperimentMatrixWorkItem CreateWorkItem(
        int experimentRunId,
        string resumeGroupId,
        string repositoryIdentity,
        string commitHash,
        TestGenerationObjective objective,
        CandidateMethod candidateMethod,
        GenerationExperimentMatrixItem matrixItem);

    ExperimentResumeDecision Evaluate(
        ExperimentMatrixWorkItem workItem,
        ExperimentResumeConfig config,
        DateTime utcNow);
}
