using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Models.Experiment;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Mapping.Code;

namespace TestMap.Services.Experiment;

/// <summary>
/// Service for selecting candidate methods for test generation experiments.
/// Queries the database for methods with coverage within specified thresholds.
/// </summary>
public class MethodSelectionService : IMethodSelectionService
{
    private readonly ProjectContext _context;
    private readonly TestMapDbContext _dbContext;

    public MethodSelectionService(
        ProjectContext context,
        TestMapDbContext dbContext)
    {
        _context = context;
        _dbContext = dbContext;
    }

    public async Task<List<CandidateMethod>> SelectCandidateMethodsAsync(
        ExperimentConfiguration config,
        CancellationToken cancellationToken = default)
    {
        _context.Project.Logger?.Information(
            "Selecting candidate methods with coverage between {MinCoverage:P} and {MaxCoverage:P}",
            config.MinCoverageThreshold,
            config.MaxCoverageThreshold);

        var selectionTime = DateTime.UtcNow;

        var candidateRows = await (
            from member in _dbContext.Members.AsNoTracking()
            let selectedCoverage = (
                from coverage in _dbContext.MemberCoverages
                join report in _dbContext.CoverageReports on coverage.CoverageReportId equals report.Id
                where coverage.MemberId == member.Id
                      && coverage.LineRate >= config.MinCoverageThreshold
                      && coverage.LineRate <= config.MaxCoverageThreshold
                orderby report.Timestamp descending, report.CreatedAt descending, coverage.Id descending
                select new
                {
                    coverage.LineRate,
                    coverage.Complexity
                })
                .FirstOrDefault()
            where !member.IsTestMember
                  && !member.IsGenerated
                  && member.Kind == "method"
                  && selectedCoverage != null
            orderby selectedCoverage.LineRate ascending, selectedCoverage.Complexity descending
            select new
            {
                member.Id,
                member.Name,
                member.FullString,
                selectedCoverage.LineRate,
                selectedCoverage.Complexity
            })
            .Take(config.CandidateLimit)
            .ToListAsync(cancellationToken);

        var candidateMethods = candidateRows
            .Select(x => new CandidateMethod
            {
                MemberId = x.Id,
                MethodName = x.Name,
                SourceCode = x.FullString,
                Signature = ExtractMethodSignature(x.FullString, x.Name),
                BaselineCoverage = x.LineRate,
                ComplexityScore = x.Complexity,
                SelectionTime = selectionTime
            })
            .ToList();

        _context.Project.Logger?.Information("Found {Count} candidate methods", candidateMethods.Count);
        return candidateMethods;
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

        var candidateMethod = new CandidateMethod
        {
            MemberId = member.Id,
            MethodName = member.Name,
            SourceCode = member.FullString,
            Signature = methodSignature,
            ExistingTestMemberId = testContext?.ExampleTestMemberId,
            ExistingTestMethodName = testContext?.ExampleTestMethodName,
            BaselineCoverage = coverageEntity?.LineRate ?? 0.0,
            ComplexityScore = coverageEntity?.Complexity ?? 0.0
        };

        return new CandidateMethodContext
        {
            Method = candidateMethod,
            MethodSignature = methodSignature,
            ContainingClass = sourceObject.FullString,
            TestClassName = testContext?.TestClass.Name ?? $"{sourceObject.Name}Tests",
            TestFilePath = testContext?.TestFile.FilePath ?? FindFallbackTestFilePath(sourceFile.FilePath, sourceObject.Name),
            SourceProjectPath = project.FilePath,
            TestProjectPath = testContext?.Project.FilePath ?? project.FilePath,
            TargetBuildFramework = ResolveTargetBuildFramework(testContext?.Project, project),
            SolutionFilePath = solution.FilePath,
            ExampleTest = testContext?.ExampleTest ?? CreateFallbackExampleTest(member.Name, DetermineTestFramework(testContext?.TestClass, testContext?.Project)),
            ExampleTestMetadataSummary = testContext?.ExampleTestMetadataSummary ?? "No enriched example test metadata is available.",
            ProjectTestMetadataSummary = testContext?.ProjectTestMetadataSummary ?? "No enriched project test metadata is available.",
            TestClass = testContext?.TestClass.FullString ?? CreateFallbackTestClass(sourceObject.Name, DetermineTestFramework(testContext?.TestClass, testContext?.Project)),
            TestFileContents = testContext?.TestFileContents ?? CreateFallbackTestClass(sourceObject.Name, DetermineTestFramework(testContext?.TestClass, testContext?.Project)),
            TestSupportContext = testContext?.SupportContext ?? "No additional helper methods, setup hooks, or support members were discovered in the selected test file.",
            TestFramework = DetermineTestFramework(testContext?.TestClass, testContext?.Project),
            TestDependencies = testContext?.Dependencies ?? GetDefaultDependencies(DetermineTestFramework(testContext?.TestClass, testContext?.Project)),
            CoverageGapSummary = BuildCoverageGapSummary(coverageGaps)
        };
    }

    private static string BuildCoverageGapSummary(
        IReadOnlyCollection<Persistence.Ef.Entities.Coverage.CoverageGapEntity> coverageGaps)
    {
        if (coverageGaps.Count == 0)
        {
            return "No line-level coverage gap data is available for this method.";
        }

        var gapLines = coverageGaps
            .Select(gap =>
            {
                var kind = string.Equals(gap.GapKind, CoverageGapKind.PartialBranch.ToString(), StringComparison.OrdinalIgnoreCase)
                    ? "partial branch"
                    : "uncovered line";
                var sourceText = string.IsNullOrWhiteSpace(gap.SourceText)
                    ? string.Empty
                    : $" - `{gap.SourceText}`";

                return $"- Line {gap.LineNumber}: {kind}, hits={gap.Hits}{(string.IsNullOrWhiteSpace(gap.ConditionCoverage) ? string.Empty : $", condition={gap.ConditionCoverage}")}{sourceText}";
            })
            .Distinct()
            .Take(12);

        return "Coverage gaps to target:\n" + string.Join("\n", gapLines);
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

        if (candidates.Count == 0)
        {
            return null;
        }

        var bestCandidate = candidates
            .Select(x => new TestContextCandidate(
                x.TestObject.ToDomain(),
                x.TestFile.ToDomain(),
                x.TestProject.ToDomain(),
                ScoreTestContext(sourceObject, sourceFile, x.TestObject, x.TestFile, x.TestProject)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TestFile.FilePath.Length)
            .FirstOrDefault();

        if (bestCandidate == null)
        {
            return null;
        }

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
            ExampleTestMetadataSummary = BuildExampleTestMetadataSummary(selectedExample),
            ProjectTestMetadataSummary = BuildProjectTestMetadataSummary(exampleCandidates),
            Dependencies = dependencies,
            TestFileContents = testFileContents,
            SupportContext = supportContext
        };
    }

    private static async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    private static int ScoreExampleTest(ObjectModel sourceObject, Persistence.Ef.Entities.Code.MemberEntity testMember)
    {
        var score = 0;

        if (!testMember.IsGenerated)
        {
            score += 40;
        }

        if (!string.IsNullOrWhiteSpace(testMember.TestIntent))
        {
            score += 25;
        }

        if (testMember.TestCategories.Count > 0)
        {
            score += 10 + (testMember.TestCategories.Count * 2);
        }

        if (!string.IsNullOrWhiteSpace(testMember.TestMetadataSource))
        {
            score += 8;
        }

        if (testMember.TestMetadataConfidence.HasValue)
        {
            score += (int)(testMember.TestMetadataConfidence.Value * 20);
        }

        if (testMember.Name.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (!string.IsNullOrWhiteSpace(testMember.TestIntent) &&
            testMember.TestIntent.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        return score;
    }

    private static string BuildExampleTestMetadataSummary(Persistence.Ef.Entities.Code.MemberEntity? exampleTest)
    {
        if (exampleTest == null)
        {
            return "No enriched example test metadata is available.";
        }

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

        return $"Example categories: {categories}\nExample intent: {intent}\nMetadata source: {source}\nMetadata confidence: {confidence}";
    }

    private static string BuildProjectTestMetadataSummary(IReadOnlyCollection<Persistence.Ef.Entities.Code.MemberEntity> testMembers)
    {
        if (testMembers.Count == 0)
        {
            return "No enriched project test metadata is available.";
        }

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
        {
            score += 100;
        }
        else if (testObject.Name.Contains(sourceObject.Name, StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        var sourceNamespace = sourceObject.Namespace ?? string.Empty;
        var testNamespace = testObject.Namespace ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sourceNamespace) && !string.IsNullOrWhiteSpace(testNamespace))
        {
            if (string.Equals(testNamespace, $"{sourceNamespace}.Tests", StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
            }
            else if (testNamespace.Contains(sourceNamespace, StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }
        }

        var sourceBaseName = Path.GetFileNameWithoutExtension(sourceFile.FilePath);
        var testBaseName = Path.GetFileNameWithoutExtension(testFile.FilePath);
        if (string.Equals(testBaseName, $"{sourceBaseName}Tests", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }
        else if (testBaseName.Contains(sourceBaseName, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (testProject.BuildMetadata.IsTestProject)
        {
            score += 15;
        }

        return score;
    }

    private static string ExtractMethodSignature(string sourceCode, string methodName)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return $"public void {methodName}()";
        }

        var lines = sourceCode.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var signature = lines.FirstOrDefault(line =>
            line.Contains(methodName, StringComparison.Ordinal) &&
            line.Contains('('));

        return signature ?? $"public void {methodName}()";
    }

    private static string FindFallbackTestFilePath(string sourceFilePath, string sourceObjectName)
    {
        var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var testFileName = string.IsNullOrWhiteSpace(fileName) ? $"{sourceObjectName}Tests.cs" : $"{fileName}Tests.cs";
        return Path.Combine(directory, "..", "Tests", testFileName);
    }

    private static string ResolveTargetBuildFramework(CSharpProjectModel? testProject, CSharpProjectModel sourceProject)
    {
        var selectedProject = testProject ?? sourceProject;
        return selectedProject.BuildMetadata.DefaultBuildTarget;
    }

    private static string DetermineTestFramework(ObjectModel? testClass, CSharpProjectModel? project)
    {
        if (!string.IsNullOrWhiteSpace(testClass?.TestFramework))
        {
            return testClass.TestFramework;
        }

        var notes = project?.BuildMetadata.Notes ?? string.Empty;

        if (notes.Contains("xunit", StringComparison.OrdinalIgnoreCase))
        {
            return "xUnit";
        }

        if (notes.Contains("mstest", StringComparison.OrdinalIgnoreCase))
        {
            return "MSTest";
        }

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

        if (!dependencies.Contains(frameworkUsing, StringComparer.Ordinal))
        {
            dependencies.Insert(0, frameworkUsing);
        }

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

    private static string CreateFallbackTestClass(string sourceObjectName, string framework)
    {
        var classAttribute = framework switch
        {
            "MSTest" => "[TestClass]",
            "xUnit" => string.Empty,
            _ => "[TestFixture]"
        };

        return $@"
{GetDefaultDependencies(framework)}

{classAttribute}
public class {sourceObjectName}Tests
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
        public string? ExampleTestMetadataSummary { get; init; }
        public string? ProjectTestMetadataSummary { get; init; }
        public string? Dependencies { get; init; }
        public string? TestFileContents { get; init; }
        public string? SupportContext { get; init; }
    }
}
