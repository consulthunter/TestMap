using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories.Experiment;

namespace TestMap.Services.Experiment.Execution;

public sealed record CandidateCohortResolution(
    CandidateCohort Cohort,
    IReadOnlyList<CandidateMethod> Candidates);

public sealed class CandidateCohortService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TestMapDbContext _dbContext;
    private readonly CandidateCohortRepository _repository;

    public CandidateCohortService(
        TestMapDbContext dbContext,
        CandidateCohortRepository repository)
    {
        _dbContext = dbContext;
        _repository = repository;
    }

    public static void PrepareConfiguration(ExperimentConfig config)
    {
        if (config.CandidateCohort.Mode == CandidateCohortMode.Disabled)
            return;

        if (string.IsNullOrWhiteSpace(config.ExperimentSeriesId))
            config.ExperimentSeriesId = config.CandidateCohort.Id?.Trim();
        if (config.CandidateCohort.Mode == CandidateCohortMode.Create &&
            config.CandidateCohort.Randomize &&
            !config.CandidateCohort.RandomSeed.HasValue)
        {
            config.CandidateCohort.RandomSeed = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
    }

    public static string BuildSelectionConfigurationHash(
        ExperimentConfig config,
        TestMapConfig rootConfig)
    {
        var targetSelection = rootConfig.TestingConfig.GenerationConfig.TargetSelection;
        var selectionConfig = new
        {
            config.Objective,
            Strategy = config.CandidateSelectionStrategy ?? targetSelection.Strategy,
            ContextMode = config.ContextMappingMode ?? targetSelection.ContextMappingMode,
            config.CandidateLimit,
            config.MinCoverageThreshold,
            config.MaxCoverageThreshold
        };
        var json = JsonSerializer.Serialize(selectionConfig);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    public async Task<CandidateCohortResolution> CreateAsync(
        ExperimentConfig config,
        TestMapConfig rootConfig,
        int projectId,
        string repositoryIdentity,
        string commitHash,
        IReadOnlyList<CandidateMethod> candidates,
        CancellationToken cancellationToken = default)
    {
        var cohortKey = RequireCohortKey(config);
        if (await _repository.GetByKeyAsync(projectId, cohortKey, cancellationToken) != null)
            throw new InvalidOperationException(
                $"Candidate cohort '{cohortKey}' already exists for this project. Use mode 'reuse' or choose a new cohort ID.");

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"Candidate cohort '{cohortKey}' cannot be created because candidate selection returned no methods.");

        var identities = await LoadSourceIdentitiesAsync(
            candidates.Select(x => x.MemberId),
            cancellationToken);
        var members = new List<CandidateCohortMember>(candidates.Count);

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!identities.TryGetValue(candidate.MemberId, out var identity))
                throw new InvalidOperationException(
                    $"Cannot freeze candidate '{candidate.MethodName}' because source member '{candidate.MemberId}' no longer exists.");

            members.Add(new CandidateCohortMember
            {
                Ordinal = index + 1,
                SourceMemberId = candidate.MemberId,
                SourceMethodName = candidate.MethodName,
                SourceMethodSignature = candidate.Signature,
                SourceFilePath = identity.SourceFilePath,
                ContainingType = identity.ContainingType,
                SourceContentHash = identity.ContentHash,
                CandidateSnapshotJson = JsonSerializer.Serialize(candidate, SnapshotJsonOptions)
            });
        }

        var cohort = await _repository.InsertAsync(new CandidateCohort
        {
            ProjectId = projectId,
            CohortKey = cohortKey,
            RepositoryIdentity = repositoryIdentity,
            CommitHash = commitHash,
            Objective = config.Objective.ToString(),
            SelectionStrategy = (config.CandidateSelectionStrategy ??
                                 rootConfig.TestingConfig.GenerationConfig.TargetSelection.Strategy).ToString(),
            SelectionConfigurationHash = BuildSelectionConfigurationHash(config, rootConfig),
            CandidateLimit = config.CandidateLimit,
            RandomSeed = config.CandidateCohort.RandomSeed,
            CreatedAt = DateTime.UtcNow,
            Members = members
        }, cancellationToken);

        var resolvedCandidates = candidates.Select(CloneCandidate).ToList();
        for (var index = 0; index < resolvedCandidates.Count; index++)
            resolvedCandidates[index].CandidateCohortMemberId = cohort.Members[index].Id;

        return new CandidateCohortResolution(cohort, resolvedCandidates);
    }

    public async Task<CandidateCohortResolution> ReuseAsync(
        ExperimentConfig config,
        TestMapConfig rootConfig,
        int projectId,
        string repositoryIdentity,
        string commitHash,
        CancellationToken cancellationToken = default)
    {
        var cohortKey = RequireCohortKey(config);
        var cohort = await _repository.GetByKeyAsync(projectId, cohortKey, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"Candidate cohort '{cohortKey}' was not found for this project. Run it once with mode 'create' first.");

        ValidateCompatibility(cohort, config, rootConfig, repositoryIdentity, commitHash);
        var currentIdentities = await LoadSourceIdentitiesByNameAsync(
            cohort.Members.Select(x => x.SourceMethodName),
            cancellationToken);
        var candidates = new List<CandidateMethod>(cohort.Members.Count);

        foreach (var member in cohort.Members.OrderBy(x => x.Ordinal))
        {
            var currentIdentity = ResolveCurrentIdentity(member, currentIdentities);
            if (currentIdentity == null)
                throw new InvalidOperationException(
                    $"Candidate cohort '{cohortKey}' cannot resolve member #{member.Ordinal} " +
                    $"'{member.ContainingType}.{member.SourceMethodName}' at '{member.SourceFilePath}'. " +
                    "Re-ingest the matching commit or create a new cohort.");

            var candidate = JsonSerializer.Deserialize<CandidateMethod>(
                                member.CandidateSnapshotJson,
                                SnapshotJsonOptions)
                            ?? throw new InvalidOperationException(
                                $"Candidate cohort '{cohortKey}' contains an invalid snapshot for member #{member.Ordinal}.");
            candidate.Id = 0;
            candidate.ExperimentRunId = 0;
            candidate.CandidateInventoryId = null;
            candidate.CandidateCohortMemberId = member.Id;
            candidate.MemberId = currentIdentity.MemberId;
            candidate.GenerationAttempts = [];
            candidate.ExperimentRun = null;
            candidates.Add(candidate);
        }

        return new CandidateCohortResolution(cohort, candidates);
    }

    private static void ValidateCompatibility(
        CandidateCohort cohort,
        ExperimentConfig config,
        TestMapConfig rootConfig,
        string repositoryIdentity,
        string commitHash)
    {
        if (!string.Equals(cohort.RepositoryIdentity, repositoryIdentity, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Candidate cohort '{cohort.CohortKey}' belongs to repository '{cohort.RepositoryIdentity}', not '{repositoryIdentity}'.");

        if (!string.Equals(cohort.CommitHash, commitHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Candidate cohort '{cohort.CohortKey}' was created for commit '{cohort.CommitHash}', not '{commitHash}'.");

        if (!string.Equals(cohort.Objective, config.Objective.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Candidate cohort '{cohort.CohortKey}' uses objective '{cohort.Objective}', not '{config.Objective}'.");

        var selectionHash = BuildSelectionConfigurationHash(config, rootConfig);
        if (!string.Equals(cohort.SelectionConfigurationHash, selectionHash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Candidate cohort '{cohort.CohortKey}' was created with different candidate-selection settings. " +
                "Use the same objective, strategy, context mode, candidate limit, and coverage thresholds.");
    }

    private async Task<Dictionary<int, SourceIdentity>> LoadSourceIdentitiesAsync(
        IEnumerable<int> memberIds,
        CancellationToken cancellationToken)
    {
        var ids = memberIds.Distinct().ToList();
        var rows = await (from member in _dbContext.Members.AsNoTracking()
                join sourceObject in _dbContext.Objects.AsNoTracking()
                    on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in _dbContext.Files.AsNoTracking()
                    on sourceObject.FileId equals sourceFile.Id
                where ids.Contains(member.Id)
                select new
                {
                    MemberId = member.Id,
                    MethodName = member.Name,
                    sourceFile.FilePath,
                    sourceObject.Namespace,
                    TypeName = sourceObject.Name,
                    member.ContentHash
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new SourceIdentity(
                x.MemberId,
                x.MethodName,
                x.FilePath,
                BuildContainingType(x.Namespace, x.TypeName),
                x.ContentHash))
            .ToDictionary(x => x.MemberId);
    }

    private async Task<List<SourceIdentity>> LoadSourceIdentitiesByNameAsync(
        IEnumerable<string> methodNames,
        CancellationToken cancellationToken)
    {
        var names = methodNames.Distinct().ToList();
        var rows = await (from member in _dbContext.Members.AsNoTracking()
                join sourceObject in _dbContext.Objects.AsNoTracking()
                    on member.ObjectEntityId equals sourceObject.Id
                join sourceFile in _dbContext.Files.AsNoTracking()
                    on sourceObject.FileId equals sourceFile.Id
                where names.Contains(member.Name)
                select new
                {
                    MemberId = member.Id,
                    MethodName = member.Name,
                    sourceFile.FilePath,
                    sourceObject.Namespace,
                    TypeName = sourceObject.Name,
                    member.ContentHash
                })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new SourceIdentity(
                x.MemberId,
                x.MethodName,
                x.FilePath,
                BuildContainingType(x.Namespace, x.TypeName),
                x.ContentHash))
            .ToList();
    }

    private static SourceIdentity? ResolveCurrentIdentity(
        CandidateCohortMember member,
        IReadOnlyCollection<SourceIdentity> identities)
    {
        var byId = identities.FirstOrDefault(x => x.MemberId == member.SourceMemberId);
        if (byId != null && StableIdentityMatches(member, byId))
            return byId;

        return identities.FirstOrDefault(x => StableIdentityMatches(member, x));
    }

    private static bool StableIdentityMatches(CandidateCohortMember member, SourceIdentity identity)
    {
        return string.Equals(member.SourceMethodName, identity.MethodName, StringComparison.Ordinal) &&
               string.Equals(
                   NormalizePath(member.SourceFilePath),
                   NormalizePath(identity.SourceFilePath),
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(member.ContainingType, identity.ContainingType, StringComparison.Ordinal) &&
               (string.IsNullOrWhiteSpace(member.SourceContentHash) ||
                string.Equals(member.SourceContentHash, identity.ContentHash, StringComparison.Ordinal));
    }

    private static CandidateMethod CloneCandidate(CandidateMethod candidate)
    {
        var json = JsonSerializer.Serialize(candidate, SnapshotJsonOptions);
        var clone = JsonSerializer.Deserialize<CandidateMethod>(json, SnapshotJsonOptions)!;
        clone.Id = 0;
        clone.ExperimentRunId = 0;
        clone.GenerationAttempts = [];
        clone.ExperimentRun = null;
        return clone;
    }

    private static string RequireCohortKey(ExperimentConfig config)
    {
        return string.IsNullOrWhiteSpace(config.CandidateCohort.Id)
            ? throw new InvalidOperationException("ExperimentConfig.CandidateCohort.Id is required.")
            : config.CandidateCohort.Id.Trim();
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim();
    }

    private static string BuildContainingType(string sourceNamespace, string typeName)
    {
        return string.IsNullOrWhiteSpace(sourceNamespace)
            ? typeName
            : $"{sourceNamespace}.{typeName}";
    }

    private sealed record SourceIdentity(
        int MemberId,
        string MethodName,
        string SourceFilePath,
        string ContainingType,
        string ContentHash);
}
