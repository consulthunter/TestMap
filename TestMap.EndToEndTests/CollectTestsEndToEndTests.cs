using Microsoft.Build.Locator;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestMap.App;
using TestMap.Execution;
using TestMap.Execution.Steps;
using TestMap.Models.Configuration;
using TestMap.Models.Testing;
using TestMap.Persistence.Ef;
using TestMap.Persistence.Ef.Entities.Testing;
using TestMap.Services;
using TestMap.Services.Configuration;

namespace TestMap.EndToEndTests;

public sealed class CollectTestsEndToEndTests : IDisposable
{
    private readonly List<string> _directoriesToDelete = [];
    private readonly List<IDisposable> _disposables = [];

    /// <summary>
    /// Verifies that the collect-tests workflow analyzes the external example project and writes persisted collection output.
    /// </summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    [Trait("Execution", "LocalOnly")]
    public async Task CollectTestsPipeline_WithTestMapExample_PersistsAnalysisAndValidationOutput()
    {
        // Arrange
        RegisterMSBuildDefaults();

        var repositoryRoot = FindRepositoryRoot();
        var fixturePath = Path.Combine(repositoryRoot, "Temp", "TestMap-Example");
        Assert.True(
            Directory.Exists(fixturePath),
            $"The TestMap-Example fixture repository was not found at {fixturePath}.");

        var rootPath = CreateTemporaryDirectory();
        var logsPath = Path.Combine(rootPath, "Logs");
        var tempPath = Path.Combine(rootPath, "Temp");
        var outputPath = Path.Combine(rootPath, "Output");
        var dataPath = Path.Combine(rootPath, "Data");
        Directory.CreateDirectory(logsPath);
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(dataPath);

        var targetRepoPath = Path.Combine(tempPath, "TestMap-Example");
        CopyDirectory(fixturePath, targetRepoPath);

        var targetFilePath = Path.Combine(dataPath, "targets.txt");
        await File.WriteAllTextAsync(
            targetFilePath,
            "https://github.com/testmap-fixtures/TestMap-Example.git");

        var config = CreateConfig(targetFilePath, logsPath, tempPath, outputPath);
        var configurationService = new ConfigurationService(config)
        {
            RunMode = RunMode.CollectTests
        };
        await configurationService.ConfigureRunAsync();

        var project = Assert.Single(configurationService.ProjectModels);
        project.EnsureProjectLogDir();
        project.EnsureProjectOutputDir();
        if (project.Logger is IDisposable loggerDisposable) _disposables.Add(loggerDisposable);
        var context = new ProjectContext(project);

        await using var provider = BuildServiceProvider(configurationService, config, context);
        await InitializeDatabaseAsync(provider);

        var pipeline = new RunPipeline(
        [
            provider.GetRequiredService<CloneRepoStep>(),
            provider.GetRequiredService<LoadDatabaseStep>(),
            provider.GetRequiredService<ExtractInfoStep>(),
            provider.GetRequiredService<InsertProjectInfoStep>(),
            provider.GetRequiredService<AnalyzeProjectStep>(),
            provider.GetRequiredService<CollectCodeMetricsStep>(),
            provider.GetRequiredService<EnrichTestMetadataStep>(),
            provider.GetRequiredService<CollectTestSmellsStep>(),
            new SeedPassingTestRunStep(provider.GetRequiredService<TestMapDbContext>()),
            provider.GetRequiredService<WriteCollectTestsResultStep>()
        ]);
        var executor = new ProjectPipelineExecutor(context, pipeline);

        // Act
        await executor.RunAsync();

        // Assert
        var db = provider.GetRequiredService<TestMapDbContext>();
        Assert.True(File.Exists(project.DatabasePath));
        Assert.NotEmpty(await db.CSharpSolutions.ToListAsync());
        Assert.NotEmpty(await db.CSharpProjects.ToListAsync());
        Assert.NotEmpty(await db.Objects.ToListAsync());
        Assert.NotEmpty(await db.Members.Where(x => x.IsTestMember).ToListAsync());
        Assert.NotEmpty(await db.TestRuns.Where(x => x.ProjectId == project.DbId && x.Success).ToListAsync());
        Assert.NotEmpty(await db.RuleDefinitions.ToListAsync());
        Assert.NotEmpty(await db.RuleDecisions.Where(x => x.ProjectId == project.DbId).ToListAsync());
        Assert.Contains(
            await db.RuleDecisions.Where(x => x.ProjectId == project.DbId).ToListAsync(),
            x => x.RuleId.StartsWith("project-discovery.build-targets.", StringComparison.Ordinal));

        var validationCsvPath = Path.Combine(outputPath, "project-validation.csv");
        Assert.True(File.Exists(validationCsvPath));
        var validationCsv = await File.ReadAllTextAsync(validationCsvPath);
        Assert.Contains("https://github.com/testmap-fixtures/TestMap-Example.git", validationCsv);
        var validationRow = ReadSingleCsvRecord(validationCsv);
        Assert.Equal("testmap-fixtures", validationRow["Owner"]);
        Assert.Equal("TestMap-Example", validationRow["Repo"]);
        Assert.Equal("False", validationRow["Restores"]);
        Assert.Equal("False", validationRow["Builds"]);
        Assert.Equal("False", validationRow["TestsRun"]);
        Assert.Equal("True", validationRow["TestsPass"]);
        Assert.Equal("e2e-seeded-baseline", validationRow["BaselineRunId"]);
    }

    public void Dispose()
    {
        foreach (var disposable in Enumerable.Reverse(_disposables))
        {
            disposable.Dispose();
        }

        SqliteConnection.ClearAllPools();

        foreach (var directory in Enumerable.Reverse(_directoriesToDelete))
        {
            if (Directory.Exists(directory))
            {
                ResetFileAttributes(directory);
                Directory.Delete(directory, true);
            }
        }
    }

    private static void RegisterMSBuildDefaults()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }
    }

    private static ServiceProvider BuildServiceProvider(
        IConfigurationService configurationService,
        TestMapConfig config,
        ProjectContext context)
    {
        var services = new ServiceCollection();
        services.AddTestMapServices(configurationService, config, context);
        return services.BuildServiceProvider();
    }

    private static async Task InitializeDatabaseAsync(ServiceProvider provider)
    {
        var db = provider.GetRequiredService<TestMapDbContext>();
        var initializer = provider.GetRequiredService<TestMapDatabaseInitializer>();
        await initializer.InitializeAsync(db);
    }

    private static TestMapConfig CreateConfig(
        string targetFilePath,
        string logsPath,
        string tempPath,
        string outputPath)
    {
        return new TestMapConfig
        {
            RuntimeConfig =
            {
                MaxConcurrency = 1,
                FilePaths =
                {
                    TargetFilePath = targetFilePath,
                    LogsDirPath = logsPath,
                    TempDirPath = tempPath,
                    OutputDirPath = outputPath
                },
                Docker =
                {
                    DefaultContext = "desktop-linux",
                    Images =
                    {
                        ValidationSdkAll = "testmap-runner"
                    }
                },
                Frameworks = new Dictionary<string, List<string>>
                {
                    ["xUnit"] = ["Fact", "Theory"]
                },
                Project =
                {
                    KeepProjectFiles = true
                }
            },
            TestingConfig =
            {
                MetadataEnrichmentConfig =
                {
                    Enabled = true,
                    UseModel = false
                }
            }
        };
    }

    private string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TestMap.EndToEndTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _directoriesToDelete.Add(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TestMap.slnx"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from the test output directory.");
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static void ResetFileAttributes(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadSingleCsvRecord(string csv)
    {
        var lines = csv
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        Assert.True(lines.Count >= 2, "Expected project-validation.csv to contain a header and at least one row.");

        var headers = SplitCsvLine(lines[0]);
        var values = SplitCsvLine(lines[1]);
        Assert.Equal(headers.Count, values.Count);

        return headers
            .Select((header, index) => new { header, value = values[index] })
            .ToDictionary(x => x.header, x => x.value, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var currentChar = line[i];
            if (currentChar == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (currentChar == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        values.Add(current.ToString());
        return values;
    }

    private sealed class SeedPassingTestRunStep(TestMapDbContext dbContext) : IPipelineStep
    {
        public async Task ExecuteAsync(ProjectContext? context = null)
        {
            if (context == null) return;

            dbContext.TestRuns.Add(new TestRunEntity
            {
                ProjectId = context.Project.DbId,
                RunId = "e2e-seeded-baseline",
                RunDate = DateTime.UtcNow.ToString("O"),
                Success = true,
                Coverage = 85,
                MutationScore = null,
                LogPath = string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }
    }
}
