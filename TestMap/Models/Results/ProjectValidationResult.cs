namespace TestMap.Models.Results;

public record ProjectValidationResult(
    string Url,
    string Owner,
    string Repo,
    bool HasXUnit,
    bool HasCoverage,
    bool HasPassingTests,
    bool HasCandidateMethods,
    bool HasMutationReports,
    bool HasFileCodeMetrics,
    bool HasFunctionCodeMetrics
);
