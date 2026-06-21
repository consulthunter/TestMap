using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Coverage;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Mapping.Code;
using TestMap.Persistence.Ef.Repositories.Experiment;
using TestMap.Services.StaticAnalysis;

namespace TestMap.Services.TestGeneration.TargetSelection;

/// <summary>
/// Service for selecting candidate methods for test generation experiments.
/// Queries the database for methods with coverage within specified thresholds.
/// </summary>
public class MethodSelectionService : IMethodSelectionService
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;
    private readonly CandidateMethodSelector _candidateMethodSelector;
    private readonly CandidateInventoryRepository _candidateInventoryRepository;
    private readonly IStaticAnalysisWorkspace? _staticAnalysisWorkspace;
    private readonly ICandidateMethodMetadataService _candidateMethodMetadataService;

    public MethodSelectionService(
        ProjectContext context,
        TestMapDbContext dbContext,
        CandidateMethodSelector candidateMethodSelector,
        CandidateInventoryRepository candidateInventoryRepository,
        IStaticAnalysisWorkspace? staticAnalysisWorkspace = null,
        ICandidateMethodMetadataService? candidateMethodMetadataService = null)
    {
        _context = context;
        _dbContext = dbContext;
        _candidateMethodSelector = candidateMethodSelector;
        _candidateInventoryRepository = candidateInventoryRepository;
        _staticAnalysisWorkspace = staticAnalysisWorkspace;
        _candidateMethodMetadataService = candidateMethodMetadataService ?? new CandidateMethodMetadataService();
    }

    public async Task<List<CandidateMethod>> SelectCandidateMethodsAsync(
        ExperimentConfig config,
        bool requirePassingExistingTest = false,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _candidateMethodSelector.SelectAsync(config, cancellationToken);
        var latestBaseline = await LoadLatestBaselineTestOutcomesAsync(cancellationToken);
        var inventory = new List<CandidateInventoryItem>();
        var enrichedCandidates = new List<EnrichedCandidateSelection>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var contextMappingMode = config.ContextMappingMode ??
                                     _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection
                                         .ContextMappingMode;
            var context = await GetMethodContextAsync(candidate.MemberId, contextMappingMode, cancellationToken);
            if (context == null) continue;

            candidate.ExistingTestMemberId = context.Method.ExistingTestMemberId;
            candidate.ExistingTestMethodName = context.Method.ExistingTestMethodName;
            candidate.TestState = context.Method.TestState;
            candidate.RecommendedAction = context.Method.RecommendedAction;
            candidate.TestStateReason = context.Method.TestStateReason;
            candidate.TestIntentionsSummary = context.Method.TestIntentionsSummary;
            candidate.TypeConstructionSummary = context.Method.TypeConstructionSummary;
            candidate.CandidateMetadataJson = context.Method.CandidateMetadataJson;

            var inventoryItem = CreateInventoryItem(
                config,
                candidate,
                context,
                latestBaseline);
            inventory.Add(inventoryItem);

            enrichedCandidates.Add(new EnrichedCandidateSelection(
                candidate,
                inventoryItem,
                context.ContextEvidenceKind,
                context.HasGroundedTestContext,
                index));
        }

        var persistedInventory = await _candidateInventoryRepository.ReplaceForProjectStrategyAsync(
            _context.Project.DbId,
            config.CandidateSelectionStrategy ?? _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection.Strategy,
            inventory,
            cancellationToken);
        var inventoryIdsBySourceMemberId = persistedInventory
            .GroupBy(x => x.SourceMemberId)
            .ToDictionary(x => x.Key, x => x.First().Id);
        foreach (var enrichedCandidate in enrichedCandidates)
            if (inventoryIdsBySourceMemberId.TryGetValue(enrichedCandidate.Candidate.MemberId, out var inventoryId))
                enrichedCandidate.Candidate.CandidateInventoryId = inventoryId;

        var selectionLimit = ResolveFinalSelectionLimit(config);
        return enrichedCandidates
            .Where(x => !requirePassingExistingTest || x.InventoryItem.IsExperimentEligible)
            .OrderBy(x => GetContextSelectionPriority(x.ContextEvidenceKind, x.HasGroundedTestContext))
            .ThenBy(x => x.StrategyRank)
            .Take(selectionLimit)
            .Select(x => x.Candidate)
            .ToList();
    }

    private static int ResolveFinalSelectionLimit(ExperimentConfig config)
    {
        return Math.Max(1, config.CandidateLimit);
    }

    private static int GetContextSelectionPriority(string evidenceKind, bool isGrounded)
    {
        if (IsDirectMethodEvidence(evidenceKind)) return 0;
        if (string.Equals(evidenceKind, "MemberRelationship", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(evidenceKind, "HelperMediatedPath", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(evidenceKind, "ProductionMethodPath", StringComparison.OrdinalIgnoreCase)) return 3;
        if (string.Equals(evidenceKind, "DeepProductionMethodPath", StringComparison.OrdinalIgnoreCase)) return 4;
        if (string.Equals(evidenceKind, "DirectConstructorInvocation", StringComparison.OrdinalIgnoreCase)) return 5;
        if (string.Equals(evidenceKind, "ConstructorMediatedPath", StringComparison.OrdinalIgnoreCase)) return 6;
        if (isGrounded) return 7;
        if (string.Equals(evidenceKind, "WeakHeuristic", StringComparison.OrdinalIgnoreCase)) return 9;
        return 8;
    }

    private static bool IsDirectMethodEvidence(string evidenceKind)
    {
        return string.Equals(evidenceKind, "DirectMethodInvocation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(evidenceKind, "DirectInvocation", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LatestBaselineTestOutcomes> LoadLatestBaselineTestOutcomesAsync(
        CancellationToken cancellationToken)
    {
        var latestRun = await _dbContext.TestRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == _context.Project.DbId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRun == null)
            return new LatestBaselineTestOutcomes(string.Empty, new Dictionary<int, string>());

        var results = await _dbContext.TestResults
            .AsNoTracking()
            .Where(x => x.TestRunId == latestRun.Id && x.MethodId > 0)
            .ToListAsync(cancellationToken);

        return new LatestBaselineTestOutcomes(
            latestRun.RunId,
            results
                .GroupBy(x => x.MethodId)
                .ToDictionary(x => x.Key, x => AggregateOutcome(x.Select(result => result.Outcome).ToList())));
    }

    private static string AggregateOutcome(IReadOnlyCollection<string> outcomes)
    {
        if (outcomes.Count == 0) return string.Empty;

        if (outcomes.Any(IsFailingOutcome)) return "Failed";
        if (outcomes.All(x => string.Equals(x, "Passed", StringComparison.OrdinalIgnoreCase))) return "Passed";

        return outcomes.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    }

    private static bool IsFailingOutcome(string outcome)
    {
        return outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
               outcome.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
               outcome.Equals("Aborted", StringComparison.OrdinalIgnoreCase);
    }

    private CandidateInventoryItem CreateInventoryItem(
        ExperimentConfig config,
        CandidateMethod candidate,
        CandidateMethodContext context,
        LatestBaselineTestOutcomes latestBaseline)
    {
        var existingTestMemberId = candidate.ExistingTestMemberId;
        var hasExistingTest = existingTestMemberId.HasValue;
        var existingTestOutcome = existingTestMemberId.HasValue &&
                                  latestBaseline.OutcomesByTestMemberId.TryGetValue(
                                      existingTestMemberId.Value,
                                      out var outcome)
            ? outcome
            : string.Empty;
        var isEligible = hasExistingTest &&
                         string.Equals(existingTestOutcome, "Passed", StringComparison.OrdinalIgnoreCase);

        return new CandidateInventoryItem
        {
            ProjectId = _context.Project.DbId,
            SourceMemberId = candidate.MemberId,
            SourceTestMappingId = context.SourceTestMappingId,
            ExistingTestMemberId = candidate.ExistingTestMemberId,
            SourceMethodName = candidate.MethodName,
            SourceMethodSignature = candidate.Signature,
            ExistingTestMethodName = candidate.ExistingTestMethodName ?? string.Empty,
            InitialCoverage = candidate.BaselineCoverage,
            ComplexityScore = candidate.ComplexityScore,
            SelectionStrategy = config.CandidateSelectionStrategy ??
                                _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection.Strategy,
            ExistingTestOutcome = existingTestOutcome,
            IsExperimentEligible = isEligible,
            IneligibilityReason = ResolveIneligibilityReason(hasExistingTest, latestBaseline.RunId, existingTestOutcome),
            RiskScore = candidate.RiskScore,
            MetricDrivenScore = candidate.MetricDrivenScore,
            ExpectedMetricDelta = candidate.ExpectedMetricDelta,
            MetricGuardrailStatus = candidate.MetricGuardrailStatus,
            MetricSelectionReason = candidate.MetricSelectionReason,
            TestState = candidate.TestState,
            RecommendedAction = candidate.RecommendedAction,
            TestStateReason = candidate.TestStateReason,
            SelectionTime = candidate.SelectionTime == default ? DateTime.UtcNow : candidate.SelectionTime,
            BaselineRunId = latestBaseline.RunId,
            ContextEvidenceKind = context.ContextEvidenceKind,
            ContextEvidenceSummary = context.ContextEvidenceSummary,
            HasGroundedTestContext = context.HasGroundedTestContext,
            MappedTestMemberId = context.HasGroundedTestContext ? context.Method.ExistingTestMemberId : null,
            MappedTestMethodName = context.HasGroundedTestContext ? context.Method.ExistingTestMethodName ?? string.Empty : string.Empty,
            AccessPathStrategy = ResolveInventoryAccessPathStrategy(context),
            AccessPathMemberIdsJson = SerializeAccessPathMemberIds(context),
            TraceSummary = BuildInventoryTraceSummary(context),
            TraceJson = BuildInventoryTraceJson(context),
            TestIntentionsSummary = context.CandidateTestIntentionsSummary,
            TypeConstructionSummary = context.CandidateTypeConstructionSummary,
            CandidateMetadataJson = context.CandidateMetadataJson
        };
    }

    private static string ResolveIneligibilityReason(
        bool hasExistingTest,
        string baselineRunId,
        string existingTestOutcome)
    {
        if (!hasExistingTest) return "No paired existing test was identified.";
        if (string.IsNullOrWhiteSpace(baselineRunId)) return "No baseline test run was found.";
        if (string.IsNullOrWhiteSpace(existingTestOutcome)) return "The paired test was not found in baseline results.";
        if (!string.Equals(existingTestOutcome, "Passed", StringComparison.OrdinalIgnoreCase))
            return $"The paired test outcome was '{existingTestOutcome}', not 'Passed'.";

        return string.Empty;
    }

    private static string ResolveInventoryAccessPathStrategy(CandidateMethodContext context)
    {
        return context.Testability?.AccessPaths.FirstOrDefault()?.Strategy.ToString() ?? string.Empty;
    }

    private static string SerializeAccessPathMemberIds(CandidateMethodContext context)
    {
        var memberIds = context.Testability?.AccessPaths.FirstOrDefault()?.PathMemberIds ?? [];
        return JsonSerializer.Serialize(memberIds);
    }

    private static string BuildInventoryTraceSummary(CandidateMethodContext context)
    {
        if (!context.HasGroundedTestContext)
            return string.Equals(context.ContextEvidenceKind, "WeakHeuristic", StringComparison.OrdinalIgnoreCase)
                ? $"Ungrounded weak heuristic context selected. {context.ContextEvidenceSummary}"
                : $"No grounded source-test trace selected. {context.ContextEvidenceSummary}";

        var path = context.Testability?.AccessPaths.FirstOrDefault()?.PathMemberIds;
        var pathText = path is { Count: > 0 }
            ? string.Join(" -> ", path)
            : context.Method.MemberId.ToString();
        var testName = string.IsNullOrWhiteSpace(context.Method.ExistingTestMethodName)
            ? context.Method.ExistingTestMemberId?.ToString() ?? "unknown test"
            : context.Method.ExistingTestMethodName;

        return
            $"{context.ContextEvidenceKind}: test '{testName}' maps to source member {context.Method.MemberId} through path {pathText}. {context.ContextEvidenceSummary}";
    }

    private static string BuildInventoryTraceJson(CandidateMethodContext context)
    {
        var accessPath = context.Testability?.AccessPaths.FirstOrDefault();
        var mappingPath = context.Testability?.TestMappings.FirstOrDefault();
        object? accessPathSnapshot = accessPath == null
            ? null
            : new
            {
                accessPath.TargetMemberId,
                accessPath.EntrypointMemberId,
                accessPath.PathMemberIds,
                Strategy = accessPath.Strategy.ToString(),
                accessPath.IsLegalFromTest,
                accessPath.RequiresReflection,
                accessPath.Distance
            };
        object? mappingPathSnapshot = mappingPath == null
            ? null
            : new
            {
                mappingPath.TestMemberId,
                mappingPath.SourceMemberId,
                mappingPath.PathMemberIds,
                mappingPath.IsDirect,
                mappingPath.IsObservedByCoverage,
                mappingPath.IsObservedByMutation
            };
        var trace = new
        {
            context.Method.MemberId,
            context.Method.MethodName,
            EvidenceKind = context.ContextEvidenceKind,
            EvidenceSummary = context.ContextEvidenceSummary,
            IsGrounded = context.HasGroundedTestContext,
            TestMemberId = context.HasGroundedTestContext ? context.Method.ExistingTestMemberId : null,
            TestMethodName = context.HasGroundedTestContext ? context.Method.ExistingTestMethodName : null,
            AccessPath = accessPathSnapshot,
            MappingPath = mappingPathSnapshot,
            EvidenceStatuses = context.Testability?.EvidenceStatuses.Select(x => x.ToString()).ToList() ?? [],
            SetupBindings = context.Testability?.SetupBindings.Select(x => new
            {
                x.NeedId,
                x.NeedKind,
                x.RequiredType,
                x.BindingExpression,
                x.BindingKind,
                x.SourceMemberId,
                x.Reason,
                x.IsPreferred
            }).ToList() ?? []
        };

        return JsonSerializer.Serialize(trace);
    }

    public async Task<CandidateMethodContext?> GetMethodContextAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        var contextMappingMode = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection
            .ContextMappingMode;
        return await GetMethodContextAsync(memberId, contextMappingMode, cancellationToken);
    }

    public async Task<CandidateMethodContext?> GetMethodContextAsync(
        int memberId,
        TestContextMappingMode contextMappingMode,
        CancellationToken cancellationToken = default)
    {
        var memberEntity = await _dbContext.Members
            .FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken);

        if (memberEntity == null)
        {
            _context.Project.Logger?.Warning("Member {MemberId} not found", memberId);
            return null;
        }

        var sourceObjectEntity = await _dbContext.Objects
            .FirstOrDefaultAsync(x => x.Id == memberEntity.ObjectEntityId, cancellationToken);

        if (sourceObjectEntity == null)
        {
            _context.Project.Logger?.Warning("Containing object for member {MemberId} not found", memberId);
            return null;
        }

        var sourceFileEntity = await _dbContext.Files
            .FirstOrDefaultAsync(x => x.Id == sourceObjectEntity.FileId, cancellationToken);

        if (sourceFileEntity == null)
        {
            _context.Project.Logger?.Warning("File for object {ObjectId} not found", sourceObjectEntity.Id);
            return null;
        }

        var projectEntity = await _dbContext.CSharpProjects
            .FirstOrDefaultAsync(x => x.Id == sourceFileEntity.CSharpProjectId, cancellationToken);

        if (projectEntity == null)
        {
            _context.Project.Logger?.Warning("Project for file {FileId} not found", sourceFileEntity.Id);
            return null;
        }

        var solutionEntity = await _dbContext.CSharpSolutions
            .FirstOrDefaultAsync(x => x.Id == projectEntity.SolutionId, cancellationToken);

        if (solutionEntity == null)
        {
            _context.Project.Logger?.Warning("Solution for project {ProjectId} not found", projectEntity.Id);
            return null;
        }

        var coverageEntity = await _dbContext.MemberCoverages
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.CoverageReportId)
            .FirstOrDefaultAsync(cancellationToken);

        var latestCoverageReportId = coverageEntity?.CoverageReportId;
        var coverageGaps = latestCoverageReportId.HasValue
            ? await _dbContext.CoverageGaps
                .Where(x => x.MemberId == memberId && x.CoverageReportId == latestCoverageReportId.Value)
                .OrderBy(x => x.LineNumber)
                .ThenBy(x => x.GapKind)
                .ToListAsync(cancellationToken)
            : [];

        var member = memberEntity.ToDomain();
        var sourceObject = sourceObjectEntity.ToDomain();
        var sourceFile = sourceFileEntity.ToDomain();
        var project = projectEntity.ToDomain();
        var solution = solutionEntity.ToDomain();
        var methodSignature = ExtractMethodSignature(member.FullString, member.Name);

        var memberVisibility = ResolveMemberVisibility(memberEntity);
        var testContext = await FindBestTestContextAsync(
            member.Id,
            member.Name,
            memberVisibility,
            contextMappingMode,
            sourceObject,
            sourceFile,
            project,
            solution.FilePath,
            cancellationToken);
        var testSignals = await GetReachableTestSignalCountAsync(memberId, cancellationToken);
        var undetectedMutants = await GetUndetectedMutantsAsync(memberId, cancellationToken);
        var exampleTestSmellCount = testContext?.ExampleTestMemberId is int exampleTestMemberId
                                    && testContext.IsGrounded
            ? await GetTestSmellCountAsync(exampleTestMemberId, cancellationToken)
            : 0;
        var testAssessment = DetermineTestAssessment(
            testContext,
            testSignals,
            coverageEntity?.LineRate ?? 0.0,
            coverageGaps.Count,
            undetectedMutants.Count,
            exampleTestSmellCount);
        var evidenceStatuses = ResolveEvidenceStatuses(
            testContext,
            coverageEntity,
            undetectedMutants.Count);
        var testability = BuildSourceMemberTestability(
            member.Id,
            memberVisibility,
            testContext,
            evidenceStatuses);
        var accessPathSummary = BuildAccessPathSummary(testability, testContext);
        var candidateMetadata = await BuildCandidateMetadataAsync(
            member,
            sourceFile.FilePath,
            solution.FilePath,
            sourceObject.FullString,
            cancellationToken);
        var candidateMetadataJson = JsonSerializer.Serialize(candidateMetadata);

        var candidateMethod = new CandidateMethod
        {
            MemberId = member.Id,
            MethodName = member.Name,
            SourceCode = member.FullString,
            Signature = methodSignature,
            ExistingTestMemberId = testContext?.IsGrounded == true ? testContext.ExampleTestMemberId : null,
            ExistingTestMethodName = testContext?.IsGrounded == true ? testContext.ExampleTestMethodName : null,
            BaselineCoverage = coverageEntity?.LineRate ?? 0.0,
            ComplexityScore = coverageEntity?.Complexity ?? 0.0,
            TestState = testAssessment.TestState,
            RecommendedAction = testAssessment.RecommendedAction,
            TestStateReason = AppendContextMappingReason(testAssessment.Reason, testContext),
            TestIntentionsSummary = candidateMetadata.TestIntentionsSummary,
            TypeConstructionSummary = candidateMetadata.TypeConstructionSummary,
            CandidateMetadataJson = candidateMetadataJson
        };

        LogContextMapping(member, sourceObject, testContext);

        return new CandidateMethodContext
        {
            Method = candidateMethod,
            SourceTestMappingId = testContext?.SourceTestMappingId,
            MethodSignature = methodSignature,
            ContainingClass = sourceObject.FullString,
            TestNamespace = ResolveTestNamespace(testContext, sourceObject.Namespace, project),
            TestClassName = ResolveTestClassName(testContext, sourceObject.Name),
            TestFilePath = ResolveTestFilePath(testContext, sourceFile.FilePath, sourceObject.Name, project),
            SourceFilePath = sourceFile.FilePath,
            SourceLocation = new CandidateSourceLocation
            {
                SourceFilePath = sourceFile.FilePath,
                StartLine = member.Location.StartLineNumber,
                EndLine = member.Location.EndLineNumber,
                StartPosition = member.Location.BodyStartPosition,
                EndPosition = member.Location.BodyEndPosition
            },
            TestLocation = testContext?.ExampleTestMemberLocation == null
                ? null
                : new CandidateTestLocation
                {
                    TestFilePath = testContext.TestFile.FilePath,
                    TestProjectPath = testContext.Project.FilePath,
                    StartLine = testContext.ExampleTestMemberLocation.StartLineNumber,
                    EndLine = testContext.ExampleTestMemberLocation.EndLineNumber
                },
            SourceProjectPath = project.FilePath,
            TestProjectPath = ResolveTestProjectPath(testContext, project),
            TargetBuildFramework = ResolveTargetBuildFramework(testContext?.Project, project),
            SolutionFilePath = solution.FilePath,
            ExampleTest = testContext?.ExampleTest ?? CreateFallbackExampleTest(member.Name,
                DetermineTestFramework(testContext?.TestClass, testContext?.Project)),
            ExampleTestMetadataSummary = testContext?.ExampleTestMetadataSummary ??
                                         "No enriched example test metadata is available.",
            ProjectTestMetadataSummary = testContext?.ProjectTestMetadataSummary ??
                                         "No enriched project test metadata is available.",
            TestClass = ResolveTestClassContents(testContext, sourceObject.Name, sourceObject.Namespace, project),
            TestFileContents = ResolveTestFileContents(testContext, sourceFile.FilePath, sourceObject.Name,
                sourceObject.Namespace, project),
            TestSupportContext = testContext?.SupportContext ??
                                 "No additional helper methods, setup hooks, or support members were discovered in the selected test file.",
            TestFramework = ResolveTestFramework(testContext, project.FilePath),
            TestDependencies = ResolveTestDependencies(testContext, project.FilePath),
            CoverageGapSummary = BuildCoverageGapSummary(coverageGaps),
            MutationSummary = BuildMutationSummary(undetectedMutants),
            CandidateTestIntentionsSummary = candidateMetadata.TestIntentionsSummary,
            CandidateTypeConstructionSummary = candidateMetadata.TypeConstructionSummary,
            CandidateMetadataJson = candidateMetadataJson,
            ContextEvidenceKind = testContext?.EvidenceKind ?? "None",
            ContextEvidenceSummary = testContext?.EvidenceSummary ?? "No test context selected.",
            HasGroundedTestContext = testContext?.IsGrounded ?? false,
            Testability = testability,
            AccessPathSummary = accessPathSummary
        };
    }

    private static string ResolveTestClassName(TestContextCandidate? testContext, string sourceObjectName)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestClass.Name)) return testContext.TestClass.Name;

        return $"{sourceObjectName}Tests";
    }

    private async Task<Models.Generation.CandidateMethodMetadata> BuildCandidateMetadataAsync(
        Models.Code.MemberModel member,
        string sourceFilePath,
        string solutionFilePath,
        string containingClassSource,
        CancellationToken cancellationToken)
    {
        var document = await TryResolveDocumentAsync(sourceFilePath, solutionFilePath, cancellationToken);
        return await _candidateMethodMetadataService.BuildAsync(
            document,
            member.Location.BodyStartPosition,
            member.Location.BodyEndPosition,
            member.FullString,
            containingClassSource,
            cancellationToken);
    }

    private async Task<Document?> TryResolveDocumentAsync(
        string sourceFilePath,
        string solutionFilePath,
        CancellationToken cancellationToken)
    {
        if (_staticAnalysisWorkspace == null || string.IsNullOrWhiteSpace(solutionFilePath)) return null;

        try
        {
            var solution = await _staticAnalysisWorkspace.OpenSolutionAsync(solutionFilePath, cancellationToken);
            var normalizedSourcePath = Path.GetFullPath(sourceFilePath);
            return solution.Projects
                .SelectMany(project => project.Documents)
                .FirstOrDefault(document =>
                    !string.IsNullOrWhiteSpace(document.FilePath) &&
                    string.Equals(Path.GetFullPath(document.FilePath), normalizedSourcePath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _context.Project.Logger?.Warning(
                "Could not resolve Roslyn document for candidate method metadata. SourceFile={SourceFilePath}, Error={ErrorMessage}",
                sourceFilePath,
                ex.Message);
            return null;
        }
    }

    private string ResolveTestFilePath(
        TestContextCandidate? testContext,
        string sourceFilePath,
        string sourceObjectName,
        CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestFile.FilePath)) return testContext.TestFile.FilePath;

        var testProjectPath = ResolveTestProjectPath(testContext, sourceProject);
        return FindFallbackTestFilePath(sourceFilePath, sourceObjectName, sourceProject.FilePath, testProjectPath);
    }

    private string ResolveTestProjectPath(TestContextCandidate? testContext, CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.Project.FilePath)) return testContext.Project.FilePath;

        if (CanUseBootstrapStateFor(sourceProject.FilePath)) return _context.TestBootstrapState!.TestProjectPath;

        var preferredTestProject = _context.Project.Projects
            .Where(x => x.BuildMetadata.IsTestProject)
            .Where(x => x.SolutionId == sourceProject.SolutionId)
            .OrderByDescending(x => string.Equals(
                Path.GetDirectoryName(x.FilePath),
                Path.GetDirectoryName(sourceProject.FilePath),
                StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.FilePath.Length)
            .FirstOrDefault();

        return preferredTestProject?.FilePath ?? sourceProject.FilePath;
    }

    private string ResolveTestClassContents(
        TestContextCandidate? testContext,
        string sourceObjectName,
        string sourceNamespace,
        CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestClass.FullString)) return testContext.TestClass.FullString;

        var framework = ResolveTestFramework(testContext, sourceProject.FilePath);
        var testNamespace = ResolveTestNamespace(testContext, sourceNamespace, sourceProject);
        var testClassName = ResolveTestClassName(testContext, sourceObjectName);
        return CreateFallbackTestClass(testNamespace, testClassName, framework);
    }

    private string ResolveTestFileContents(
        TestContextCandidate? testContext,
        string sourceFilePath,
        string sourceObjectName,
        string sourceNamespace,
        CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestFileContents)) return testContext.TestFileContents;

        var testFilePath = ResolveTestFilePath(testContext, sourceFilePath, sourceObjectName, sourceProject);
        if (File.Exists(testFilePath)) return File.ReadAllText(testFilePath);

        return ResolveTestClassContents(testContext, sourceObjectName, sourceNamespace, sourceProject);
    }

    private string ResolveTestFramework(TestContextCandidate? testContext, string sourceProjectPath)
    {
        if (CanUseBootstrapStateFor(sourceProjectPath)) return _context.TestBootstrapState!.Framework;

        return DetermineTestFramework(testContext?.TestClass, testContext?.Project);
    }

    private string ResolveTestDependencies(TestContextCandidate? testContext, string sourceProjectPath)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.Dependencies)) return testContext.Dependencies;

        if (CanUseBootstrapStateFor(sourceProjectPath)) return _context.TestBootstrapState!.Dependencies;

        return GetDefaultDependencies(ResolveTestFramework(testContext, sourceProjectPath));
    }

    private bool CanUseBootstrapStateFor(string sourceProjectPath)
    {
        return _context.TestBootstrapState != null &&
               string.Equals(
                   Path.GetFullPath(_context.TestBootstrapState.SourceProjectPath),
                   Path.GetFullPath(sourceProjectPath),
                   StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveTestNamespace(
        TestContextCandidate? testContext,
        string sourceNamespace,
        CSharpProjectModel sourceProject)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestClass.Namespace)) return testContext.TestClass.Namespace;

        if (!string.IsNullOrWhiteSpace(sourceNamespace)) return $"{sourceNamespace}.Tests";

        var testProjectPath = ResolveTestProjectPath(testContext, sourceProject);
        return Path.GetFileNameWithoutExtension(testProjectPath)
            .Replace(".csproj", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCoverageGapSummary(
        IReadOnlyCollection<Persistence.Ef.Entities.Coverage.CoverageGapEntity> coverageGaps)
    {
        if (coverageGaps.Count == 0) return "No line-level coverage gap data is available for this method.";

        var gapLines = coverageGaps
            .Select(gap =>
            {
                var kind = string.Equals(gap.GapKind, CoverageGapKind.PartialBranch.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                    ? "partial branch"
                    : "uncovered line";
                var sourceText = string.IsNullOrWhiteSpace(gap.SourceText)
                    ? string.Empty
                    : $" - `{gap.SourceText}`";

                return
                    $"- Line {gap.LineNumber}: {kind}, hits={gap.Hits}{(string.IsNullOrWhiteSpace(gap.ConditionCoverage) ? string.Empty : $", condition={gap.ConditionCoverage}")}{sourceText}";
            })
            .Distinct()
            .Take(12);

        return "Coverage gaps to target:\n" + string.Join("\n", gapLines);
    }

    private static string BuildMutationSummary(
        IReadOnlyCollection<Persistence.Ef.Entities.MutationTesting.MutantEntity> mutants)
    {
        if (mutants.Count == 0) return "No surviving or no-coverage mutants are available for this method.";

        var mutantLines = mutants
            .OrderBy(x => x.Location.StartLineNumber)
            .ThenBy(x => x.StrykerMutantId)
            .Select(mutant =>
            {
                var id = string.IsNullOrWhiteSpace(mutant.StrykerMutantId)
                    ? mutant.Id.ToString()
                    : mutant.StrykerMutantId;

                // Location: show single line or range (1-based for readability).
                var startLine = mutant.Location.StartLineNumber + 1;
                var endLine   = mutant.Location.EndLineNumber + 1;
                var location  = startLine == endLine
                    ? $"line {startLine}"
                    : $"lines {startLine}-{endLine}";

                var sb = new StringBuilder();
                sb.Append($"- Mutant {id}: {mutant.Status}, {mutant.MutatorName}, {location}");

                if (!string.IsNullOrWhiteSpace(mutant.OriginalCode))
                    sb.Append($"\n  before: `{mutant.OriginalCode}`");

                if (!string.IsNullOrWhiteSpace(mutant.Replacement))
                    sb.Append($"\n  after:  `{mutant.Replacement}`");

                if (!string.IsNullOrWhiteSpace(mutant.StatusReason))
                    sb.Append($"\n  reason: {mutant.StatusReason}");

                if (mutant.CoveredBy.Count > 0)
                    sb.Append($"\n  coveredBy: {string.Join(", ", mutant.CoveredBy.Take(3))}");

                return sb.ToString();
            })
            .Distinct()
            .Take(12);

        return "Mutation evidence to target:\n" + string.Join("\n", mutantLines);
    }

    private async Task<int> GetReachableTestSignalCountAsync(int memberId, CancellationToken cancellationToken)
    {
        var relationshipSignals = await _dbContext.MemberRelationships
            .AsNoTracking()
            .Where(x => x.TargetId == memberId)
            .Where(x => x.RelationshipType == "tests" || x.RelationshipType == "covers")
            .CountAsync(cancellationToken);

        var invocationSignals = await (
                from invocation in _dbContext.Invocations
                join member in _dbContext.Members on invocation.MemberId equals member.Id
                where invocation.InvokedMemberId == memberId && member.IsTestMember
                select invocation)
            .CountAsync(cancellationToken);

        var invocationEdges = await _dbContext.Invocations
            .AsNoTracking()
            .Where(x => x.InvokedMemberId.HasValue)
            .Select(x => new InvocationEdge(x.MemberId, x.InvokedMemberId!.Value))
            .ToListAsync(cancellationToken);
        var maxDepth = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection
            .MaxIndirectInvocationDepth;
        var reachableEntrypointIds = FindCallerPaths(memberId, invocationEdges, maxDepth)
            .Keys
            .ToList();
        var indirectInvocationSignals = reachableEntrypointIds.Count == 0
            ? 0
            : await (
                    from invocation in _dbContext.Invocations
                    join member in _dbContext.Members on invocation.MemberId equals member.Id
                    where invocation.InvokedMemberId.HasValue &&
                          reachableEntrypointIds.Contains(invocation.InvokedMemberId.Value) &&
                          member.IsTestMember
                    select invocation)
                .CountAsync(cancellationToken);

        return relationshipSignals + invocationSignals + indirectInvocationSignals;
    }

    private async Task<List<Persistence.Ef.Entities.MutationTesting.MutantEntity>> GetUndetectedMutantsAsync(
        int memberId,
        CancellationToken cancellationToken)
    {
        var mutants = await _dbContext.Mutants
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .Where(x => x.Status == "Survived" || x.Status == "NoCoverage")
            .ToListAsync(cancellationToken);

        return mutants
            .OrderBy(x => x.Location.StartLineNumber)
            .ThenBy(x => x.StrykerMutantId)
            .ToList();
    }

    private Task<int> GetTestSmellCountAsync(int testMemberId, CancellationToken cancellationToken)
    {
        return _dbContext.TestSmells
            .AsNoTracking()
            .CountAsync(x => x.MemberId == testMemberId, cancellationToken);
    }

    private static CandidateTestAssessment DetermineTestAssessment(
        TestContextCandidate? testContext,
        int testSignals,
        double baselineCoverage,
        int coverageGapCount,
        int undetectedMutantCount,
        int exampleTestSmellCount)
    {
        if (testContext?.IsGrounded != true || testContext.ExampleTestMemberId == null)
            return new CandidateTestAssessment(
                CandidateTestState.NoKnownTest,
                CandidateActionKind.GenerateNewTest,
                "No related baseline test was identified for this source method.");

        var metadataConfidence = testContext.ExampleTestMetadataConfidence ?? 0.0;
        var weakQualitySignals = new List<string>();
        var extensionSignals = new List<string>();

        if (exampleTestSmellCount > 0) weakQualitySignals.Add($"test smell count={exampleTestSmellCount}");

        if (metadataConfidence > 0.0 && metadataConfidence < 0.5)
            weakQualitySignals.Add($"low metadata confidence={metadataConfidence:0.00}");

        if (testSignals <= 0) weakQualitySignals.Add("no direct or indirect test signals were found");

        if (coverageGapCount > 0) extensionSignals.Add($"coverage gaps={coverageGapCount}");

        if (undetectedMutantCount > 0) extensionSignals.Add($"undetected mutants={undetectedMutantCount}");

        if (baselineCoverage < 0.99) extensionSignals.Add($"baseline coverage={baselineCoverage:P0}");

        if (weakQualitySignals.Count > 0)
            return new CandidateTestAssessment(
                CandidateTestState.NeedsTestImprovement,
                CandidateActionKind.ImproveExistingTest,
                "Existing test coverage was found, but the baseline suggests quality issues: " +
                string.Join(", ", weakQualitySignals) + ".");

        if (extensionSignals.Count > 0)
            return new CandidateTestAssessment(
                CandidateTestState.NeedsTestExtension,
                CandidateActionKind.ExtendExistingTestSuite,
                "Existing tests appear usable, but the baseline indicates additional scenarios are still missing: " +
                string.Join(", ", extensionSignals) + ".");

        return new CandidateTestAssessment(
            CandidateTestState.LikelySufficient,
            CandidateActionKind.Skip,
            "Existing tests appear sufficient for the current baseline signals.");
    }

    private async Task<TestContextCandidate?> FindBestTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        MemberVisibility sourceMemberVisibility,
        TestContextMappingMode mappingMode,
        ObjectModel sourceObject,
        FileModel sourceFile,
        CSharpProjectModel sourceProject,
        string solutionFilePath,
        CancellationToken cancellationToken)
    {
        var persistedMappingCandidate = await FindPersistedSourceTestMappingContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceMemberVisibility,
            sourceObject.Name,
            sourceProject,
            cancellationToken);

        if (persistedMappingCandidate != null) return persistedMappingCandidate;

        var directInvocationCandidate = await FindDirectInvocationTestContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceMemberVisibility,
            sourceObject.Name,
            sourceProject,
            cancellationToken);

        if (directInvocationCandidate != null) return directInvocationCandidate;

        var indirectInvocationCandidate = await FindIndirectInvocationTestContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceMemberVisibility,
            sourceObject.Name,
            sourceProject,
            cancellationToken);

        if (indirectInvocationCandidate != null) return indirectInvocationCandidate;

        var helperMediatedCandidate = await FindHelperMediatedTestContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceObject.Name,
            sourceProject,
            cancellationToken);

        if (helperMediatedCandidate != null) return helperMediatedCandidate;

        var symbolFinderCandidate = await FindRoslynSymbolFinderTestContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceMemberVisibility,
            sourceObject.Name,
            sourceFile,
            sourceProject,
            solutionFilePath,
            cancellationToken);

        if (symbolFinderCandidate != null) return symbolFinderCandidate;

        if (mappingMode == TestContextMappingMode.DirectInvocationOnly)
        {
            _context.Project.Logger?.Warning(
                "No direct, indirect, helper-mediated, or SymbolFinder test context found for source member {SourceMemberId} ({SourceMemberName}); score-based context selection is disabled by {MappingMode}.",
                sourceMemberId,
                sourceMemberName,
                mappingMode);
            return null;
        }

        var relationshipCandidate = await FindMemberRelationshipTestContextAsync(
            sourceMemberId,
            sourceMemberName,
            sourceObject.Name,
            sourceProject,
            cancellationToken);

        if (relationshipCandidate != null) return relationshipCandidate;

        var candidates = await (
                from testObject in _dbContext.Objects
                join testFile in _dbContext.Files on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects on testFile.CSharpProjectId equals testProject.Id
                where testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                select new
                {
                    TestObject = testObject,
                    TestFile = testFile,
                    TestProject = testProject
                })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return null;

        var bestCandidate = candidates
            .Select(x => new TestContextCandidate(
                x.TestObject.ToDomain(),
                x.TestFile.ToDomain(),
                x.TestProject.ToDomain(),
                ScoreTestContext(sourceObject, sourceFile, x.TestObject, x.TestFile, x.TestProject)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TestFile.FilePath.Length)
            .FirstOrDefault();

        if (bestCandidate == null) return null;

        var testMembers = await _dbContext.Members
            .Where(x => x.ObjectEntityId == bestCandidate.TestClass.Id)
            .ToListAsync(cancellationToken);

        var exampleCandidates = testMembers
            .Where(x => x.IsTestMember && x.Kind == "method")
            .ToList();

        var selectedExample = exampleCandidates
            .OrderByDescending(x => ScoreExampleTest(sourceObject, x))
            .ThenBy(x => x.IsGenerated)
            .ThenBy(x => x.Name)
            .FirstOrDefault();

        var dependencies = BuildDependencies(bestCandidate.TestFile, bestCandidate.TestClass, bestCandidate.Project);
        var testFileContents = await TryReadFileAsync(bestCandidate.TestFile.FilePath, cancellationToken)
                               ?? bestCandidate.TestClass.FullString;
        var supportContext = BuildSupportContext(testMembers, selectedExample);

        return bestCandidate with
        {
            ExampleTestMemberId = selectedExample?.Id,
            ExampleTestMethodName = selectedExample?.Name,
            ExampleTest = selectedExample?.FullString,
            ExampleTestMemberLocation = selectedExample?.Location,
            ExampleTestMetadataSummary = BuildExampleTestMetadataSummary(selectedExample),
            ExampleTestMetadataConfidence = selectedExample?.TestMetadataConfidence,
            ProjectTestMetadataSummary = BuildProjectTestMetadataSummary(exampleCandidates),
            Dependencies = dependencies,
            TestFileContents = testFileContents,
            SupportContext = supportContext,
            SupportBindings = DiscoverContextBindings(testMembers, selectedExample, sourceObject.Name),
            EvidenceKind = "WeakHeuristic",
            EvidenceSummary = BuildHeuristicEvidenceSummary(sourceObject, bestCandidate, selectedExample),
            IsGrounded = false
        };
    }

    private async Task<TestContextCandidate?> FindPersistedSourceTestMappingContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        MemberVisibility sourceMemberVisibility,
        string sourceObjectName,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
        var mapping = await _dbContext.SourceTestMappings
            .AsNoTracking()
            .Include(x => x.TraceSteps)
            .Where(x => x.SourceMemberId == sourceMemberId && x.ProjectId == _context.Project.DbId && x.IsGrounded)
            .OrderBy(x => x.PathLength)
            .ThenByDescending(x => x.Confidence)
            .ThenBy(x => x.TestMemberId)
            .FirstOrDefaultAsync(cancellationToken);
        if (mapping == null) return null;

        var candidate = await (
                from testMember in _dbContext.Members.AsNoTracking()
                join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                where testMember.Id == mapping.TestMemberId
                      && testMember.IsTestMember
                      && testMember.Kind == "method"
                      && testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                select new
                {
                    TestMember = testMember,
                    TestObject = testObject,
                    TestFile = testFile,
                    TestProject = testProject
                })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate == null) return null;

        var path = mapping.TraceSteps
            .OrderBy(x => x.StepIndex)
            .Select(x => x.ToMemberId)
            .ToList();
        if (path.Count == 0) path = [sourceMemberId];

        var accessPath = new AccessPath
        {
            TargetMemberId = sourceMemberId,
            EntrypointMemberId = path[0],
            PathMemberIds = path,
            Strategy = Enum.TryParse<TestAccessStrategy>(mapping.AccessPathStrategy, out var strategy)
                ? strategy
                : ResolveDirectAccessStrategy(sourceMemberVisibility),
            IsLegalFromTest = true,
            RequiresReflection = false
        };

        var row = new TestContextEvidenceRow(
            candidate.TestMember,
            candidate.TestObject,
            candidate.TestFile,
            candidate.TestProject,
            mapping.EvidenceKind,
            mapping.Summary,
            true,
            accessPath,
            path)
        {
            SourceTestMappingId = mapping.Id
        };

        return await BuildGroundedTestContextAsync(row, sourceObjectName, cancellationToken);
    }

    private async Task<TestContextCandidate?> FindDirectInvocationTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        MemberVisibility sourceMemberVisibility,
        string sourceObjectName,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
        var candidate = await (
                from invocation in _dbContext.Invocations.AsNoTracking()
                join testMember in _dbContext.Members.AsNoTracking() on invocation.MemberId equals testMember.Id
                join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                where invocation.InvokedMemberId == sourceMemberId
                      && testMember.IsTestMember
                      && testMember.Kind == "method"
                      && testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                orderby testMember.IsGenerated,
                    testFile.FilePath.Length,
                    testMember.Name
                select new TestContextEvidenceRow(
                    testMember,
                    testObject,
                    testFile,
                    testProject,
                    "DirectInvocation",
                    $"Test method '{testObject.Name}.{testMember.Name}' directly invokes source member '{sourceMemberName}' via invocation.MemberId={testMember.Id} -> invocation.InvokedMemberId={sourceMemberId}.",
                    true,
                    null,
                    Array.Empty<int>()))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate == null) return null;

        var accessPath = new AccessPath
        {
            TargetMemberId = sourceMemberId,
            EntrypointMemberId = sourceMemberId,
            PathMemberIds = [sourceMemberId],
            Strategy = ResolveDirectAccessStrategy(sourceMemberVisibility),
            IsLegalFromTest = sourceMemberVisibility is MemberVisibility.Public or MemberVisibility.Internal,
            RequiresReflection = false
        };

        candidate = candidate with
        {
            AccessPath = accessPath,
            PathMemberIds = [sourceMemberId]
        };

        return candidate == null
            ? null
            : await BuildGroundedTestContextAsync(candidate, sourceObjectName, cancellationToken);
    }

    private async Task<TestContextCandidate?> FindIndirectInvocationTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        MemberVisibility sourceMemberVisibility,
        string sourceObjectName,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
        var invocationEdges = await _dbContext.Invocations
            .AsNoTracking()
            .Where(x => x.InvokedMemberId.HasValue)
            .Select(x => new InvocationEdge(x.MemberId, x.InvokedMemberId!.Value))
            .ToListAsync(cancellationToken);
        var maxDepth = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection
            .MaxIndirectInvocationDepth;
        var pathsByEntrypoint = FindCallerPaths(sourceMemberId, invocationEdges, maxDepth);
        if (pathsByEntrypoint.Count == 0) return null;

        var entrypointIds = pathsByEntrypoint.Keys.ToList();
        var candidateRows = await (
                from invocation in _dbContext.Invocations.AsNoTracking()
                join testMember in _dbContext.Members.AsNoTracking() on invocation.MemberId equals testMember.Id
                join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                join entrypointMember in _dbContext.Members.AsNoTracking() on invocation.InvokedMemberId equals entrypointMember.Id
                join entrypointObject in _dbContext.Objects.AsNoTracking() on entrypointMember.ObjectEntityId equals entrypointObject.Id
                join entrypointFile in _dbContext.Files.AsNoTracking() on entrypointObject.FileId equals entrypointFile.Id
                join entrypointProject in _dbContext.CSharpProjects.AsNoTracking() on entrypointFile.CSharpProjectId equals entrypointProject.Id
                where invocation.InvokedMemberId.HasValue
                      && entrypointIds.Contains(invocation.InvokedMemberId.GetValueOrDefault())
                      && testMember.IsTestMember
                      && testMember.Kind == "method"
                      && testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                      && entrypointProject.SolutionId == sourceProject.SolutionId
                select new
                {
                    TestMember = testMember,
                    TestObject = testObject,
                    TestFile = testFile,
                    TestProject = testProject,
                    Entrypoint = entrypointMember,
                    EntrypointProject = entrypointProject
                })
            .ToListAsync(cancellationToken);

        var selected = candidateRows
            .Select(row => new
            {
                row.TestMember,
                row.TestObject,
                row.TestFile,
                row.TestProject,
                row.Entrypoint,
                row.EntrypointProject,
                Path = pathsByEntrypoint[row.Entrypoint.Id]
            })
            .Where(x => !x.EntrypointProject.BuildMetadata.IsTestProject)
            .OrderBy(x => x.Path.Count)
            .ThenBy(x => x.EntrypointProject.Id == sourceProject.Id ? 0 : 1)
            .ThenBy(x => x.TestMember.IsGenerated)
            .ThenBy(x => x.TestFile.FilePath.Length)
            .ThenBy(x => x.TestMember.Name)
            .FirstOrDefault();

        if (selected == null) return null;

        var pathText = string.Join(" -> ", selected.Path);
        return await BuildGroundedTestContextAsync(
            new TestContextEvidenceRow(
                selected.TestMember,
                selected.TestObject,
                selected.TestFile,
                selected.TestProject,
                "IndirectInvocation",
                $"Test method '{selected.TestObject.Name}.{selected.TestMember.Name}' invokes entrypoint '{selected.Entrypoint.Name}', which reaches source member '{sourceMemberName}' through invocation path {pathText}.",
                true,
                new AccessPath
                {
                    TargetMemberId = sourceMemberId,
                    EntrypointMemberId = selected.Entrypoint.Id,
                    PathMemberIds = selected.Path,
                    Strategy = ResolveCallerPathStrategy(ResolveMemberVisibility(selected.Entrypoint)),
                    IsLegalFromTest = ResolveMemberVisibility(selected.Entrypoint) is MemberVisibility.Public or MemberVisibility.Internal,
                    RequiresReflection = false
                },
                selected.Path),
            sourceObjectName,
            cancellationToken);
    }

    private async Task<TestContextCandidate?> FindHelperMediatedTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        string sourceObjectName,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
        var invocationEdges = await _dbContext.Invocations
            .AsNoTracking()
            .Where(x => x.InvokedMemberId.HasValue)
            .Select(x => new InvocationEdge(x.MemberId, x.InvokedMemberId!.Value))
            .ToListAsync(cancellationToken);
        var maxDepth = _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection
            .MaxIndirectInvocationDepth;
        var pathsByEntrypoint = FindCallerPaths(sourceMemberId, invocationEdges, maxDepth);
        if (pathsByEntrypoint.Count == 0) return null;

        var entrypointIds = pathsByEntrypoint.Keys.ToList();
        var candidateRows = await (
                from helperInvocation in _dbContext.Invocations.AsNoTracking()
                join helperMember in _dbContext.Members.AsNoTracking() on helperInvocation.MemberId equals helperMember.Id
                join testInvocation in _dbContext.Invocations.AsNoTracking() on helperMember.Id equals testInvocation.InvokedMemberId
                join testMember in _dbContext.Members.AsNoTracking() on testInvocation.MemberId equals testMember.Id
                join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                join entrypointMember in _dbContext.Members.AsNoTracking() on helperInvocation.InvokedMemberId equals entrypointMember.Id
                where helperInvocation.InvokedMemberId.HasValue
                      && entrypointIds.Contains(helperInvocation.InvokedMemberId.GetValueOrDefault())
                      && helperMember.ObjectEntityId == testObject.Id
                      && !helperMember.IsTestMember
                      && !helperMember.IsGenerated
                      && testMember.IsTestMember
                      && testMember.Kind == "method"
                      && testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                select new
                {
                    TestMember = testMember,
                    TestObject = testObject,
                    TestFile = testFile,
                    TestProject = testProject,
                    HelperMember = helperMember,
                    Entrypoint = entrypointMember
                })
            .ToListAsync(cancellationToken);

        var selected = candidateRows
            .Where(row => IsLikelyTestSupportMember(row.HelperMember))
            .Select(row =>
            {
                var sourcePath = pathsByEntrypoint[row.Entrypoint.Id];
                var fullPath = new[] { row.HelperMember.Id }.Concat(sourcePath).ToList();
                return new
                {
                    row.TestMember,
                    row.TestObject,
                    row.TestFile,
                    row.TestProject,
                    row.HelperMember,
                    row.Entrypoint,
                    Path = fullPath
                };
            })
            .OrderBy(x => x.Path.Count)
            .ThenBy(x => x.TestMember.IsGenerated)
            .ThenBy(x => x.TestFile.FilePath.Length)
            .ThenBy(x => x.TestMember.Name)
            .FirstOrDefault();

        if (selected == null) return null;

        var entrypointVisibility = ResolveMemberVisibility(selected.Entrypoint);
        var pathText = string.Join(" -> ", selected.Path);
        return await BuildGroundedTestContextAsync(
            new TestContextEvidenceRow(
                selected.TestMember,
                selected.TestObject,
                selected.TestFile,
                selected.TestProject,
                "HelperMediatedPath",
                $"Test method '{selected.TestObject.Name}.{selected.TestMember.Name}' invokes helper '{selected.HelperMember.Name}', which invokes entrypoint '{selected.Entrypoint.Name}' and reaches source member '{sourceMemberName}' through invocation path {pathText}.",
                true,
                new AccessPath
                {
                    TargetMemberId = sourceMemberId,
                    EntrypointMemberId = selected.HelperMember.Id,
                    PathMemberIds = selected.Path,
                    Strategy = TestAccessStrategy.HelperMediatedPath,
                    IsLegalFromTest = entrypointVisibility is MemberVisibility.Public or MemberVisibility.Internal,
                    RequiresReflection = false
                },
                selected.Path),
            sourceObjectName,
            cancellationToken);
    }

    private async Task<TestContextCandidate?> FindRoslynSymbolFinderTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        MemberVisibility sourceMemberVisibility,
        string sourceObjectName,
        FileModel sourceFile,
        CSharpProjectModel sourceProject,
        string solutionFilePath,
        CancellationToken cancellationToken)
    {
        if (_staticAnalysisWorkspace == null) return null;

        try
        {
            var solution = await OpenRoslynSolutionAsync(solutionFilePath, sourceProject.FilePath, cancellationToken);
            var sourceDocument = FindDocumentByPath(solution, sourceFile.FilePath);
            if (sourceDocument == null) return null;

            var targetSymbol = await FindSourceMemberSymbolAsync(
                sourceDocument,
                sourceMemberName,
                cancellationToken);
            if (targetSymbol == null) return null;

            var references = await SymbolFinder.FindReferencesAsync(targetSymbol, solution, cancellationToken);
            var referenceCandidates = new List<RoslynReferenceCandidate>();
            foreach (var referencedSymbol in references)
            foreach (var location in referencedSymbol.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!location.Location.IsInSource || location.Location.SourceTree == null) continue;

                var document = solution.GetDocument(location.Location.SourceTree);
                if (document?.FilePath == null) continue;

                var containingMemberName = await FindContainingMemberNameAsync(
                    document,
                    location.Location.SourceSpan.Start,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(containingMemberName)) continue;

                referenceCandidates.Add(new RoslynReferenceCandidate(
                    NormalizeFilePath(document.FilePath),
                    containingMemberName));
            }

            if (referenceCandidates.Count == 0) return null;

            var memberNames = referenceCandidates
                .Select(x => x.MemberName)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var candidateRows = await (
                    from testMember in _dbContext.Members.AsNoTracking()
                    join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                    join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                    join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                    where memberNames.Contains(testMember.Name)
                          && testMember.IsTestMember
                          && testMember.Kind == "method"
                          && testObject.IsTestObject
                          && testProject.SolutionId == sourceProject.SolutionId
                    select new
                    {
                        TestMember = testMember,
                        TestObject = testObject,
                        TestFile = testFile,
                        TestProject = testProject
                    })
                .ToListAsync(cancellationToken);

            var selected = candidateRows
                .Where(row => referenceCandidates.Any(candidate =>
                    string.Equals(candidate.MemberName, row.TestMember.Name, StringComparison.Ordinal) &&
                    string.Equals(candidate.FilePath, NormalizeFilePath(row.TestFile.FilePath),
                        StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.TestMember.IsGenerated)
                .ThenBy(x => x.TestFile.FilePath.Length)
                .ThenBy(x => x.TestMember.Name)
                .FirstOrDefault();

            if (selected == null) return null;

            _context.Project.Logger?.Information(
                "SymbolFinder fallback mapped source member {SourceMemberId} ({SourceMemberName}) to test member {TestMemberId} ({TestMemberName}).",
                sourceMemberId,
                sourceMemberName,
                selected.TestMember.Id,
                selected.TestMember.Name);

            return await BuildGroundedTestContextAsync(
                new TestContextEvidenceRow(
                    selected.TestMember,
                    selected.TestObject,
                    selected.TestFile,
                    selected.TestProject,
                    "RoslynSymbolFinder",
                    $"Roslyn SymbolFinder found a direct reference from test method '{selected.TestObject.Name}.{selected.TestMember.Name}' to source member '{sourceMemberName}' after persisted invocation edges produced no mapping.",
                    true,
                    new AccessPath
                    {
                        TargetMemberId = sourceMemberId,
                        EntrypointMemberId = sourceMemberId,
                        PathMemberIds = [sourceMemberId],
                        Strategy = ResolveDirectAccessStrategy(sourceMemberVisibility),
                        IsLegalFromTest = sourceMemberVisibility is MemberVisibility.Public or MemberVisibility.Internal,
                        RequiresReflection = false
                    },
                    [sourceMemberId]),
                sourceObjectName,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _context.Project.Logger?.Warning(
                "SymbolFinder fallback failed for source member {SourceMemberId} ({SourceMemberName}): {Message}",
                sourceMemberId,
                sourceMemberName,
                ex.Message);
            return null;
        }
    }

    private async Task<Solution> OpenRoslynSolutionAsync(
        string solutionFilePath,
        string sourceProjectPath,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(solutionFilePath))
        {
            try
            {
                return await _staticAnalysisWorkspace!.OpenSolutionAsync(solutionFilePath, cancellationToken);
            }
            catch when (!string.IsNullOrWhiteSpace(sourceProjectPath))
            {
                var project = await _staticAnalysisWorkspace!.OpenProjectAsync(sourceProjectPath, cancellationToken);
                return project.Solution;
            }
        }

        var fallbackProject = await _staticAnalysisWorkspace!.OpenProjectAsync(sourceProjectPath, cancellationToken);
        return fallbackProject.Solution;
    }

    private static Document? FindDocumentByPath(Solution solution, string filePath)
    {
        var normalizedPath = NormalizeFilePath(filePath);
        return solution.Projects
            .SelectMany(x => x.Documents)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.FilePath) &&
                                 string.Equals(NormalizeFilePath(x.FilePath), normalizedPath,
                                     StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<ISymbol?> FindSourceMemberSymbolAsync(
        Document document,
        string sourceMemberName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root == null || semanticModel == null) return null;

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                     .Where(x => x.Identifier.Text == sourceMemberName))
        {
            var symbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
            if (symbol != null) return symbol;
        }

        foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
                     .Where(x => x.Identifier.Text == sourceMemberName))
        {
            var symbol = semanticModel.GetDeclaredSymbol(constructor, cancellationToken);
            if (symbol != null) return symbol;
        }

        return null;
    }

    private static async Task<string?> FindContainingMemberNameAsync(
        Document document,
        int position,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root == null || semanticModel == null) return null;

        var token = root.FindToken(position);
        var member = token.Parent?
            .AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(x => x is MethodDeclarationSyntax or ConstructorDeclarationSyntax);

        if (member == null) return null;

        var symbol = semanticModel.GetDeclaredSymbol(member, cancellationToken);
        return symbol?.Name;
    }

    private static string NormalizeFilePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private async Task<TestContextCandidate?> FindMemberRelationshipTestContextAsync(
        int sourceMemberId,
        string sourceMemberName,
        string sourceObjectName,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
        var candidates = await (
                from relationship in _dbContext.MemberRelationships.AsNoTracking()
                join testMember in _dbContext.Members.AsNoTracking() on relationship.SourceId equals testMember.Id
                join testObject in _dbContext.Objects.AsNoTracking() on testMember.ObjectEntityId equals testObject.Id
                join testFile in _dbContext.Files.AsNoTracking() on testObject.FileId equals testFile.Id
                join testProject in _dbContext.CSharpProjects.AsNoTracking() on testFile.CSharpProjectId equals testProject.Id
                where relationship.TargetId == sourceMemberId
                      && (relationship.RelationshipType == "tests" || relationship.RelationshipType == "covers")
                      && testMember.IsTestMember
                      && testMember.Kind == "method"
                      && testObject.IsTestObject
                      && testProject.SolutionId == sourceProject.SolutionId
                orderby testMember.IsGenerated,
                    testFile.FilePath.Length,
                    testMember.Name
                select new TestContextEvidenceRow(
                    testMember,
                    testObject,
                    testFile,
                    testProject,
                    "MemberRelationship",
                    $"Test method '{testObject.Name}.{testMember.Name}' maps to source member '{sourceMemberName}' through member relationship '{relationship.RelationshipType}' ({testMember.Id} -> {sourceMemberId}).",
                    true,
                    null,
                    Array.Empty<int>()))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidates != null) candidates = candidates with { PathMemberIds = [sourceMemberId] };

        return candidates == null
            ? null
            : await BuildGroundedTestContextAsync(candidates, sourceObjectName, cancellationToken);
    }

    private async Task<TestContextCandidate> BuildGroundedTestContextAsync(
        TestContextEvidenceRow evidence,
        string sourceObjectName,
        CancellationToken cancellationToken)
    {
        var testClass = evidence.TestObject.ToDomain();
        var testFile = evidence.TestFile.ToDomain();
        var testProject = evidence.TestProject.ToDomain();
        var testMembers = await _dbContext.Members
            .Where(x => x.ObjectEntityId == evidence.TestObject.Id)
            .ToListAsync(cancellationToken);
        var exampleCandidates = testMembers
            .Where(x => x.IsTestMember && x.Kind == "method")
            .ToList();
        var dependencies = BuildDependencies(testFile, testClass, testProject);
        var testFileContents = await TryReadFileAsync(testFile.FilePath, cancellationToken)
                               ?? testClass.FullString;
        var supportContext = BuildSupportContext(testMembers, evidence.TestMember);

        return new TestContextCandidate(
            testClass,
            testFile,
            testProject,
            0)
        {
            ExampleTestMemberId = evidence.TestMember.Id,
            ExampleTestMethodName = evidence.TestMember.Name,
            ExampleTest = evidence.TestMember.FullString,
            ExampleTestMemberLocation = evidence.TestMember.Location,
            ExampleTestMetadataSummary = BuildExampleTestMetadataSummary(evidence.TestMember),
            ExampleTestMetadataConfidence = evidence.TestMember.TestMetadataConfidence,
            ProjectTestMetadataSummary = BuildProjectTestMetadataSummary(exampleCandidates),
            Dependencies = dependencies,
            TestFileContents = testFileContents,
            SupportContext = supportContext,
            SupportBindings = DiscoverContextBindings(testMembers, evidence.TestMember, sourceObjectName),
            EvidenceKind = evidence.EvidenceKind,
            EvidenceSummary = evidence.EvidenceSummary,
            IsGrounded = evidence.IsGrounded,
            AccessPath = evidence.AccessPath,
            MappingPathMemberIds = evidence.PathMemberIds,
            SourceTestMappingId = evidence.SourceTestMappingId
        };
    }

    private static async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private static int ScoreExampleTest(ObjectModel sourceObject, Persistence.Ef.Entities.Code.MemberEntity testMember)
    {
        var score = 0;

        if (!testMember.IsGenerated) score += 40;

        if (!string.IsNullOrWhiteSpace(testMember.TestIntent)) score += 25;

        if (testMember.TestCategories.Count > 0) score += 10 + testMember.TestCategories.Count * 2;

        if (!string.IsNullOrWhiteSpace(testMember.TestMetadataSource)) score += 8;

        if (testMember.TestMetadataConfidence.HasValue) score += (int)(testMember.TestMetadataConfidence.Value * 20);

        if (testMember.Name.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase)) score += 20;

        if (!string.IsNullOrWhiteSpace(testMember.TestIntent) &&
            testMember.TestIntent.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase))
            score += 25;

        return score;
    }

    private static string BuildHeuristicEvidenceSummary(
        ObjectModel sourceObject,
        TestContextCandidate bestCandidate,
        Persistence.Ef.Entities.Code.MemberEntity? selectedExample)
    {
        var example = selectedExample == null
            ? "No example test method was selected."
            : $"Style example '{bestCandidate.TestClass.Name}.{selectedExample.Name}' was selected from the highest-scoring test class.";
        return
            $"No direct invocation or explicit member relationship mapped to source object '{sourceObject.Name}'. " +
            $"Heuristic style context '{bestCandidate.TestClass.Name}' was selected with score {bestCandidate.Score}. " +
            $"{example} This context is not treated as a paired existing test.";
    }

    private static string AppendContextMappingReason(string reason, TestContextCandidate? testContext)
    {
        var contextReason = testContext == null
            ? "Context mapping: no test context selected."
            : $"Context mapping: {testContext.EvidenceKind}; grounded={testContext.IsGrounded}; {testContext.EvidenceSummary}";

        return string.IsNullOrWhiteSpace(reason)
            ? contextReason
            : $"{reason} {contextReason}";
    }

    private void LogContextMapping(
        MemberModel sourceMember,
        ObjectModel sourceObject,
        TestContextCandidate? testContext)
    {
        _context.Project.Logger?.Information(
            "Generation context mapping: SourceMemberId={SourceMemberId}, Source={SourceNamespace}.{SourceClass}.{SourceMethod}, ExistingTestMemberId={TestMemberId}, Test={TestNamespace}.{TestClass}.{TestMethod}, EvidenceKind={EvidenceKind}, EvidenceSummary={EvidenceSummary}, Score={Score}, IsGrounded={IsGrounded}",
            sourceMember.Id,
            sourceObject.Namespace,
            sourceObject.Name,
            sourceMember.Name,
            testContext?.IsGrounded == true ? testContext.ExampleTestMemberId : null,
            testContext?.TestClass.Namespace,
            testContext?.TestClass.Name,
            testContext?.IsGrounded == true ? testContext.ExampleTestMethodName : null,
            testContext?.EvidenceKind ?? "None",
            testContext?.EvidenceSummary ?? "No test context selected.",
            testContext?.Score ?? 0,
            testContext?.IsGrounded ?? false);
    }

    private static string BuildExampleTestMetadataSummary(Persistence.Ef.Entities.Code.MemberEntity? exampleTest)
    {
        if (exampleTest == null) return "No enriched example test metadata is available.";

        var categories = exampleTest.TestCategories.Count > 0
            ? string.Join(", ", exampleTest.TestCategories)
            : "Unspecified";
        var intent = string.IsNullOrWhiteSpace(exampleTest.TestIntent)
            ? "Unspecified"
            : exampleTest.TestIntent;
        var source = string.IsNullOrWhiteSpace(exampleTest.TestMetadataSource)
            ? "Unknown"
            : exampleTest.TestMetadataSource;
        var confidence = exampleTest.TestMetadataConfidence.HasValue
            ? exampleTest.TestMetadataConfidence.Value.ToString("0.00")
            : "n/a";

        return
            $"Example categories: {categories}\nExample intent: {intent}\nMetadata source: {source}\nMetadata confidence: {confidence}";
    }

    private static string BuildProjectTestMetadataSummary(
        IReadOnlyCollection<Persistence.Ef.Entities.Code.MemberEntity> testMembers)
    {
        if (testMembers.Count == 0) return "No enriched project test metadata is available.";

        var topCategories = testMembers
            .SelectMany(x => x.TestCategories)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Take(5)
            .Select(x => $"{x.Key} ({x.Count()})")
            .ToList();

        var sampleIntents = testMembers
            .Select(x => x.TestIntent)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var categoryLine = topCategories.Count > 0
            ? $"Common categories: {string.Join(", ", topCategories)}"
            : "Common categories: none";
        var intentLine = sampleIntents.Count > 0
            ? $"Sample intents: {string.Join(" | ", sampleIntents)}"
            : "Sample intents: none";

        return $"{categoryLine}\n{intentLine}";
    }

    private static int ScoreTestContext(
        ObjectModel sourceObject,
        FileModel sourceFile,
        Persistence.Ef.Entities.Code.ObjectEntity testObject,
        Persistence.Ef.Entities.Code.FileEntity testFile,
        Persistence.Ef.Entities.Code.CSharpProjectEntity testProject)
    {
        var score = 0;
        var expectedName = $"{sourceObject.Name}Tests";

        if (string.Equals(testObject.Name, expectedName, StringComparison.OrdinalIgnoreCase))
            score += 100;
        else if (testObject.Name.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase)) score += 50;

        var sourceNamespace = sourceObject.Namespace ?? string.Empty;
        var testNamespace = testObject.Namespace ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sourceNamespace) && !string.IsNullOrWhiteSpace(testNamespace))
        {
            if (string.Equals(testNamespace, $"{sourceNamespace}.Tests", StringComparison.OrdinalIgnoreCase))
                score += 40;
            else if (testNamespace.Contains(sourceNamespace, StringComparison.OrdinalIgnoreCase)) score += 20;
        }

        var sourceBaseName = Path.GetFileNameWithoutExtension(sourceFile.FilePath);
        var testBaseName = Path.GetFileNameWithoutExtension(testFile.FilePath);
        if (string.Equals(testBaseName, $"{sourceBaseName}Tests", StringComparison.OrdinalIgnoreCase))
            score += 30;
        else if (testBaseName.Contains(sourceBaseName, StringComparison.OrdinalIgnoreCase)) score += 10;

        if (testProject.BuildMetadata.IsTestProject) score += 15;

        return score;
    }

    private static string ExtractMethodSignature(string sourceCode, string methodName)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) return $"public void {methodName}()";

        var lines = sourceCode.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var signature = lines.FirstOrDefault(line =>
            line.Contains(methodName, StringComparison.Ordinal) &&
            line.Contains('('));

        return signature ?? $"public void {methodName}()";
    }

    private static string FindFallbackTestFilePath(
        string sourceFilePath,
        string sourceObjectName,
        string sourceProjectPath,
        string testProjectPath)
    {
        var sourceProjectDirectory = Path.GetDirectoryName(sourceProjectPath) ?? string.Empty;
        var testProjectDirectory = Path.GetDirectoryName(testProjectPath) ?? sourceProjectDirectory;
        var sourceDirectory = Path.GetDirectoryName(sourceFilePath) ?? sourceProjectDirectory;
        var relativeDirectory = string.Empty;

        try
        {
            relativeDirectory = Path.GetRelativePath(sourceProjectDirectory, sourceDirectory);
        }
        catch
        {
            relativeDirectory = string.Empty;
        }

        if (string.Equals(relativeDirectory, ".", StringComparison.Ordinal)) relativeDirectory = string.Empty;

        var directory = string.IsNullOrWhiteSpace(relativeDirectory)
            ? testProjectDirectory
            : Path.Combine(testProjectDirectory, relativeDirectory);
        var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var testFileName = string.IsNullOrWhiteSpace(fileName) ? $"{sourceObjectName}Tests.cs" : $"{fileName}Tests.cs";
        return Path.Combine(directory, testFileName);
    }

    private static string ResolveTargetBuildFramework(CSharpProjectModel? testProject, CSharpProjectModel sourceProject)
    {
        // Bootstrap mode may inject a runtime test project that is not present in the analysis database yet.
        var selectedProject = testProject ?? sourceProject;
        return selectedProject.BuildMetadata.DefaultBuildTarget;
    }

    private static string DetermineTestFramework(ObjectModel? testClass, CSharpProjectModel? project)
    {
        if (!string.IsNullOrWhiteSpace(testClass?.TestFramework)) return testClass.TestFramework;

        var notes = project?.BuildMetadata.Notes ?? string.Empty;

        if (notes.Contains("xunit", StringComparison.OrdinalIgnoreCase)) return "xUnit";

        if (notes.Contains("mstest", StringComparison.OrdinalIgnoreCase)) return "MSTest";

        return "NUnit";
    }

    private static string BuildDependencies(FileModel testFile, ObjectModel testClass, CSharpProjectModel project)
    {
        var framework = DetermineTestFramework(testClass, project);
        var dependencies = testFile.UsingStatements
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var frameworkUsing = framework switch
        {
            "xUnit" => "using Xunit;",
            "MSTest" => "using Microsoft.VisualStudio.TestTools.UnitTesting;",
            _ => "using NUnit.Framework;"
        };

        if (!dependencies.Contains(frameworkUsing, StringComparer.Ordinal)) dependencies.Insert(0, frameworkUsing);

        return string.Join(Environment.NewLine, dependencies);
    }

    private static string CreateFallbackExampleTest(string methodName, string framework)
    {
        var attribute = framework switch
        {
            "xUnit" => "[Fact]",
            "MSTest" => "[TestMethod]",
            _ => "[Test]"
        };

        var assertion = framework switch
        {
            "xUnit" => "Assert.NotNull(result);",
            "MSTest" => "Assert.IsNotNull(result);",
            _ => "Assert.That(result, Is.Not.Null);"
        };

        return $@"
{attribute}
public void {methodName}_Example()
{{
    // Arrange
    var sut = new SystemUnderTest();

    // Act
    var result = sut.DoSomething();

    // Assert
    {assertion}
}}";
    }

    private static string CreateFallbackTestClass(string testNamespace, string testClassName, string framework)
    {
        var classAttribute = framework switch
        {
            "MSTest" => "[TestClass]",
            "xUnit" => string.Empty,
            _ => "[TestFixture]"
        };

        return $@"
{GetDefaultDependencies(framework)}

namespace {testNamespace};

{classAttribute}
public class {testClassName}
{{
}}".Trim();
    }

    private static string GetDefaultDependencies(string framework)
    {
        return framework switch
        {
            "xUnit" => @"using Xunit;
using System;",
            "MSTest" => @"using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;",
            _ => @"using NUnit.Framework;
using System;"
        };
    }

    private static string BuildSupportContext(
        IReadOnlyCollection<Persistence.Ef.Entities.Code.MemberEntity> members,
        Persistence.Ef.Entities.Code.MemberEntity? selectedExample)
    {
        var helperMembers = members
            .Where(x => !x.IsGenerated)
            .Where(x => x.Id != selectedExample?.Id)
            .Where(x =>
                !x.IsTestMember ||
                x.Name.Contains("SetUp", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Initialize", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Fixture", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"// {x.Kind} {x.Name}\n{x.FullString}")
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();

        return helperMembers.Count == 0
            ? "No additional helper methods, setup hooks, or support members were discovered in the selected test file."
            : string.Join("\n\n", helperMembers);
    }

    private sealed record TestContextCandidate(
        ObjectModel TestClass,
        FileModel TestFile,
        CSharpProjectModel Project,
        int Score)
    {
        public int? ExampleTestMemberId { get; init; }
        public string? ExampleTestMethodName { get; init; }
        public string? ExampleTest { get; init; }
        public Models.Code.Location? ExampleTestMemberLocation { get; init; }
        public string? ExampleTestMetadataSummary { get; init; }
        public double? ExampleTestMetadataConfidence { get; init; }
        public string? ProjectTestMetadataSummary { get; init; }
        public string? Dependencies { get; init; }
        public string? TestFileContents { get; init; }
        public string? SupportContext { get; init; }
        public string EvidenceKind { get; init; } = "None";
        public string EvidenceSummary { get; init; } = string.Empty;
        public bool IsGrounded { get; init; }
        public AccessPath? AccessPath { get; init; }
        public IReadOnlyList<int> MappingPathMemberIds { get; init; } = [];
        public IReadOnlyList<ContextBinding> SupportBindings { get; init; } = [];
        public int? SourceTestMappingId { get; init; }
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> FindCallerPaths(
        int targetMemberId,
        IReadOnlyCollection<InvocationEdge> edges,
        int maxDepth)
    {
        var callersByInvokedMemberId = edges
            .Where(x => x.CallerMemberId != x.InvokedMemberId)
            .GroupBy(x => x.InvokedMemberId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(edge => edge.CallerMemberId).Distinct().OrderBy(id => id).ToList());
        var pathsByEntrypoint = new Dictionary<int, IReadOnlyList<int>>();
        var queue = new Queue<IReadOnlyList<int>>();
        queue.Enqueue([targetMemberId]);

        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();
            var currentMemberId = currentPath[0];
            var currentDepth = currentPath.Count - 1;
            if (currentDepth >= maxDepth) continue;

            if (!callersByInvokedMemberId.TryGetValue(currentMemberId, out var callers)) continue;

            foreach (var callerMemberId in callers)
            {
                if (currentPath.Contains(callerMemberId)) continue;

                var path = new[] { callerMemberId }.Concat(currentPath).ToList();
                if (callerMemberId != targetMemberId &&
                    (!pathsByEntrypoint.TryGetValue(callerMemberId, out var existingPath) ||
                     path.Count < existingPath.Count))
                    pathsByEntrypoint[callerMemberId] = path;

                queue.Enqueue(path);
            }
        }

        return pathsByEntrypoint;
    }

    private static SourceMemberTestability BuildSourceMemberTestability(
        int sourceMemberId,
        MemberVisibility visibility,
        TestContextCandidate? testContext,
        IReadOnlyList<TestEvidenceStatus> evidenceStatuses)
    {
        var mappingPath = testContext?.MappingPathMemberIds.Count > 0
            ? testContext.MappingPathMemberIds
            : [sourceMemberId];
        var mappings = testContext?.IsGrounded == true && testContext.ExampleTestMemberId is int testMemberId
            ?
            [
                new TestMappingPath
                {
                    TestMemberId = testMemberId,
                    SourceMemberId = sourceMemberId,
                    PathMemberIds = mappingPath,
                    IsDirect = IsDirectMethodEvidence(testContext.EvidenceKind),
                    IsObservedByCoverage = evidenceStatuses.Contains(TestEvidenceStatus.ObservedCovered),
                    IsObservedByMutation = evidenceStatuses.Contains(TestEvidenceStatus.ObservedMutationExercised)
                }
            ]
            : Array.Empty<TestMappingPath>();
        var accessPaths = testContext?.AccessPath == null
            ? Array.Empty<AccessPath>()
            : [testContext.AccessPath];

        return new SourceMemberTestability
        {
            SourceMemberId = sourceMemberId,
            Visibility = visibility,
            TestMappings = mappings,
            AccessPaths = accessPaths,
            EvidenceStatuses = evidenceStatuses,
            SetupBindings = testContext?.SupportBindings.Count > 0
                ? testContext.SupportBindings
                : BuildContextBindings(testContext)
        };
    }

    private static IReadOnlyList<ContextBinding> BuildContextBindings(TestContextCandidate? testContext)
    {
        if (testContext == null || string.IsNullOrWhiteSpace(testContext.SupportContext)) return [];

        var bindings = new List<ContextBinding>();
        var lines = testContext.SupportContext
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines.Take(6))
        {
            if (!line.Contains("builder", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("factory", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("helper", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("create", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("fixture", StringComparison.OrdinalIgnoreCase))
                continue;

            bindings.Add(new ContextBinding
            {
                NeedId = $"support:{bindings.Count + 1}",
                NeedKind = "ExistingTestSupport",
                BindingExpression = line,
                BindingKind = "HelperOrFixture",
                Reason = "Existing test support context mentions a helper, fixture, factory, builder, create, or setup member.",
                IsPreferred = true
            });
        }

        return bindings;
    }

    private static bool IsLikelyTestSupportMember(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        var name = member.Name ?? string.Empty;
        var source = member.FullString ?? string.Empty;

        return name.Contains("helper", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("factory", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("builder", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("build", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("fixture", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("create", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("helper", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("factory", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("builder", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("build", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("fixture", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ContextBinding> DiscoverContextBindings(
        IReadOnlyCollection<Persistence.Ef.Entities.Code.MemberEntity> testMembers,
        Persistence.Ef.Entities.Code.MemberEntity? selectedExample,
        string sourceObjectName)
    {
        var bindings = new List<ContextBinding>();
        foreach (var member in testMembers
                     .Where(x => !x.IsGenerated)
                     .Where(x => x.Id != selectedExample?.Id)
                     .OrderByDescending(x => IsPreferredSupportMember(x, sourceObjectName))
                     .ThenBy(x => GetSupportBindingRank(x))
                     .ThenBy(x => x.Name))
        {
            var binding = CreateContextBinding(member, bindings.Count + 1, sourceObjectName);
            if (binding != null) bindings.Add(binding);
            if (bindings.Count >= 12) break;
        }

        return bindings;
    }

    private static ContextBinding? CreateContextBinding(
        Persistence.Ef.Entities.Code.MemberEntity member,
        int index,
        string sourceObjectName)
    {
        var kind = (member.Kind ?? string.Empty).Trim();
        var name = member.Name ?? string.Empty;
        var bindingKind = ResolveSupportBindingKind(member, sourceObjectName);
        if (bindingKind == null) return null;

        return new ContextBinding
        {
            NeedId = $"support:{index}",
            NeedKind = ResolveSupportNeedKind(bindingKind),
            RequiredType = ResolveSupportRequiredType(member, bindingKind, sourceObjectName),
            BindingExpression = FormatSupportBindingExpression(member),
            BindingKind = bindingKind,
            SourceMemberId = member.Id,
            Reason = ResolveSupportReason(member, bindingKind, sourceObjectName),
            IsPreferred = IsPreferredSupportMember(member, sourceObjectName)
        };
    }

    private static string? ResolveSupportBindingKind(
        Persistence.Ef.Entities.Code.MemberEntity member,
        string sourceObjectName)
    {
        var kind = (member.Kind ?? string.Empty).Trim();
        var name = member.Name ?? string.Empty;
        if (IsSetupMember(member)) return "SetupMethod";
        if (kind.Equals("constructor", StringComparison.OrdinalIgnoreCase)) return "Constructor";
        if (kind.Equals("field", StringComparison.OrdinalIgnoreCase)) return "Field";
        if (kind.Equals("property", StringComparison.OrdinalIgnoreCase)) return "Property";
        if (!kind.Equals("method", StringComparison.OrdinalIgnoreCase)) return null;

        if (name.Contains("builder", StringComparison.OrdinalIgnoreCase)) return "Builder";
        if (name.Contains("build", StringComparison.OrdinalIgnoreCase)) return "Builder";
        if (name.Contains("factory", StringComparison.OrdinalIgnoreCase)) return "Factory";
        if (name.Contains("create", StringComparison.OrdinalIgnoreCase)) return "Factory";
        if (name.Contains("fixture", StringComparison.OrdinalIgnoreCase)) return "Fixture";
        if (name.Contains("helper", StringComparison.OrdinalIgnoreCase)) return "Helper";

        var returnType = ExtractReturnType(member);
        if (IsCompatibleSupportType(returnType, sourceObjectName)) return "CompatibleReturn";

        return null;
    }

    private static string ResolveSupportNeedKind(string bindingKind)
    {
        return bindingKind switch
        {
            "Constructor" => "FixtureConstruction",
            "SetupMethod" => "LifecycleSetup",
            "Field" or "Property" => "ReusableState",
            "Builder" or "Factory" or "Fixture" or "Helper" or "CompatibleReturn" => "ExistingTestSupport",
            _ => "ExistingTestSupport"
        };
    }

    private static string? ResolveSupportRequiredType(
        Persistence.Ef.Entities.Code.MemberEntity member,
        string bindingKind,
        string sourceObjectName)
    {
        if (bindingKind == "Constructor") return sourceObjectName;

        return member.Kind.Equals("method", StringComparison.OrdinalIgnoreCase)
            ? ExtractReturnType(member)
            : ExtractDeclaredType(member);
    }

    private static string ResolveSupportReason(
        Persistence.Ef.Entities.Code.MemberEntity member,
        string bindingKind,
        string sourceObjectName)
    {
        var type = ResolveSupportRequiredType(member, bindingKind, sourceObjectName);
        var compatible = IsCompatibleSupportType(type, sourceObjectName)
            ? " It appears compatible with the source object type."
            : string.Empty;
        return $"Discovered {bindingKind} support member '{member.Name}' from the selected test class.{compatible}";
    }

    private static string FormatSupportBindingExpression(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        var summary = (member.FullString ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(summary)
            ? $"{member.Kind} {member.Name}"
            : summary;
    }

    private static int GetSupportBindingRank(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        return ResolveSupportBindingKind(member, string.Empty) switch
        {
            "Constructor" => 0,
            "SetupMethod" => 1,
            "Field" => 2,
            "Property" => 3,
            "Builder" => 4,
            "Factory" => 5,
            "Fixture" => 6,
            "Helper" => 7,
            "CompatibleReturn" => 8,
            _ => 99
        };
    }

    private static bool IsPreferredSupportMember(
        Persistence.Ef.Entities.Code.MemberEntity member,
        string sourceObjectName)
    {
        var bindingKind = ResolveSupportBindingKind(member, sourceObjectName);
        if (bindingKind == null) return false;
        if (bindingKind is "Constructor" or "SetupMethod" or "Field" or "Property") return true;

        var requiredType = ResolveSupportRequiredType(member, bindingKind, sourceObjectName);
        return IsCompatibleSupportType(requiredType, sourceObjectName) ||
               IsLikelyTestSupportMember(member);
    }

    private static bool IsSetupMember(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        return member.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
               member.Name.Contains("initialize", StringComparison.OrdinalIgnoreCase) ||
               member.Attributes.Any(x =>
                   x.Contains("SetUp", StringComparison.OrdinalIgnoreCase) ||
                   x.Contains("TestInitialize", StringComparison.OrdinalIgnoreCase) ||
                   x.Contains("OneTimeSetUp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCompatibleSupportType(string? typeName, string sourceObjectName)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(sourceObjectName)) return false;

        var normalizedType = NormalizeTypeName(typeName);
        var normalizedSource = NormalizeTypeName(sourceObjectName);
        return string.Equals(normalizedType, normalizedSource, StringComparison.OrdinalIgnoreCase) ||
               normalizedType.EndsWith($".{normalizedSource}", StringComparison.OrdinalIgnoreCase) ||
               normalizedType.Contains($"<{normalizedSource}>", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractReturnType(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        if (!member.Kind.Equals("method", StringComparison.OrdinalIgnoreCase)) return null;

        var signature = ExtractSignaturePrefix(member);
        var nameIndex = signature.IndexOf(member.Name, StringComparison.Ordinal);
        if (nameIndex <= 0) return null;

        var prefix = signature[..nameIndex].Trim();
        var tokens = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsModifierToken(x))
            .ToList();
        return tokens.LastOrDefault(x => !x.StartsWith("[", StringComparison.Ordinal));
    }

    private static string? ExtractDeclaredType(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        var signature = ExtractSignaturePrefix(member);
        var nameIndex = signature.IndexOf(member.Name, StringComparison.Ordinal);
        if (nameIndex <= 0) return null;

        var prefix = signature[..nameIndex].Trim();
        var tokens = prefix.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsModifierToken(x))
            .ToList();
        return tokens.LastOrDefault(x => !x.StartsWith("[", StringComparison.Ordinal));
    }

    private static string ExtractSignaturePrefix(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        var source = member.FullString ?? string.Empty;
        var firstLine = source
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        var parenIndex = firstLine.IndexOf('(');
        var equalsIndex = firstLine.IndexOf('=');
        var terminatorIndex = new[] { parenIndex, equalsIndex }
            .Where(x => x >= 0)
            .DefaultIfEmpty(firstLine.Length)
            .Min();
        return firstLine[..terminatorIndex].Trim();
    }

    private static string NormalizeTypeName(string typeName)
    {
        var normalized = typeName
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Replace("[]", string.Empty, StringComparison.Ordinal)
            .Trim();
        var genericTickIndex = normalized.IndexOf('`');
        if (genericTickIndex > 0) normalized = normalized[..genericTickIndex];
        return normalized;
    }

    private static bool IsModifierToken(string token)
    {
        return token is
            "public" or "private" or "protected" or "internal" or
            "static" or "readonly" or "volatile" or "async" or
            "sealed" or "override" or "virtual" or "abstract" or
            "extern" or "unsafe" or "new" or "partial";
    }

    private static IReadOnlyList<TestEvidenceStatus> ResolveEvidenceStatuses(
        TestContextCandidate? testContext,
        Persistence.Ef.Entities.Coverage.MemberCoverageEntity? coverage,
        int undetectedMutantCount)
    {
        var statuses = new List<TestEvidenceStatus>();
        if (testContext?.IsGrounded == true)
        {
            statuses.Add(IsDirectMethodEvidence(testContext.EvidenceKind)
                ? TestEvidenceStatus.DirectlyMappedToTest
                : TestEvidenceStatus.IndirectlyMappedToTest);
        }

        if (coverage == null)
        {
            statuses.Add(TestEvidenceStatus.EvidenceUnavailable);
        }
        else if (coverage.LinesCovered > 0 || coverage.LineRate > 0.0)
        {
            statuses.Add(TestEvidenceStatus.ObservedCovered);
        }
        else
        {
            statuses.Add(TestEvidenceStatus.ObservedUntested);
        }

        if (undetectedMutantCount > 0) statuses.Add(TestEvidenceStatus.ObservedMutationExercised);

        return statuses.Distinct().ToList();
    }

    private static string BuildAccessPathSummary(
        SourceMemberTestability testability,
        TestContextCandidate? testContext)
    {
        var evidence = string.Join(", ", testability.EvidenceStatuses);
        if (testability.AccessPaths.Count == 0)
            return $"Context graph access: no legal access path resolved. Visibility={testability.Visibility}; evidence={evidence}.";

        var accessPath = testability.AccessPaths[0];
        var path = string.Join(" -> ", accessPath.PathMemberIds);
        var testMapping = testContext?.ExampleTestMethodName == null
            ? "No mapped example test."
            : $"Mapped example test: {testContext.ExampleTestMethodName}.";
        return
            $"Context graph access: strategy={accessPath.Strategy}; visibility={testability.Visibility}; " +
            $"entrypointMemberId={accessPath.EntrypointMemberId}; targetMemberId={accessPath.TargetMemberId}; " +
            $"path={path}; legalFromTest={accessPath.IsLegalFromTest}; requiresReflection={accessPath.RequiresReflection}; " +
            $"evidence={evidence}. {testMapping}";
    }

    private static MemberVisibility ResolveMemberVisibility(Persistence.Ef.Entities.Code.MemberEntity member)
    {
        if (member.Modifiers.Any(x => x.Equals("public", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("public ", StringComparison.Ordinal))
            return MemberVisibility.Public;
        if (member.Modifiers.Any(x => x.Equals("private", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("private ", StringComparison.Ordinal))
            return MemberVisibility.Private;
        if (member.Modifiers.Any(x => x.Equals("protected", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("protected ", StringComparison.Ordinal))
            return MemberVisibility.Protected;
        if (member.Modifiers.Any(x => x.Equals("internal", StringComparison.OrdinalIgnoreCase)) ||
            member.FullString.Contains("internal ", StringComparison.Ordinal))
            return MemberVisibility.Internal;
        if (member.FullString.Contains('.', StringComparison.Ordinal) &&
            member.FullString.Contains("=>", StringComparison.Ordinal))
            return MemberVisibility.ExplicitInterface;

        return MemberVisibility.Unknown;
    }

    private static TestAccessStrategy ResolveDirectAccessStrategy(MemberVisibility visibility)
    {
        return visibility switch
        {
            MemberVisibility.Public => TestAccessStrategy.DirectPublicCall,
            MemberVisibility.Internal => TestAccessStrategy.DirectInternalCall,
            MemberVisibility.Protected => TestAccessStrategy.ProtectedSubclassHarness,
            _ => TestAccessStrategy.NotReasonablyTestable
        };
    }

    private static TestAccessStrategy ResolveCallerPathStrategy(MemberVisibility visibility)
    {
        return visibility switch
        {
            MemberVisibility.Public => TestAccessStrategy.PublicCallerPath,
            MemberVisibility.Internal => TestAccessStrategy.InternalCallerPath,
            MemberVisibility.Protected => TestAccessStrategy.ProtectedSubclassHarness,
            _ => TestAccessStrategy.Unknown
        };
    }

    private sealed record InvocationEdge(int CallerMemberId, int InvokedMemberId);

    private sealed record RoslynReferenceCandidate(string FilePath, string MemberName);

    private sealed record TestContextEvidenceRow(
        Persistence.Ef.Entities.Code.MemberEntity TestMember,
        Persistence.Ef.Entities.Code.ObjectEntity TestObject,
        Persistence.Ef.Entities.Code.FileEntity TestFile,
        Persistence.Ef.Entities.Code.CSharpProjectEntity TestProject,
        string EvidenceKind,
        string EvidenceSummary,
        bool IsGrounded,
        AccessPath? AccessPath,
        IReadOnlyList<int> PathMemberIds)
    {
        public int? SourceTestMappingId { get; init; }
    }

    private sealed record CandidateTestAssessment(
        CandidateTestState TestState,
        CandidateActionKind RecommendedAction,
        string Reason);

    private sealed record LatestBaselineTestOutcomes(
        string RunId,
        IReadOnlyDictionary<int, string> OutcomesByTestMemberId);

    private sealed record EnrichedCandidateSelection(
        CandidateMethod Candidate,
        CandidateInventoryItem InventoryItem,
        string ContextEvidenceKind,
        bool HasGroundedTestContext,
        int StrategyRank);
}
