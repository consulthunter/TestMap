using System.Text.Json;
using F23.StringSimilarity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TestMap.Models;
using TestMap.Models.Results;
using TestMap.Services.Database;
using TestMap.Services.xNose.Reporters;
using TestMap.Services.xNose.Smells;
using TestMap.Services.xNose.Visitors;

namespace TestMap.Services.xNose;

public class xNoseService
{
    private readonly ProjectModel _projectModel;
    private readonly SqliteDatabaseService _databaseService;
    public ClassVirtualizationVisitor ClassVirtualizationVisitor { get; set; }
    public xNoseService(ProjectModel projectModel, SqliteDatabaseService sqliteDatabaseService)
    {
        _projectModel = projectModel;
        _databaseService = sqliteDatabaseService;
        ClassVirtualizationVisitor = new ClassVirtualizationVisitor();
    }

    public async Task Analyze(string solutionPath)
    {
        await XNoseAnalyze(solutionPath);
    }
    
    private async Task XNoseAnalyze(string solutionPath)
    {
        try
        {
            List<string> counter = new List<string>();
            var reporter = new JsonFileReporter(solutionPath);
            int testClassCount = 0, testMethodCount = 0;

            var testSmells = new List<ASmell>
            {
                new EmptyTestSmell(),
                new ConditionalTestSmell(),
                new CyclomaticComplexityTestSmell(),
                new ExpectedExceptionTestSmell(),
                new AssertionRouletteTestSmell(),
                new UnknownTestSmell(),
                new RedundantPrintTestSmell(),
                new SleepyTestSmell(),
                new IgnoreTestSmell(),
                new RedundantAssertionTestSmell(),
                new DuplicateAssertionTestSmell(),
                new MagicNumberTestSmell(),
                new EagerTestSmell(),
                new BoolInAssertEqualSmell(),
                new EqualInAssertSmell(),
                new SensitiveEqualitySmell(),
                new ConstructorInitializationTestSmell(),
                new ObscureInLineSetUpSmell()
            };

            Dictionary<string, Dictionary<string, bool>> otherMethodTestSmell =
                new Dictionary<string, Dictionary<string, bool>>();
            foreach (var (classDeclaration, methodDeclarations) in ClassVirtualizationVisitor.ClassWithOtherMethods)
            {
                foreach (var methodDeclaration in methodDeclarations)
                {
                    if (methodDeclaration.Body == null)
                        continue;
                    if (!otherMethodTestSmell.ContainsKey(methodDeclaration.Identifier.Text))
                        otherMethodTestSmell[methodDeclaration.Identifier.Text] = new Dictionary<string, bool>();
                    foreach (var smell in testSmells)
                    {
                        smell.Node = methodDeclaration;

                        otherMethodTestSmell[methodDeclaration.Identifier.Text][smell.Name()] = smell.HasSmell();
                    }

                }
            }

            _projectModel.Logger?.Information("Break");
            foreach (var smell in testSmells)
            {
                smell.otherMethodTestSmell = otherMethodTestSmell;
            }

            foreach (var (classDeclaration, methodDeclarations) in ClassVirtualizationVisitor.ClassWithMethods)
            {
                List<string> methodBodyCollection = new List<string>();
                var classReporter = new ClassReporter
                {
                    Name = classDeclaration.Identifier.ValueText
                };
                testClassCount++;
                testMethodCount += methodDeclarations.Count;
                // _projectModel.Logger?.Information(
                //     $"Analysis started for class: {classReporter.Name}, ProjectName: {project.Name.ToString()}");
                foreach (var methodDeclaration in methodDeclarations)
                {
                    if (methodDeclaration.Body == null)
                    {
                        string errorLine =
                            $"Could not load the body for function: {methodDeclaration.Identifier.Text} in class: {classReporter.Name}";
                        _projectModel.Logger?.Error(errorLine);
                        var tempMethodReporter = new MethodReporter
                        {
                            Name = methodDeclaration.Identifier.Text,
                            Body = errorLine
                        };
                        classReporter.AddMethodReport(tempMethodReporter);
                        continue;
                    }

                    var methodReporter = new MethodReporter
                    {
                        Name = methodDeclaration.Identifier.Text,
                        Body = methodDeclaration.Body.NormalizeWhitespace().ToFullString()
                    };
                    methodBodyCollection.Add(methodReporter.Body);
                    foreach (var smell in testSmells)
                    {
                        smell.Node = methodDeclaration;
                        var message = new MethodReporterMessage
                        {
                            Name = smell.Name(),
                            Status = smell.HasSmell() ? "Found" : "Not Found"
                        };
                        methodReporter.AddMessage(message);
                    }

                    classReporter.AddMethodReport(methodReporter);
                }

                if (HasLackOfCohesion(methodBodyCollection))
                {
                    classReporter.Message = "This class has Lack of Cohesion of Test Cases";
                }

                reporter.AddClassReporter(classReporter);
                _projectModel.Logger?.Information($"Analysis ended for class: {classReporter.Name}");
            }

            _projectModel.Logger?.Information(
                $"Total Test projects: {counter.Count()}, testClassCount: {testClassCount}, testMethodCount: {testMethodCount}"
            );
            await reporter.SaveReportAsync();
            
            // load into DB
            await SaveTestSmellsToDb(solutionPath);
        }
        catch (Exception e)
        {
            _projectModel.Logger?.Error("Error in xNose " + e.ToString());
            throw e;
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


    private bool HasLackOfCohesion(List<string> methodBodyCollection)
    {
        var cosineInstance = new Cosine();
        double cosineScoreSum = 0.0;
        int pairCount = 0;
        for (int i = 0; i < methodBodyCollection.Count; i++)
        {
            for (int j = 0; j < methodBodyCollection.Count; j++)
            {
                if (i != j)
                {
                    cosineScoreSum += cosineInstance.Similarity(methodBodyCollection[i], methodBodyCollection[j]);
                    pairCount++;
                }
            }
        }
        if (pairCount <= 0)
            return false;

        var testClassCohesionScore = cosineScoreSum / (double)pairCount;
        _projectModel.Logger?.Information(testClassCohesionScore.ToString());
        return ((1.0 - testClassCohesionScore) >= 0.6);//from paper
    }
}