using TestMap.Models;
using TestMap.Models.Targets;
using TestMap.Persistence.Ef.Mapping;
using TestMap.Services.Targets;

namespace TestMap.UnitTests.Persistence;

public sealed class PinnedProjectProvenanceMappingTests
{
    [Fact]
    public void ProjectMapping_RoundTripsPinnedIdentity()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var target = new TargetIdentityService().Create("owner/repository", commit, [1]);
        var project = new ProjectModel(target.Url, "owner", "repository", directoryPath: "workspace", databasePath: "analysis.db");
        project.BindTarget(target);
        project.MaterializedRevision = new MaterializedRevision(
            target.TargetId, target.Repository, commit, commit, target.Url,
            new TargetPaths("workspace", "analysis.db", "artifacts", "run.log"),
            new string('a', 64), new string('b', 64), DateTimeOffset.UtcNow, MaterializationStatus.Available);

        var entity = project.ToEntity();
        var roundTrip = entity.ToDomain();

        Assert.Equal(target.TargetId, entity.TargetId);
        Assert.Equal(commit, roundTrip.MaterializedRevision?.ResolvedCommit);
        Assert.Equal(new string('a', 64), roundTrip.MaterializedRevision?.ManifestSha256);
    }
}
