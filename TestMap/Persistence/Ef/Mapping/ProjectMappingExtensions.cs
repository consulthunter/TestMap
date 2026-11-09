using TestMap.Models;
using TestMap.Persistence.Ef.Entities;
using TestMap.Models.Targets;

namespace TestMap.Persistence.Ef.Mapping;

public static class ProjectMappingExtensions
{
    public static ProjectEntity ToEntity(this ProjectModel project)
    {
        return new ProjectEntity
        {
            Owner = project.Owner,
            RepoName = project.RepoName,
            WebUrl = project.GitHubUrl,
            Branch = project.Branch,
            LastAnalyzedCommit = project.LastAnalyzedCommit,
            DatabasePath = project.DatabasePath,
            ContentHash = project.ContentHash,
            DirectoryPath = project.DirectoryPath,
            TargetId = project.MaterializedRevision?.TargetId,
            RepositoryIdentity = project.MaterializedRevision?.RepositoryIdentity,
            RequestedCommit = project.MaterializedRevision?.RequestedCommit,
            ResolvedCommit = project.MaterializedRevision?.ResolvedCommit,
            TargetManifestSha256 = project.MaterializedRevision?.ManifestSha256,
            TargetSourceSha256 = project.MaterializedRevision?.SourceSha256,
            MaterializedAtUtc = project.MaterializedRevision?.MaterializedAtUtc?.UtcDateTime,
            ProvenancePolicyVersion = project.MaterializedRevision?.PolicyVersion
        };
    }

    public static ProjectModel ToDomain(this ProjectEntity project)
    {
        var model = new ProjectModel(
            gitHubUrl: project.WebUrl ?? string.Empty,
            owner: project.Owner,
            repoName: project.RepoName,
            directoryPath: project.DirectoryPath,
            databasePath: project.DatabasePath)
        {
            DbId = project.Id,
            Branch = project.Branch,
            LastAnalyzedCommit = project.LastAnalyzedCommit,
            Commit = project.ResolvedCommit
        };
        if (project.TargetId is not null && project.RepositoryIdentity is not null && project.RequestedCommit is not null)
        {
            var target = new RepositoryTarget(
                project.TargetId, project.RepositoryIdentity, project.WebUrl ?? string.Empty,
                project.RequestedCommit, []);
            model.BindTarget(target);
            model.MaterializedRevision = new MaterializedRevision(
                project.TargetId, project.RepositoryIdentity, project.RequestedCommit, project.ResolvedCommit,
                project.WebUrl,
                new TargetPaths(project.DirectoryPath, project.DatabasePath ?? string.Empty, string.Empty, string.Empty),
                project.TargetManifestSha256 ?? string.Empty,
                project.TargetSourceSha256 ?? string.Empty,
                project.MaterializedAtUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(project.MaterializedAtUtc.Value, DateTimeKind.Utc)),
                project.ResolvedCommit is null ? MaterializationStatus.Pending : MaterializationStatus.Available,
                project.ProvenancePolicyVersion ?? "pinned-target-v1");
        }
        return model;
    }
}
