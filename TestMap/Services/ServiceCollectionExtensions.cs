using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestMap.App;
using TestMap.Execution.Steps;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Repositories;
using TestMap.Persistence.Ef.Repositories.Code;
using TestMap.Persistence.Ef.Repositories.Coverage;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Persistence.Ef.Repositories.FlakyTestDetection;
using TestMap.Persistence.Ef.Repositories.MutationTesting;
using TestMap.Persistence.Ef.Repositories.RiskScoring;
using TestMap.Persistence.Ef.Repositories.Testing;
using TestMap.Runs;
using TestMap.Services.CollectInformation;
using TestMap.Services.Configuration;
using TestMap.Services.Mapping;
using TestMap.Services.ProjectOperations;
using TestMap.Services.RepoOperations;
using TestMap.Services.RepoOperations.Clone;
using TestMap.Services.RepoOperations.Delete;
using TestMap.Services.FlakyTestDetection;
using TestMap.Services.RiskScoring;
using TestMap.Services.StaticAnalysis;
using TestMap.Services.Testing;
using TestMap.Services.Testing.Providers;
using TestMap.Services.Testing.Providers.Abstractions;
using TestMap.Services.Testing.Providers.Amazon;
using TestMap.Services.Testing.Providers.Custom;
using TestMap.Services.Testing.Providers.Google;
using TestMap.Services.Testing.Providers.Ollama;
using TestMap.Services.Testing.Providers.OpenAI;
using TestMap.Services.Experiment;

namespace TestMap.Services;

/// <summary>
/// Extension methods for configuring TestMap services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core TestMap services including test building, generation, and collection services.
    /// </summary>
    public static IServiceCollection AddTestMapCore(this IServiceCollection services)
    {
        services.AddScoped<BuildTestService>();
        services.AddScoped<IBuildTestService>(sp => sp.GetRequiredService<BuildTestService>());
        services.AddScoped<CallFailureService>();
        services.AddScoped<ProjectArtifactCleanupService>();
        services.AddScoped<DockerRuntimePathMapper>();
        services.AddScoped<TestGenerator>();
        services.AddScoped<ITestGenerationPipelineService, TestGenerationPipelineService>();
        services.AddScoped<CollectCoverageResultsService>();
        services.AddScoped<CollectMutationTestingResultsService>();
        services.AddScoped<CollectTestResultsService>();
        services.AddScoped<CollectTestsResultWriter>();
        services.AddScoped<MapCoverageService>();
        services.AddScoped<MapMutationService>();
        services.AddRiskScoring();
        services.AddFlakyTestDetection();

        return services;
    }

    public static IServiceCollection AddFlakyTestDetection(this IServiceCollection services)
    {
        services.AddScoped<IFlakyTestDetectionService, FlakyTestDetectionService>();
        services.AddScoped<IFlakinessScoringService, FlakinessScoringService>();
        services.AddScoped<ITestExecutionHistoryService, TestExecutionHistoryService>();
        services.AddScoped<IFlakyTestRerunService, FlakyTestRerunService>();
        services.AddScoped<IFlakinessFactorProvider, OutcomeVarianceFlakinessFactorProvider>();
        services.AddScoped<IFlakinessFactorProvider, RerunInstabilityFlakinessFactorProvider>();
        services.AddScoped<IFlakinessFactorProvider, DurationVarianceFlakinessFactorProvider>();
        services.AddScoped<IFlakinessFactorProvider, FailureSignatureFlakinessFactorProvider>();
        services.AddScoped<IFlakinessFactorProvider, EnvironmentSignalFlakinessFactorProvider>();

        return services;
    }

    public static IServiceCollection AddRiskScoring(this IServiceCollection services)
    {
        services.AddScoped<IRiskScoringService, RiskScoringService>();
        services.AddScoped<IRiskFactorProvider, CoverageGapRiskFactorProvider>();
        services.AddScoped<IRiskFactorProvider, MutationSurvivalRiskFactorProvider>();
        services.AddScoped<IRiskFactorProvider, ComplexityRiskFactorProvider>();
        services.AddScoped<IRiskFactorProvider, CallGraphRiskFactorProvider>();
        services.AddScoped<IRiskFactorProvider, ChurnRiskFactorProvider>();
        services.AddScoped<IRiskFactorProvider, TestGapRiskFactorProvider>();

        return services;
    }

    /// <summary>
    /// Adds AI generation providers for test generation.
    /// </summary>
    public static IServiceCollection AddAiProviders(this IServiceCollection services)
    {
        services.AddScoped<IAiGenerationProvider, OpenAiGenerationProvider>();
        services.AddScoped<IAiGenerationProvider, CustomOpenAiGenerationProvider>();
        services.AddScoped<IAiGenerationProvider, OllamaGenerationProvider>();
        services.AddScoped<IAiGenerationProvider, GoogleGeminiGenerationProvider>();
        services.AddScoped<IAiGenerationProvider, GoogleCloudGenerationProvider>();
        services.AddScoped<IAiGenerationProvider, AmazonGenerationProvider>();

        return services;
    }

    /// <summary>
    /// Adds repository operations and project analysis services.
    /// </summary>
    public static IServiceCollection AddProjectServices(this IServiceCollection services)
    {
        services.AddScoped<IRepoOperations, RepoOperations.RepoOperations>();
        services.AddScoped<IStaticAnalysisWorkspace, StaticAnalysisWorkspace>();
        services.AddScoped<IAnalyzeProjectService, AnalyzeProjectService>();
        services.AddScoped<ICodeMetricsService, CodeMetricsService>();
        services.AddScoped<ITestMetadataEnrichmentService, TestMetadataEnrichmentService>();
        services.AddScoped<ITestSmellService, TestSmellService>();
        services.AddScoped<XNoseNextService>();
        services.AddScoped<IProjectBuildAnalysisService, ProjectBuildAnalysisService>();
        services.AddScoped<IExtractInformationService, ExtractInformationService>();
        services.AddScoped<IGenerateTestService, GenerateTestService>();
        services.AddScoped<ICloneRepoService, CloneRepoService>();
        services.AddScoped<IDeleteProjectService, DeleteProjectService>();

        return services;
    }

    /// <summary>
    /// Adds project operation services with required dependencies.
    /// </summary>
    public static IServiceCollection AddProjectOperations(
        this IServiceCollection services,
        Models.Configuration.TestMapConfig config,
        string githubToken,
        ProjectContext context)
    {
        services.AddScoped<ICheckProjectsService, CheckProjectsService>(sp =>
            new CheckProjectsService(config, githubToken, context));

        return services;
    }

    /// <summary>
    /// Adds all TestMap repositories for data access.
    /// </summary>
    public static IServiceCollection AddTestMapRepositories(this IServiceCollection services)
    {
        services.AddScoped<ProjectRepository>();
        services.AddScoped<CSharpSolutionRepository>();
        services.AddScoped<CSharpProjectRepository>();
        services.AddScoped<FileRepository>();
        services.AddScoped<ObjectRepository>();
        services.AddScoped<MemberRepository>();
        services.AddScoped<ObjectRelationshipRepository>();
        services.AddScoped<MemberRelationshipRepository>();
        services.AddScoped<InvocationRepository>();
        services.AddScoped<CodeMetricRepository>();
        services.AddScoped<CoverageReportRepository>();
        services.AddScoped<CoverageGapRepository>();
        services.AddScoped<ObjectCoverageRepository>();
        services.AddScoped<MemberCoverageRepository>();
        services.AddScoped<MutationTestingReportRepository>();
        services.AddScoped<CandidateMethodRiskScoreRepository>();
        services.AddScoped<TestExecutionResultRepository>();
        services.AddScoped<FlakyTestScoreRepository>();
        services.AddScoped<FlakyTestRerunResultRepository>();
        services.AddScoped<TestRunRepository>();
        services.AddScoped<TestResultRepository>();
        services.AddScoped<TestSmellRepository>();

        return services;
    }

    /// <summary>
    /// Adds experiment repositories for AI provider comparison experiments.
    /// </summary>
    public static IServiceCollection AddExperimentRepositories(this IServiceCollection services)
    {
        services.AddScoped<ExperimentRunRepository>();
        services.AddScoped<CandidateMethodRepository>();
        services.AddScoped<GenerationAttemptRepository>();
        services.AddScoped<GenerationStepRepository>();
        services.AddScoped<TestExecutionRepository>();

        return services;
    }

    /// <summary>
    /// Adds experiment services for AI provider comparison experiments.
    /// </summary>
    public static IServiceCollection AddExperimentServices(this IServiceCollection services)
    {
        services.AddScoped<IMethodSelectionService, MethodSelectionService>();
        services.AddScoped<IExperimentOrchestrationService, ExperimentOrchestrationService>();
        services.AddScoped<IExperimentAnalysisService, ExperimentAnalysisService>();

        return services;
    }

    /// <summary>
    /// Adds all pipeline steps for workflow execution.
    /// </summary>
    public static IServiceCollection AddPipelineSteps(this IServiceCollection services)
    {
        services.AddScoped<CloneRepoStep>();
        services.AddScoped<LoadDatabaseStep>();
        services.AddScoped<ExtractInfoStep>();
        services.AddScoped<InsertProjectInfoStep>();
        services.AddScoped<AnalyzeProjectStep>();
        services.AddScoped<CollectCodeMetricsStep>();
        services.AddScoped<EnrichTestMetadataStep>();
        services.AddScoped<CollectTestSmellsStep>();
        services.AddScoped<BuildTestStep>();
        services.AddScoped<WriteCollectTestsResultStep>();
        services.AddScoped<GenerateTestsStep>();
        services.AddScoped<CheckProjectsStep>();
        services.AddScoped<RunExperimentStep>();

        return services;
    }

    /// <summary>
    /// Adds all pipeline run implementations.
    /// </summary>
    public static IServiceCollection AddPipelineRuns(this IServiceCollection services)
    {
        services.AddTransient<CollectTestsRun>();
        services.AddTransient<GenerateTestsRun>();
        services.AddTransient<CheckProjectsRun>();
        services.AddTransient<ExperimentRun>();
        services.AddTransient<StaticAnalysisRun>();

        return services;
    }

    /// <summary>
    /// Adds the pipeline run factory for creating pipeline runs based on run mode.
    /// </summary>
    public static IServiceCollection AddPipelineFactory(this IServiceCollection services)
    {
        services.AddTransient<IPipelineRunFactory, PipelineRunFactory>();

        return services;
    }

    /// <summary>
    /// Adds the TestMap database context with SQLite configuration.
    /// </summary>
    public static IServiceCollection AddTestMapDatabase(
        this IServiceCollection services,
        ProjectContext context)
    {
        services.AddDbContext<TestMapDbContext>((sp, options) =>
        {
            var dbPath = context.Project.DatabasePath;
            options.UseSqlite($"Data Source={dbPath}");
        });

        return services;
    }

    /// <summary>
    /// Adds all TestMap services needed for project pipeline execution.
    /// This is a convenience method that registers all required services in one call.
    /// </summary>
    public static IServiceCollection AddTestMapServices(
        this IServiceCollection services,
        IConfigurationService configurationService,
        Models.Configuration.TestMapConfig config,
        ProjectContext context)
    {
        // Add singletons
        services.AddSingleton(configurationService);
        services.AddSingleton(config);
        services.AddScoped<ProjectContext>(_ => context);
        services.AddScoped<SqliteSchemaCompatibilityService>();

        // Add database
        services.AddTestMapDatabase(context);

        // Add all service groups
        services.AddTestMapCore();
        services.AddAiProviders();
        services.AddProjectServices();
        
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
        services.AddProjectOperations(config, token, context);
        
        services.AddTestMapRepositories();
        services.AddExperimentRepositories();
        services.AddExperimentServices();
        services.AddPipelineSteps();
        services.AddPipelineRuns();
        services.AddPipelineFactory();

        return services;
    }
}
