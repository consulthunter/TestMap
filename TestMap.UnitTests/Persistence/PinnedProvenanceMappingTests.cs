using TestMap.Models.AgentTools;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef.Mapping.AgentTools;
using TestMap.Persistence.Ef.Mapping.Experiment;

namespace TestMap.UnitTests.Persistence;

public sealed class PinnedProvenanceMappingTests
{
    [Fact]
    public void ExperimentRunMapping_RoundTripsPinnedProvenance()
    {
        var run = new ExperimentRun
        {
            TargetId = new string('a', 64), RepositoryIdentity = "owner/repository",
            RequestedCommit = new string('b', 40), ResolvedCommit = new string('b', 40),
            TargetManifestSha256 = new string('c', 64), TargetSourceSha256 = new string('d', 64),
            WorkspaceIntegrityStatus = "VerifiedClean", ProvenancePolicyVersion = "pinned-target-v1"
        };
        var roundTrip = run.ToEntity().ToDomain();
        Assert.Equal(run.TargetId, roundTrip.TargetId);
        Assert.Equal(run.ResolvedCommit, roundTrip.ResolvedCommit);
        Assert.Equal("VerifiedClean", roundTrip.WorkspaceIntegrityStatus);
    }

    [Fact]
    public void AttemptMappings_RoundTripBaseCommitAndIntegrity()
    {
        var generation = new GenerationAttempt { BaseCommit = new string('b', 40), WorkspaceIntegrityStatus = "VerifiedClean" };
        var generationRoundTrip = generation.ToEntity().ToDomain();
        Assert.Equal(generation.BaseCommit, generationRoundTrip.BaseCommit);
        Assert.Equal(generation.WorkspaceIntegrityStatus, generationRoundTrip.WorkspaceIntegrityStatus);

        var tool = new ToolAttempt { BaseCommit = new string('b', 40), WorkspaceIntegrityStatus = "RevisionMismatch" };
        var toolRoundTrip = tool.ToEntity().ToDomain();
        Assert.Equal(tool.BaseCommit, toolRoundTrip.BaseCommit);
        Assert.Equal(tool.WorkspaceIntegrityStatus, toolRoundTrip.WorkspaceIntegrityStatus);
    }
}
