using TestMap.Models.Targets;
using TestMap.Services.ProjectDiscovery.Contracts;
using TestMap.Services.Targets;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TestMap.Services.ProjectDiscovery;

public sealed class ProjectCheckSerializer(ProjectCheckContractValidator validator) : IProjectCheckSerializer
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .DisableAliases()
        .Build();
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public byte[] SerializeReport(ProjectCheckReport report)
    {
        validator.ValidateReport(report);
        return TargetFingerprintService.EncodeDeterministic(_serializer.Serialize(ToDocument(report)));
    }

    public ProjectCheckReport DeserializeReport(ReadOnlySpan<byte> yaml)
    {
        var report = FromDocument(Deserialize<ReportDocument>(yaml));
        validator.ValidateReport(report);
        return report;
    }

    public byte[] SerializeBundle(ProjectCheckBundle bundle)
    {
        validator.ValidateBundle(bundle);
        return TargetFingerprintService.EncodeDeterministic(_serializer.Serialize(ToDocument(bundle)));
    }

    public ProjectCheckBundle DeserializeBundle(ReadOnlySpan<byte> yaml)
    {
        var bundle = FromDocument(Deserialize<BundleDocument>(yaml));
        validator.ValidateBundle(bundle);
        return bundle;
    }

    private T Deserialize<T>(ReadOnlySpan<byte> yaml)
    {
        var text = new System.Text.UTF8Encoding(false, true).GetString(yaml);
        return _deserializer.Deserialize<T>(text) ?? throw new InvalidDataException("YAML document is empty.");
    }

    private static ReportDocument ToDocument(ProjectCheckReport report) => new()
    {
        ReportSchemaVersion = report.ReportSchemaVersion,
        GeneratedAtUtc = report.GeneratedAtUtc.ToString("O"),
        Input = ToDocument(report.Input),
        Policy = ToDocument(report.Policy),
        Summary = new SummaryDocument
        {
            InputTargets = report.Summary.InputTargets,
            TestsDetected = report.Summary.TestsDetected,
            NoTestsDetected = report.Summary.NoTestsDetected,
            Indeterminate = report.Summary.Indeterminate,
            ByStatus = Enum.GetValues<ProjectCheckStatus>().ToDictionary(
                status => status.ToString(), status => report.Summary.ByStatus[status], StringComparer.Ordinal)
        },
        Observations = report.Observations.Select(row => new ObservationDocument
        {
            TargetId = row.TargetId,
            Repository = row.Repository,
            RequestedCommit = row.RequestedCommit,
            ObservedCommit = row.ObservedCommit,
            Status = row.Status.ToString(),
            PolicyName = row.PolicyName,
            PolicyVersion = row.PolicyVersion,
            EvidenceCategory = row.EvidenceCategory?.ToString(),
            EvidencePath = row.EvidencePath,
            TreeComplete = row.TreeComplete,
            ReasonKind = row.ReasonKind,
            Summary = row.Summary,
            CheckedAtUtc = row.CheckedAtUtc.ToString("O")
        }).ToList()
    };

    private static ProjectCheckReport FromDocument(ReportDocument document) => new(
        document.ReportSchemaVersion,
        ParseTimestamp(document.GeneratedAtUtc),
        FromDocument(document.Input),
        FromDocument(document.Policy),
        new ProjectCheckSummary(
            document.Summary?.InputTargets ?? -1,
            document.Summary?.TestsDetected ?? -1,
            document.Summary?.NoTestsDetected ?? -1,
            document.Summary?.Indeterminate ?? -1,
            ParseStatusCounts(document.Summary?.ByStatus)),
        (document.Observations ?? []).Select(row => new ProjectCheckObservation(
            row.TargetId ?? string.Empty,
            row.Repository ?? string.Empty,
            row.RequestedCommit ?? string.Empty,
            row.ObservedCommit,
            ParseEnum<ProjectCheckStatus>(row.Status, "status"),
            row.PolicyName ?? string.Empty,
            row.PolicyVersion ?? string.Empty,
            string.IsNullOrWhiteSpace(row.EvidenceCategory)
                ? null
                : ParseEnum<ProjectCheckEvidenceCategory>(row.EvidenceCategory, "evidence_category"),
            row.EvidencePath,
            row.TreeComplete,
            row.ReasonKind,
            row.Summary ?? string.Empty,
            ParseTimestamp(row.CheckedAtUtc))).ToArray());

    private static BundleDocument ToDocument(ProjectCheckBundle bundle) => new()
    {
        BundleSchemaVersion = bundle.BundleSchemaVersion,
        GeneratedAtUtc = bundle.GeneratedAtUtc.ToString("O"),
        Input = ToDocument(bundle.Input),
        Policy = ToDocument(bundle.Policy),
        Complete = bundle.Complete,
        ClassificationComplete = bundle.ClassificationComplete,
        Artifacts = new ArtifactsDocument
        {
            Report = ToDocument(bundle.Artifacts.Report),
            TestsDetected = ToDocument(bundle.Artifacts.TestsDetected),
            NoTestsDetected = ToDocument(bundle.Artifacts.NoTestsDetected)
        }
    };

    private static ProjectCheckBundle FromDocument(BundleDocument document) => new(
        document.BundleSchemaVersion,
        ParseTimestamp(document.GeneratedAtUtc),
        FromDocument(document.Input),
        FromDocument(document.Policy),
        document.Complete,
        document.ClassificationComplete,
        new ProjectCheckArtifacts(
            FromDocument(document.Artifacts?.Report),
            FromDocument(document.Artifacts?.TestsDetected),
            FromDocument(document.Artifacts?.NoTestsDetected)));

    private static InputDocument ToDocument(ProjectCheckInputReference input) => new()
        { File = input.File, Sha256 = input.Sha256, SchemaVersion = input.SchemaVersion };
    private static ProjectCheckInputReference FromDocument(InputDocument? input) => new(
        input?.File ?? string.Empty, input?.Sha256 ?? string.Empty, input?.SchemaVersion ?? 0);
    private static PolicyDocument ToDocument(ProjectCheckPolicyReference policy) => new()
        { Name = policy.Name, Version = policy.Version };
    private static ProjectCheckPolicyReference FromDocument(PolicyDocument? policy) => new(
        policy?.Name ?? string.Empty, policy?.Version ?? string.Empty);
    private static ArtifactDocument ToDocument(ProjectCheckArtifactReference artifact) => new()
        { File = artifact.File, Sha256 = artifact.Sha256, Records = artifact.Records };
    private static ProjectCheckArtifactReference FromDocument(ArtifactDocument? artifact) => new(
        artifact?.File ?? string.Empty, artifact?.Sha256 ?? string.Empty, artifact?.Records ?? -1);

    private static DateTimeOffset ParseTimestamp(string? value) => DateTimeOffset.Parse(
        value ?? string.Empty,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind);

    private static T ParseEnum<T>(string? value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, false, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown {field} '{value}'.");

    private static IReadOnlyDictionary<ProjectCheckStatus, int> ParseStatusCounts(Dictionary<string, int>? values)
    {
        if (values is null) return new Dictionary<ProjectCheckStatus, int>();
        return values.ToDictionary(
            pair => ParseEnum<ProjectCheckStatus>(pair.Key, "status count"),
            pair => pair.Value);
    }

    private sealed class ReportDocument
    {
        public int ReportSchemaVersion { get; set; }
        public string? GeneratedAtUtc { get; set; }
        public InputDocument? Input { get; set; }
        public PolicyDocument? Policy { get; set; }
        public SummaryDocument? Summary { get; set; }
        public List<ObservationDocument>? Observations { get; set; }
    }

    private sealed class SummaryDocument
    {
        public int InputTargets { get; set; }
        public int TestsDetected { get; set; }
        public int NoTestsDetected { get; set; }
        public int Indeterminate { get; set; }
        public Dictionary<string, int>? ByStatus { get; set; }
    }

    private sealed class ObservationDocument
    {
        public string? TargetId { get; set; }
        public string? Repository { get; set; }
        public string? RequestedCommit { get; set; }
        public string? ObservedCommit { get; set; }
        public string? Status { get; set; }
        public string? PolicyName { get; set; }
        public string? PolicyVersion { get; set; }
        public string? EvidenceCategory { get; set; }
        public string? EvidencePath { get; set; }
        public bool? TreeComplete { get; set; }
        public string? ReasonKind { get; set; }
        public string? Summary { get; set; }
        public string? CheckedAtUtc { get; set; }
    }

    private sealed class BundleDocument
    {
        public int BundleSchemaVersion { get; set; }
        public string? GeneratedAtUtc { get; set; }
        public InputDocument? Input { get; set; }
        public PolicyDocument? Policy { get; set; }
        public bool Complete { get; set; }
        public bool ClassificationComplete { get; set; }
        public ArtifactsDocument? Artifacts { get; set; }
    }

    private sealed class InputDocument { public string? File { get; set; } public string? Sha256 { get; set; } public int SchemaVersion { get; set; } }
    private sealed class PolicyDocument { public string? Name { get; set; } public string? Version { get; set; } }
    private sealed class ArtifactsDocument
    {
        public ArtifactDocument? Report { get; set; }
        public ArtifactDocument? TestsDetected { get; set; }
        public ArtifactDocument? NoTestsDetected { get; set; }
    }
    private sealed class ArtifactDocument { public string? File { get; set; } public string? Sha256 { get; set; } public int Records { get; set; } }
}
