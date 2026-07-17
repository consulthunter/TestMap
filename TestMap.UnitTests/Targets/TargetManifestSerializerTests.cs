using TestMap.Services.Targets;
using TestMap.Models.Targets;

namespace TestMap.UnitTests.Targets;

public sealed class TargetManifestSerializerTests
{
    [Fact]
    public void Serialize_IsDeterministicAndRoundTrips()
    {
        var serializer = new TargetManifestSerializer(new TargetFingerprintService());
        var manifest = TargetTestData.Manifest();
        var first = serializer.Serialize(manifest);
        var second = serializer.Serialize(manifest);
        Assert.Equal(first, second);
        var roundTrip = serializer.Deserialize(first);
        Assert.Equal(manifest.SchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(manifest.Targets[0].TargetId, roundTrip.Targets[0].TargetId);
        Assert.Equal(manifest.Targets[0].Repository, roundTrip.Targets[0].Repository);
        Assert.Equal(manifest.Targets[0].SourceRows, roundTrip.Targets[0].SourceRows);
    }

    [Fact]
    public void Serialize_DerivedEmptyManifest_RoundTrips()
    {
        var serializer = new TargetManifestSerializer(new TargetFingerprintService());
        var manifest = new TargetManifest(
            2,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new TargetSourceProvenance("parent.yaml", TargetTestData.Hash),
            null,
            [],
            new TargetDerivationProvenance(
                "project_test_presence", "tests_detected", ProjectCheckPolicy.Name,
                ProjectCheckPolicy.Version, new TargetReportReference("report.yaml", TargetTestData.Hash)));

        var roundTrip = serializer.Deserialize(serializer.Serialize(manifest));

        Assert.Equal(2, roundTrip.SchemaVersion);
        Assert.Empty(roundTrip.Targets);
        Assert.Null(roundTrip.Rejections);
        Assert.Equal("tests_detected", roundTrip.Derivation?.Category);
    }

    [Fact]
    public void Serialize_MixedSchemaProvenance_Throws()
    {
        var manifest = TargetTestData.Manifest() with
        {
            Derivation = new TargetDerivationProvenance(
                "project_test_presence", "tests_detected", ProjectCheckPolicy.Name,
                ProjectCheckPolicy.Version, new TargetReportReference("report.yaml", TargetTestData.Hash))
        };

        Assert.Throws<InvalidDataException>(() =>
            new TargetManifestSerializer(new TargetFingerprintService()).Serialize(manifest));
    }

    [Fact]
    public void Serialize_UrlTargetManifest_RoundTripsSchema3()
    {
        var manifest = new TargetManifest(
            3,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new TargetSourceProvenance(
                null, TargetTestData.Hash, "github_repository_url",
                "https://github.com/owner/repository"),
            null,
            [TargetTestData.Target()],
            null,
            new TargetReportReference("target-resolution-9f86d081884c.yaml", TargetTestData.Hash));
        var serializer = new TargetManifestSerializer(new TargetFingerprintService());

        var roundTrip = serializer.Deserialize(serializer.Serialize(manifest));

        Assert.Equal(3, roundTrip.SchemaVersion);
        Assert.Equal("github_repository_url", roundTrip.Source.Kind);
        Assert.Equal("https://github.com/owner/repository", roundTrip.Source.Url);
        Assert.Equal(TargetTestData.Hash, roundTrip.Resolution?.Sha256);
        Assert.Single(roundTrip.Targets);
    }

    [Fact]
    public void Serialize_UrlTargetManifest_WithFileOrMultipleTargets_Throws()
    {
        var manifest = new TargetManifest(
            3,
            new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
            new TargetSourceProvenance(
                "source.csv", TargetTestData.Hash, "github_repository_url",
                "https://github.com/owner/repository"),
            null,
            [TargetTestData.Target(), TargetTestData.Target(TargetTestData.OtherCommit)],
            null,
            new TargetReportReference("resolution.yaml", TargetTestData.Hash));

        Assert.Throws<InvalidDataException>(() =>
            new TargetManifestSerializer(new TargetFingerprintService()).Serialize(manifest));
    }

    [Theory]
    [InlineData("invalid-version.yaml")]
    [InlineData("duplicate-target.yaml")]
    [InlineData("malformed.yaml")]
    public void Deserialize_InvalidManifest_Throws(string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Targets", "Manifests", file));
        Assert.ThrowsAny<Exception>(() => new TargetManifestSerializer(new TargetFingerprintService()).Deserialize(bytes));
    }
}
