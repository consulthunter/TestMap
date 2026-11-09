using TestMap.Models.Configuration.Experiment;

namespace TestMap.Services.StaticAnalysis.Assertions;

public sealed record AssertionLineagePolicy
{
    public const string CurrentPolicyVersion = "assertion-lineage-v1";
    public const string CurrentCatalogVersion = "assertion-catalog-v1";
    public const int DefaultPathCap = 256;
    public const int MaximumSupportedDepth = 64;

    public bool Enabled { get; init; } = true;
    public int MaxDepth { get; init; } = AssertionLineageEvaluationConfig.DefaultMaxDepth;
    public int PathCap { get; init; } = DefaultPathCap;
    public string PolicyVersion { get; init; } = CurrentPolicyVersion;
    public string CatalogVersion { get; init; } = CurrentCatalogVersion;

    public static AssertionLineagePolicy Resolve(AssertionLineageEvaluationConfig? config)
    {
        config ??= new AssertionLineageEvaluationConfig();
        Validate(config);
        return new AssertionLineagePolicy
        {
            Enabled = config.Enabled,
            MaxDepth = config.MaxDepth
        };
    }

    public static void Validate(AssertionLineageEvaluationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.MaxDepth is < 1 or > MaximumSupportedDepth)
            throw new ArgumentOutOfRangeException(
                nameof(config.MaxDepth),
                config.MaxDepth,
                $"Assertion-lineage MaxDepth must be between 1 and {MaximumSupportedDepth}.");
    }

    public static bool TryValidate(AssertionLineageEvaluationConfig? config, out string? error)
    {
        try
        {
            Validate(config ?? new AssertionLineageEvaluationConfig());
            error = null;
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
