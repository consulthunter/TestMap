using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Configuration;
using TestMap.Models.Coverage;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Mapping.Code;
using TestMap.Persistence.Ef.Repositories.Experiment;

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

    public MethodSelectionService(
        ProjectContext context,
        TestMapDbContext dbContext,
        CandidateMethodSelector candidateMethodSelector,
        CandidateInventoryRepository candidateInventoryRepository)
    {
        _context = context;
        _dbContext = dbContext;
        _candidateMethodSelector = candidateMethodSelector;
        _candidateInventoryRepository = candidateInventoryRepository;
    }

    public async Task<List<CandidateMethod>> SelectCandidateMethodsAsync(
        ExperimentConfig config,
        bool requirePassingExistingTest = false,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _candidateMethodSelector.SelectAsync(config, cancellationToken);
        var latestBaseline = await LoadLatestBaselineTestOutcomesAsync(cancellationToken);
        var inventory = new List<CandidateInventoryItem>();
        var selected = new List<CandidateMethod>();

        foreach (var candidate in candidates)
        {
            var context = await GetMethodContextAsync(candidate.MemberId, cancellationToken);
            if (context == null) continue;

            candidate.ExistingTestMemberId = context.Method.ExistingTestMemberId;
            candidate.ExistingTestMethodName = context.Method.ExistingTestMethodName;
            candidate.TestState = context.Method.TestState;
            candidate.RecommendedAction = context.Method.RecommendedAction;
            candidate.TestStateReason = context.Method.TestStateReason;

            var inventoryItem = CreateInventoryItem(
                config,
                candidate,
                latestBaseline);
            inventory.Add(inventoryItem);

            if (!requirePassingExistingTest || inventoryItem.IsExperimentEligible)
                selected.Add(candidate);
        }

        await _candidateInventoryRepository.ReplaceForProjectStrategyAsync(
            _context.Project.DbId,
            config.CandidateSelectionStrategy ?? _context.Project.Config.TestingConfig.GenerationConfig.TargetSelection.Strategy,
            inventory,
            cancellationToken);

        return selected;
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
            BaselineRunId = latestBaseline.RunId
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

    public async Task<CandidateMethodContext?> GetMethodContextAsync(
        int memberId,
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

        var testContext = await FindBestTestContextAsync(
            sourceObject,
            sourceFile,
            project,
            cancellationToken);
        var directTestSignals = await GetDirectTestSignalCountAsync(memberId, cancellationToken);
        var undetectedMutants = await GetUndetectedMutantsAsync(memberId, cancellationToken);
        var exampleTestSmellCount = testContext?.ExampleTestMemberId is int exampleTestMemberId
            ? await GetTestSmellCountAsync(exampleTestMemberId, cancellationToken)
            : 0;
        var testAssessment = DetermineTestAssessment(
            testContext,
            directTestSignals,
            coverageEntity?.LineRate ?? 0.0,
            coverageGaps.Count,
            undetectedMutants.Count,
            exampleTestSmellCount);

        var candidateMethod = new CandidateMethod
        {
            MemberId = member.Id,
            MethodName = member.Name,
            SourceCode = member.FullString,
            Signature = methodSignature,
            ExistingTestMemberId = testContext?.ExampleTestMemberId,
            ExistingTestMethodName = testContext?.ExampleTestMethodName,
            BaselineCoverage = coverageEntity?.LineRate ?? 0.0,
            ComplexityScore = coverageEntity?.Complexity ?? 0.0,
            TestState = testAssessment.TestState,
            RecommendedAction = testAssessment.RecommendedAction,
            TestStateReason = testAssessment.Reason
        };

        return new CandidateMethodContext
        {
            Method = candidateMethod,
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
            MutationSummary = BuildMutationSummary(undetectedMutants)
        };
    }

    private static string ResolveTestClassName(TestContextCandidate? testContext, string sourceObjectName)
    {
        if (!string.IsNullOrWhiteSpace(testContext?.TestClass.Name)) return testContext.TestClass.Name;

        return $"{sourceObjectName}Tests";
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
                var location = mutant.Location.StartLineNumber > 0
                    ? $"lines {mutant.Location.StartLineNumber}-{mutant.Location.EndLineNumber}"
                    : "unknown location";
                var replacement = string.IsNullOrWhiteSpace(mutant.Replacement)
                    ? string.Empty
                    : $", replacement=`{mutant.Replacement}`";
                var reason = string.IsNullOrWhiteSpace(mutant.StatusReason)
                    ? string.Empty
                    : $", reason={mutant.StatusReason}";
                var coveredBy = mutant.CoveredBy.Count == 0
                    ? string.Empty
                    : $", coveredBy={string.Join(", ", mutant.CoveredBy.Take(3))}";

                return
                    $"- Mutant {id}: {mutant.Status}, {mutant.MutatorName}, {location}{replacement}{reason}{coveredBy}";
            })
            .Distinct()
            .Take(12);

        return "Mutation evidence to target:\n" + string.Join("\n", mutantLines);
    }

    private async Task<int> GetDirectTestSignalCountAsync(int memberId, CancellationToken cancellationToken)
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

        return relationshipSignals + invocationSignals;
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
        int directTestSignals,
        double baselineCoverage,
        int coverageGapCount,
        int undetectedMutantCount,
        int exampleTestSmellCount)
    {
        if (testContext?.ExampleTestMemberId == null)
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

        if (directTestSignals <= 0) weakQualitySignals.Add("no direct test signals were found");

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
        ObjectModel sourceObject,
        FileModel sourceFile,
        CSharpProjectModel sourceProject,
        CancellationToken cancellationToken)
    {
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
            SupportContext = supportContext
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
    }

    private sealed record CandidateTestAssessment(
        CandidateTestState TestState,
        CandidateActionKind RecommendedAction,
        string Reason);

    private sealed record LatestBaselineTestOutcomes(
        string RunId,
        IReadOnlyDictionary<int, string> OutcomesByTestMemberId);
}
