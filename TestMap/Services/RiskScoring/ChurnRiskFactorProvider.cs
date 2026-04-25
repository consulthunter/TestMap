using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.RiskScoring;
using TestMap.Persistence.Ef;

namespace TestMap.Services.RiskScoring;

public class ChurnRiskFactorProvider(ProjectContext context, TestMapDbContext dbContext) : IRiskFactorProvider
{
    public RiskFactorKind Factor => RiskFactorKind.Churn;

    public async Task<RiskFactorScore> ScoreAsync(MemberModel candidateMember,
        CancellationToken cancellationToken = default)
    {
        var filePath = await (
                from member in dbContext.Members
                join sourceObject in dbContext.Objects on member.ObjectEntityId equals sourceObject.Id
                join file in dbContext.Files on sourceObject.FileId equals file.Id
                where member.Id == candidateMember.Id
                select file.FilePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(filePath) ||
            string.IsNullOrWhiteSpace(context.Project.DirectoryPath) ||
            !Repository.IsValid(context.Project.DirectoryPath))
            return new RiskFactorScore(Factor, 0.0, "No git repository or source file path is available.");

        var relativePath = Path.GetRelativePath(context.Project.DirectoryPath, filePath)
            .Replace('\\', '/');

        try
        {
            using var repository = new Repository(context.Project.DirectoryPath);
            var commits = repository.Commits
                .QueryBy(relativePath)
                .Take(25)
                .Count();
            var score = Math.Min(1.0, commits / 15.0);

            return new RiskFactorScore(Factor, score, $"recent file commits={commits}");
        }
        catch (Exception ex) when (ex is LibGit2SharpException or ArgumentException)
        {
            return new RiskFactorScore(Factor, 0.0, $"git churn unavailable: {ex.Message}");
        }
    }
}