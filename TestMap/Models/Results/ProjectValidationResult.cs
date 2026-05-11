namespace TestMap.Models.Results;

public record ProjectValidationResult(
    string Url,
    string Owner,
    string Repo,
    bool Restores,
    bool Builds,
    bool TestsRun,
    bool TestsPass,
    bool HasCoverage,
    bool HasMutationScore,
    bool HasCandidateMethods,
    int CandidateCount,
    int ExperimentEligibleCandidateCount,
    string DockerContext,
    string DockerOs,
    string ExecutionSupport,
    int UnsupportedProjectCount,
    string UnsupportedProjects,
    string BaselineRunId,
    string FailureCategory,
    string FailureSummary
);
