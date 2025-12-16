namespace TestMap.Models.Results;

public record ProjectValidationResult(
    string ProjectName,
    bool HasCoverage,
    bool HasMutationReports,
    bool HasFileCodeMetrics,
    bool HasFunctionCodeMetrics
);
