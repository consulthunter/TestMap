using System.Text.Json;
using TestMap.Models;
using TestMap.Models.Results;
using TestMap.Services.Database;
using XNoseNext.Core;
using XNoseNext.Core.Reporters;

namespace TestMap.Services.xNose;

public class xNoseService
{
    private readonly ProjectModel _projectModel;
    private readonly SqliteDatabaseService _databaseService;
    public xNoseService(ProjectModel projectModel, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = projectModel;
        _databaseService = sqliteDatabaseService;
    }

    public async Task Analyze(string solutionPath)
    {
        await XNoseAnalyze(solutionPath);
    }
    
    private async Task XNoseAnalyze(string solutionPath)
    {
        try
        {
            var analyzer = new XNoseNextAnalyzer();
            var service = new SmellFindingsService(analyzer);
            
            var solutionParentDir = Path.GetDirectoryName(solutionPath) ?? "";
            var filePath = Path.Combine(solutionParentDir, "xnose-next-report.json");

            var reporter = new JsonFileSmellFindingsReporter(filePath);
            await service.CollectAndReportAsync(solutionPath, reporter);
            
            await SaveTestSmellsToDb(solutionPath);
            
        } catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async Task SaveTestSmellsToDb(string solutionPath)
    {
        try
        {
            var fileName =
                $"{Path.GetFileName(solutionPath).Replace(".sln", "").ToLowerInvariant()}_test_smell_reports.json";
            var dirName = Path.Join(Path.GetDirectoryName(solutionPath), fileName);

            string json = File.ReadAllText(dirName);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            List<TestSmellResult> results =
                JsonSerializer.Deserialize<List<TestSmellResult>>(json, options);

            // 3. Loop and insert
            foreach (var testClass in results ?? new())
            {
                var classId = await _databaseService.ClassRepository.FindClass(testClass.Name);
                if (classId != 0)
                {
                    foreach (var method in testClass.Methods ?? new())
                    {
                        var methodId = await _databaseService.MethodRepository.FindMethod(method.Name, classId);
                        if (methodId != 0)
                        {
                            foreach (var smell in method.Smells ?? new())
                            {
                                var smellId = await _databaseService.TestSmellRepository.FindTestSmell(smell.Name);
                                if (smellId != 0)
                                {
                                    await _databaseService.MethodTestSmellRepository.InsertMethodTestSmellGetId(
                                        methodId, smellId, smell.Status);
                                }
                                else
                                {
                                    _projectModel.Logger?.Information($"Test Smell {smell.Name} not found in DB");
                                }
                            }
                        }
                        else
                        {
                            _projectModel.Logger?.Information($"Method {method.Name} not found in DB");
                        }
                    }
                }
                else
                {
                    _projectModel.Logger?.Information($"Class {testClass.Name} not found in DB");
                }
            }

            // 4. Delete the file
            File.Delete(dirName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

    }
}