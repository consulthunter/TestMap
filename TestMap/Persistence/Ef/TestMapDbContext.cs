using Microsoft.EntityFrameworkCore;
using TestMap.Persistence.Ef.Entities;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Persistence.Ef.Entities.Coverage;
using TestMap.Persistence.Ef.Entities.Experiment;
using TestMap.Persistence.Ef.Entities.FlakyTestDetection;
using TestMap.Persistence.Ef.Entities.MutationTesting;
using TestMap.Persistence.Ef.Entities.RiskScoring;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef;

public class TestMapDbContext : DbContext
{
    public TestMapDbContext(DbContextOptions<TestMapDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<CodeMetricEntity> CodeMetrics => Set<CodeMetricEntity>();
    public DbSet<CSharpProjectEntity> CSharpProjects => Set<CSharpProjectEntity>();
    public DbSet<CSharpSolutionEntity> CSharpSolutions => Set<CSharpSolutionEntity>();
    public DbSet<FileEntity> Files => Set<FileEntity>();
    public DbSet<InvocationEntity> Invocations => Set<InvocationEntity>();
    public DbSet<MemberEntity> Members => Set<MemberEntity>();
    public DbSet<MemberRelationshipEntity> MemberRelationships => Set<MemberRelationshipEntity>();
    public DbSet<ObjectEntity> Objects => Set<ObjectEntity>();
    public DbSet<ObjectRelationshipEntity> ObjectRelationships => Set<ObjectRelationshipEntity>();
    public DbSet<CoverageReportEntity> CoverageReports => Set<CoverageReportEntity>();
    public DbSet<CoverageGapEntity> CoverageGaps => Set<CoverageGapEntity>();
    public DbSet<MemberCoverageEntity> MemberCoverages => Set<MemberCoverageEntity>();
    public DbSet<ObjectCoverageEntity> ObjectCoverages => Set<ObjectCoverageEntity>();
    public DbSet<MutationTestingReportEntity> MutationTestingReports => Set<MutationTestingReportEntity>();
    public DbSet<MutantEntity> Mutants => Set<MutantEntity>();
    public DbSet<MutantSurvivedTestEntity> MutantSurvivedTests => Set<MutantSurvivedTestEntity>();
    public DbSet<CandidateMethodRiskScoreEntity> CandidateMethodRiskScores => Set<CandidateMethodRiskScoreEntity>();
    public DbSet<TestExecutionResultEntity> TestExecutionResults => Set<TestExecutionResultEntity>();
    public DbSet<FlakyTestScoreEntity> FlakyTestScores => Set<FlakyTestScoreEntity>();
    public DbSet<FlakyTestRerunResultEntity> FlakyTestRerunResults => Set<FlakyTestRerunResultEntity>();
    public DbSet<TestResultEntity> TestResults => Set<TestResultEntity>();
    public DbSet<TestRunEntity> TestRuns => Set<TestRunEntity>();
    public DbSet<TestSmellEntity> TestSmells => Set<TestSmellEntity>();
    public DbSet<ExperimentRunEntity> ExperimentRuns => Set<ExperimentRunEntity>();
    public DbSet<CandidateMethodEntity> CandidateMethods => Set<CandidateMethodEntity>();
    public DbSet<GenerationAttemptEntity> GenerationAttempts => Set<GenerationAttemptEntity>();
    public DbSet<GenerationStepEntity> GenerationSteps => Set<GenerationStepEntity>();
    public DbSet<TestExecutionEntity> TestExecutions => Set<TestExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TestMapDbContext).Assembly);
    }
}
