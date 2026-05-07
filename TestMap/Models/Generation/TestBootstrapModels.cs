namespace TestMap.Models.Generation;

public enum BootstrapTestType
{
    Unknown,
    Unit,
    Integration,
    System
}

public sealed class TestBootstrapDetectionResult
{
    public bool ShouldBootstrap { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool HasTestProjects { get; init; }
    public bool HasDiscoveredTestMembers { get; init; }
}

public sealed class TestBootstrapRequest
{
    public required string SourceProjectPath { get; init; }
    public string? SolutionPath { get; init; }
    public required string Framework { get; init; }
    public required string TestProjectSuffix { get; init; }
    public bool AddCoverletCollector { get; init; }
    public int InitialCandidateLimit { get; init; }
}

public sealed class TestProjectBootstrapResult
{
    public bool Success { get; init; }
    public string TestProjectName { get; init; } = string.Empty;
    public string TestProjectPath { get; init; } = string.Empty;
    public string? SolutionPath { get; init; }
    public IReadOnlyList<string> PackageReferences { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PlannedOperations { get; init; } = Array.Empty<string>();
}

public sealed class TestProjectScaffoldingResult
{
    public bool Success { get; init; }
    public string TestClassName { get; init; } = string.Empty;
    public string TestFilePath { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public string ScaffoldPreview { get; init; } = string.Empty;
}

public sealed class TestTypeClassificationResult
{
    public BootstrapTestType TestType { get; init; } = BootstrapTestType.Unit;
    public string Reason { get; init; } = string.Empty;
}

public sealed class TestBootstrapPlan
{
    public required TestBootstrapDetectionResult Detection { get; init; }
    public TestProjectBootstrapResult? ProjectBootstrap { get; init; }
    public TestProjectScaffoldingResult? InitialScaffold { get; init; }
}

public sealed class TestBootstrapRuntimeState
{
    public required string SourceProjectPath { get; init; }
    public required string TestProjectPath { get; init; }
    public required string TestProjectName { get; init; }
    public required string TestClassName { get; init; }
    public required string TestFilePath { get; init; }
    public required string Framework { get; init; }
    public required string TargetFramework { get; init; }
    public required string Dependencies { get; init; }
}