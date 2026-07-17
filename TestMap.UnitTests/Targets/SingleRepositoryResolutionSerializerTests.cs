using System.Text;
using TestMap.Models.Targets;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class SingleRepositoryResolutionSerializerTests
{
    [Fact]
    public void Serialize_IsDeterministicAndRoundTripsAllProvenance()
    {
        var serializer = CreateSerializer();
        var resolution = Resolution();

        var first = serializer.Serialize(resolution);
        var second = serializer.Serialize(resolution);
        var roundTrip = serializer.Deserialize(first);

        Assert.Equal(first, second);
        Assert.Equal(resolution, roundTrip);
        Assert.DoesNotContain("\r\n", Encoding.UTF8.GetString(first), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SingleRepositoryResolutionStatus.RepositoryUnavailable)]
    [InlineData(SingleRepositoryResolutionStatus.AuthenticationFailed)]
    [InlineData(SingleRepositoryResolutionStatus.AuthorizationFailed)]
    [InlineData(SingleRepositoryResolutionStatus.RateLimited)]
    [InlineData(SingleRepositoryResolutionStatus.EmptyRepository)]
    [InlineData(SingleRepositoryResolutionStatus.DefaultBranchUnavailable)]
    [InlineData(SingleRepositoryResolutionStatus.CommitUnavailable)]
    [InlineData(SingleRepositoryResolutionStatus.ServiceUnavailable)]
    [InlineData(SingleRepositoryResolutionStatus.ResolutionFailed)]
    public void Serialize_FailureStatus_RoundTrips(SingleRepositoryResolutionStatus status)
    {
        var failure = Resolution() with
        {
            Status = status,
            ResolvedRepository = null,
            DefaultBranch = null,
            ResolvedCommit = null,
            ReasonKind = "resolution_failed",
            RetryAfterUtc = status == SingleRepositoryResolutionStatus.RateLimited
                ? new DateTimeOffset(2026, 7, 16, 1, 0, 0, TimeSpan.Zero)
                : null
        };

        Assert.Equal(failure, CreateSerializer().Deserialize(CreateSerializer().Serialize(failure)));
    }

    [Fact]
    public void Deserialize_UnknownStatus_Throws()
    {
        var yaml = Encoding.UTF8.GetString(CreateSerializer().Serialize(Resolution()))
            .Replace("status: Resolved", "status: FutureStatus", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            CreateSerializer().Deserialize(Encoding.UTF8.GetBytes(yaml)));
    }

    [Fact]
    public void Serialize_NonRateLimitedRetryTime_Throws()
    {
        var invalid = Resolution() with
        {
            Status = SingleRepositoryResolutionStatus.ServiceUnavailable,
            ResolvedRepository = null,
            DefaultBranch = null,
            ResolvedCommit = null,
            ReasonKind = "service_unavailable",
            RetryAfterUtc = new DateTimeOffset(2026, 7, 16, 1, 0, 0, TimeSpan.Zero)
        };

        Assert.Throws<InvalidDataException>(() => CreateSerializer().Serialize(invalid));
    }

    [Fact]
    public void Serialize_HashCorrectUrlForDifferentRepository_Throws()
    {
        const string otherUrl = "https://github.com/other/repository";
        var fingerprint = new TargetFingerprintService();
        var invalid = Resolution() with
        {
            RequestedUrl = otherUrl,
            RequestedUrlSha256 = fingerprint.ComputeSha256(Encoding.UTF8.GetBytes(otherUrl))
        };

        Assert.Throws<InvalidDataException>(() => CreateSerializer().Serialize(invalid));
    }

    private static SingleRepositoryResolutionSerializer CreateSerializer()
    {
        var fingerprint = new TargetFingerprintService();
        return new SingleRepositoryResolutionSerializer(new SingleRepositoryResolutionValidator(fingerprint));
    }

    private static SingleRepositoryResolution Resolution()
    {
        const string url = "https://github.com/owner/repository";
        var fingerprint = new TargetFingerprintService();
        var requestedAt = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
        return new SingleRepositoryResolution(
            1,
            requestedAt,
            requestedAt.AddSeconds(1),
            url,
            fingerprint.ComputeSha256(Encoding.UTF8.GetBytes(url)),
            "owner/repository",
            "https://github.com/owner/repository.git",
            SingleRepositoryResolutionPolicy.Provider,
            SingleRepositoryResolutionPolicy.Name,
            SingleRepositoryResolutionPolicy.Version,
            RepositoryAuthenticationMode.Anonymous,
            SingleRepositoryResolutionStatus.Resolved,
            "owner/repository",
            "main",
            TargetTestData.Commit,
            1,
            null,
            null,
            "Resolved exact default-branch head.");
    }
}
