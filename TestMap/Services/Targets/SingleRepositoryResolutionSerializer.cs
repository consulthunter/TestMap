using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TestMap.Services.Targets;

public sealed class SingleRepositoryResolutionSerializer(
    SingleRepositoryResolutionValidator validator) : ISingleRepositoryResolutionSerializer
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance).DisableAliases().Build();
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

    public byte[] Serialize(SingleRepositoryResolution resolution)
    {
        validator.Validate(resolution);
        return TargetFingerprintService.EncodeDeterministic(_serializer.Serialize(ToDocument(resolution)));
    }

    public SingleRepositoryResolution Deserialize(ReadOnlySpan<byte> yaml)
    {
        var text = new System.Text.UTF8Encoding(false, true).GetString(yaml);
        var document = _deserializer.Deserialize<ResolutionDocument>(text)
            ?? throw new InvalidDataException("Repository resolution record is empty.");
        var resolution = FromDocument(document);
        validator.Validate(resolution);
        return resolution;
    }

    private static ResolutionDocument ToDocument(SingleRepositoryResolution value) => new()
    {
        ResolutionSchemaVersion = value.ResolutionSchemaVersion,
        RequestedAtUtc = value.RequestedAtUtc.ToString("O"),
        CompletedAtUtc = value.CompletedAtUtc.ToString("O"),
        RequestedUrl = value.RequestedUrl,
        RequestedUrlSha256 = value.RequestedUrlSha256,
        Repository = value.Repository,
        CanonicalUrl = value.CanonicalUrl,
        Provider = value.Provider,
        PolicyName = value.PolicyName,
        PolicyVersion = value.PolicyVersion,
        AuthenticationMode = value.AuthenticationMode.ToString().ToLowerInvariant(),
        Status = value.Status.ToString(),
        ResolvedRepository = value.ResolvedRepository,
        DefaultBranch = value.DefaultBranch,
        ResolvedCommit = value.ResolvedCommit,
        ResolutionPasses = value.ResolutionPasses,
        ReasonKind = value.ReasonKind,
        RetryAfterUtc = value.RetryAfterUtc?.ToString("O"),
        Summary = value.Summary
    };

    private static SingleRepositoryResolution FromDocument(ResolutionDocument value) => new(
        value.ResolutionSchemaVersion,
        ParseTime(value.RequestedAtUtc),
        ParseTime(value.CompletedAtUtc),
        value.RequestedUrl ?? string.Empty,
        value.RequestedUrlSha256 ?? string.Empty,
        value.Repository ?? string.Empty,
        value.CanonicalUrl ?? string.Empty,
        value.Provider ?? string.Empty,
        value.PolicyName ?? string.Empty,
        value.PolicyVersion ?? string.Empty,
        ParseEnum<RepositoryAuthenticationMode>(value.AuthenticationMode, "authentication_mode"),
        ParseEnum<SingleRepositoryResolutionStatus>(value.Status, "status"),
        value.ResolvedRepository,
        value.DefaultBranch,
        value.ResolvedCommit,
        value.ResolutionPasses,
        value.ReasonKind,
        string.IsNullOrWhiteSpace(value.RetryAfterUtc) ? null : ParseTime(value.RetryAfterUtc),
        value.Summary ?? string.Empty);

    private static DateTimeOffset ParseTime(string? value) => DateTimeOffset.Parse(
        value ?? string.Empty, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind);

    private static T ParseEnum<T>(string? value, string field) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown {field} '{value}'.");

    private sealed class ResolutionDocument
    {
        public int ResolutionSchemaVersion { get; set; }
        public string? RequestedAtUtc { get; set; }
        public string? CompletedAtUtc { get; set; }
        public string? RequestedUrl { get; set; }
        public string? RequestedUrlSha256 { get; set; }
        public string? Repository { get; set; }
        public string? CanonicalUrl { get; set; }
        public string? Provider { get; set; }
        public string? PolicyName { get; set; }
        public string? PolicyVersion { get; set; }
        public string? AuthenticationMode { get; set; }
        public string? Status { get; set; }
        public string? ResolvedRepository { get; set; }
        public string? DefaultBranch { get; set; }
        public string? ResolvedCommit { get; set; }
        public int ResolutionPasses { get; set; }
        public string? ReasonKind { get; set; }
        public string? RetryAfterUtc { get; set; }
        public string? Summary { get; set; }
    }
}
