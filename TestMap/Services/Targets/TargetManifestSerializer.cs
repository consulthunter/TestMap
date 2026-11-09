using System.Text.RegularExpressions;
using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TestMap.Services.Targets;

public sealed class TargetManifestSerializer(TargetFingerprintService fingerprint) : ITargetManifestSerializer
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .DisableAliases()
        .Build();

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public byte[] Serialize(TargetManifest manifest)
    {
        Validate(manifest);
        return TargetFingerprintService.EncodeDeterministic(_serializer.Serialize(ToDocument(manifest)));
    }

    public TargetManifest Deserialize(ReadOnlySpan<byte> yaml)
    {
        var text = new System.Text.UTF8Encoding(false, true).GetString(yaml);
        var document = _deserializer.Deserialize<ManifestDocument>(text)
            ?? throw new InvalidDataException("Target manifest is empty.");
        var manifest = FromDocument(document);
        Validate(manifest);
        return manifest;
    }

    public async Task<(TargetManifest Manifest, string Sha256)> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return (Deserialize(bytes), fingerprint.ComputeSha256(bytes));
    }

    private static void Validate(TargetManifest manifest)
    {
        if (manifest.SchemaVersion is not (1 or 2 or 3))
            throw new InvalidDataException("Only target manifest schema versions 1, 2, and 3 are supported.");
        if (manifest.GeneratedAtUtc.Offset != TimeSpan.Zero) throw new InvalidDataException("generated_at_utc must be UTC.");
        ValidateHash(manifest.Source.Sha256, "source.sha256");

        if (manifest.SchemaVersion == 1)
        {
            ValidateSourceFile(manifest.Source);
            if (manifest.Rejections is null || manifest.Derivation is not null || manifest.Resolution is not null)
                throw new InvalidDataException("Schema 1 requires rejections and forbids derivation and resolution provenance.");
            ValidateReportReference(manifest.Rejections, "rejections");
            if (manifest.Targets.Count == 0)
                throw new InvalidDataException("A source target manifest must contain at least one target.");
        }
        else if (manifest.SchemaVersion == 2)
        {
            ValidateSourceFile(manifest.Source);
            if (manifest.Rejections is not null || manifest.Derivation is null || manifest.Resolution is not null)
                throw new InvalidDataException("Schema 2 requires derivation provenance and forbids rejections and resolution provenance.");
            ValidateDerivation(manifest.Derivation);
        }
        else
        {
            if (manifest.Rejections is not null || manifest.Derivation is not null || manifest.Resolution is null)
                throw new InvalidDataException("Schema 3 requires resolution provenance and forbids rejections and derivation.");
            if (manifest.Source.File is not null || manifest.Source.Kind != "github_repository_url" ||
                string.IsNullOrWhiteSpace(manifest.Source.Url))
                throw new InvalidDataException("Schema 3 requires a GitHub repository URL source and no source file.");
            ValidateReportReference(manifest.Resolution, "resolution");
            if (manifest.Targets.Count != 1 || !manifest.Targets[0].SourceRows.SequenceEqual([1]))
                throw new InvalidDataException("Schema 3 requires exactly one target with source_rows [1].");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var revisions = new HashSet<string>(StringComparer.Ordinal);
        var identity = new TargetIdentityService();
        foreach (var target in manifest.Targets)
        {
            ValidateHash(target.TargetId, "target_id");
            if (!identity.TryNormalizeRepository(target.Repository, out var repository) || repository != target.Repository)
                throw new InvalidDataException("Target repository is not normalized.");
            if (!identity.TryNormalizeCommit(target.Commit, out var commit) || commit != target.Commit)
                throw new InvalidDataException("Target commit is not a normalized full SHA.");
            if (target.Url != TargetIdentityService.CanonicalGitHubUrl(repository))
                throw new InvalidDataException("Target URL is not canonical.");
            if (target.TargetId != TargetIdentityService.CreateTargetId(repository, commit))
                throw new InvalidDataException("Target ID does not match repository and commit.");
            if (!ids.Add(target.TargetId) || !revisions.Add(repository + "@" + commit))
                throw new InvalidDataException("Target manifest contains a duplicate target.");
            if (target.SourceRows.Count == 0 || target.SourceRows.Any(row => row < 1) || !target.SourceRows.SequenceEqual(target.SourceRows.Distinct().Order()))
                throw new InvalidDataException("Target source_rows must be sorted, distinct, positive logical records.");
        }
    }

    private static void ValidateSourceFile(TargetSourceProvenance source)
    {
        if (Path.GetFileName(source.File) != source.File || string.IsNullOrWhiteSpace(source.File) ||
            source.Kind is not null || source.Url is not null)
            throw new InvalidDataException("File-backed target source provenance is invalid.");
    }

    private static void ValidateDerivation(TargetDerivationProvenance derivation)
    {
        if (derivation.Kind != "project_test_presence")
            throw new InvalidDataException("Unsupported target derivation kind.");
        if (derivation.Category is not ("tests_detected" or "no_tests_detected"))
            throw new InvalidDataException("Unsupported target derivation category.");
        if (derivation.PolicyName != ProjectCheckPolicy.Name || derivation.PolicyVersion != ProjectCheckPolicy.Version)
            throw new InvalidDataException("Unsupported target derivation policy.");
        ValidateReportReference(derivation.Report, "derivation.report");
    }

    private static void ValidateReportReference(TargetReportReference report, string field)
    {
        ValidateHash(report.Sha256, field + ".sha256");
        if (Path.GetFileName(report.File) != report.File || string.IsNullOrWhiteSpace(report.File))
            throw new InvalidDataException(field + ".file must be a basename.");
    }

    private static void ValidateHash(string value, string field)
    {
        if (!Regex.IsMatch(value ?? string.Empty, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException($"{field} must be a lower-case SHA-256 value.");
    }

    private static ManifestDocument ToDocument(TargetManifest value) => new()
    {
        SchemaVersion = value.SchemaVersion,
        GeneratedAtUtc = value.GeneratedAtUtc.ToString("O"),
        Source = new SourceDocument
        {
            File = value.Source.File,
            Sha256 = value.Source.Sha256,
            Kind = value.Source.Kind,
            Url = value.Source.Url
        },
        Rejections = value.Rejections is null ? null : new ReportDocument { File = value.Rejections.File, Sha256 = value.Rejections.Sha256 },
        Resolution = value.Resolution is null ? null : new ReportDocument { File = value.Resolution.File, Sha256 = value.Resolution.Sha256 },
        Derivation = value.Derivation is null ? null : new DerivationDocument
        {
            Kind = value.Derivation.Kind,
            Category = value.Derivation.Category,
            PolicyName = value.Derivation.PolicyName,
            PolicyVersion = value.Derivation.PolicyVersion,
            Report = new ReportDocument { File = value.Derivation.Report.File, Sha256 = value.Derivation.Report.Sha256 }
        },
        Targets = value.Targets.Select(target => new TargetDocument
        {
            TargetId = target.TargetId,
            Repository = target.Repository,
            Url = target.Url,
            Commit = target.Commit,
            SourceRows = target.SourceRows.ToList()
        }).ToList()
    };

    private static TargetManifest FromDocument(ManifestDocument value) => new(
        value.SchemaVersion,
        DateTimeOffset.Parse(value.GeneratedAtUtc ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
        new TargetSourceProvenance(
            value.Source?.File,
            value.Source?.Sha256 ?? string.Empty,
            value.Source?.Kind,
            value.Source?.Url),
        value.Rejections is null ? null : new TargetReportReference(value.Rejections.File ?? string.Empty, value.Rejections.Sha256 ?? string.Empty),
        (value.Targets ?? []).Select(target => new RepositoryTarget(
            target.TargetId ?? string.Empty,
            target.Repository ?? string.Empty,
            target.Url ?? string.Empty,
            target.Commit ?? string.Empty,
            target.SourceRows ?? [])).ToArray(),
        value.Derivation is null ? null : new TargetDerivationProvenance(
            value.Derivation.Kind ?? string.Empty,
            value.Derivation.Category ?? string.Empty,
            value.Derivation.PolicyName ?? string.Empty,
            value.Derivation.PolicyVersion ?? string.Empty,
            new TargetReportReference(value.Derivation.Report?.File ?? string.Empty, value.Derivation.Report?.Sha256 ?? string.Empty)),
        value.Resolution is null ? null : new TargetReportReference(
            value.Resolution.File ?? string.Empty, value.Resolution.Sha256 ?? string.Empty));

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; set; }
        public string? GeneratedAtUtc { get; set; }
        public SourceDocument? Source { get; set; }
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public ReportDocument? Rejections { get; set; }
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public ReportDocument? Resolution { get; set; }
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public DerivationDocument? Derivation { get; set; }
        public List<TargetDocument>? Targets { get; set; }
    }
    private sealed class SourceDocument
    {
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? File { get; set; }
        public string? Sha256 { get; set; }
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Kind { get; set; }
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Url { get; set; }
    }
    private sealed class ReportDocument { public string? File { get; set; } public string? Sha256 { get; set; } }
    private sealed class DerivationDocument
    {
        public string? Kind { get; set; }
        public string? Category { get; set; }
        public string? PolicyName { get; set; }
        public string? PolicyVersion { get; set; }
        public ReportDocument? Report { get; set; }
    }
    private sealed class TargetDocument
    {
        public string? TargetId { get; set; }
        public string? Repository { get; set; }
        public string? Url { get; set; }
        public string? Commit { get; set; }
        public List<int>? SourceRows { get; set; }
    }
}
