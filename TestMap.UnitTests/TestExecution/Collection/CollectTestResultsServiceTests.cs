using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models;
using TestMap.Models.Code;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Code;
using TestMap.Services.TestExecution.Collection;

namespace TestMap.UnitTests.TestExecution.Collection;

public sealed class CollectTestResultsServiceTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];
    private readonly List<SqliteConnection> _connectionsToDispose = [];

    /// <summary>
    /// Verifies that TRX files are parsed into test result models with outcomes, durations, and error messages.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithTrxFile_ReturnsParsedTestResultsAndRawXml()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var trxPath = Path.Combine(coverageDirectory, "results.trx");
        await File.WriteAllTextAsync(
            trxPath,
            """
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="PassingTest" outcome="Passed" duration="00:00:01.234" />
                <UnitTestResult testName="FailingTest" outcome="Failed" duration="00:00:02">
                  <Output>
                    <ErrorInfo>
                      <Message>Expected true but was false.</Message>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
              </Results>
            </TestRun>
            """);
        await using var db = CreateDbContext();
        var service = new CollectTestResultsService(CreateContext(projectDirectory), db);

        // Act
        var (results, raw) = await service.CollectAsync("run-1", "2026-04-28");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.Equal("run-1", result.RunId);
            Assert.Equal("2026-04-28", result.RunDate);
        });
        Assert.Contains(results, result =>
            result.TestName == "PassingTest" &&
            result.Outcome == "Passed" &&
            result.Duration == TimeSpan.FromMilliseconds(1234));
        Assert.Contains(results, result =>
            result.TestName == "FailingTest" &&
            result.Outcome == "Failed" &&
            result.ErrorMessage == "Expected true but was false.");
        Assert.Contains("UnitTestResult", raw);
    }

    /// <summary>
    /// Verifies that a missing coverage directory returns empty test results.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithMissingCoverageDirectory_ReturnsEmptyResults()
    {
        // Arrange
        var projectDirectory = CreateTemporaryDirectory();
        await using var db = CreateDbContext();
        var service = new CollectTestResultsService(CreateContext(projectDirectory), db);

        // Act
        var (results, raw) = await service.CollectAsync("run-1", "2026-04-28");

        // Assert
        Assert.Empty(results);
        Assert.Equal(string.Empty, raw);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CollectAsync_WithTrxDefinitions_MapsResultsToPersistedTestMembers()
    {
        var projectDirectory = CreateTemporaryDirectory();
        var coverageDirectory = Directory.CreateDirectory(Path.Combine(projectDirectory, "coverage")).FullName;
        var trxPath = Path.Combine(coverageDirectory, "results.trx");
        var testId = Guid.NewGuid().ToString();
        var executionId = Guid.NewGuid().ToString();
        await File.WriteAllTextAsync(
            trxPath,
            $"""
             <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
               <Results>
                 <UnitTestResult executionId="{executionId}" testId="{testId}" testName="Demo.Tests.CalculatorTests.Add_Works(value: 1)" outcome="Passed" duration="00:00:00.100" />
               </Results>
               <TestDefinitions>
                 <UnitTest name="Demo.Tests.CalculatorTests.Add_Works(value: 1)" id="{testId}">
                   <Execution id="{executionId}" />
                   <TestMethod className="Demo.Tests.CalculatorTests" name="Add_Works" />
                 </UnitTest>
               </TestDefinitions>
             </TestRun>
             """);
        await using var db = CreateDbContext();
        var memberId = await SeedTestMemberAsync(db, "Demo.Tests", "CalculatorTests", "Add_Works");
        var service = new CollectTestResultsService(CreateContext(projectDirectory), db);

        var (results, _) = await service.CollectAsync("run-1", "2026-04-28");

        var result = Assert.Single(results);
        Assert.Equal(memberId, result.MethodId);
    }

    public void Dispose()
    {
        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        foreach (var connection in _connectionsToDispose)
            connection.Dispose();
    }

    private ProjectContext CreateContext(string projectDirectory)
    {
        return new ProjectContext(new ProjectModel(directoryPath: projectDirectory));
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private TestMapDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connectionsToDispose.Add(connection);

        var options = new DbContextOptionsBuilder<TestMapDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new TestMapDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<int> SeedTestMemberAsync(
        TestMapDbContext db,
        string ns,
        string className,
        string methodName)
    {
        var file = new FileEntity
        {
            FilePath = Path.Combine("Demo.Tests", $"{className}.cs")
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        var obj = new ObjectEntity
        {
            FileId = file.Id,
            Namespace = ns,
            Name = className,
            Kind = "class",
            Location = new Location(1, 1, 10, 10),
            IsTestObject = true
        };
        db.Objects.Add(obj);
        await db.SaveChangesAsync();

        var member = new MemberEntity
        {
            ObjectEntityId = obj.Id,
            Name = methodName,
            Kind = "method",
            FullString = $"public void {methodName}() {{ }}",
            Location = new Location(3, 3, 5, 5),
            IsTestMember = true
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();

        return member.Id;
    }
}
