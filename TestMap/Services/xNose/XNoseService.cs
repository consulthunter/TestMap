using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using F23.StringSimilarity;
using TestMap.Models;
using TestMap.Services.xNose.Reporters;
using TestMap.Services.xNose.Smells;
using TestMap.Services.xNose.Visitors;

namespace TestMap.Services.xNose;

public class XNoseService : IXNoseService
{
    private readonly ProjectModel _project;
    public XNoseService(ProjectModel projectModel)
    {
        _project = projectModel;
    }

    public async Task XNoseServiceAsync(String solutionPath)
    {
        // Attempt to set the version of MSBuild.
        // Console.WriteLine(args.ToString());
        _project.Logger?.Information($"xNose starting with solution path: '{solutionPath}'");

        var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
        var instance = visualStudioInstances.Length == 1
            // If there is only one instance of MSBuild on this machine, set that as the one to use.
            ? visualStudioInstances[0]
            // Handle selecting the version of MSBuild you want to use.
            : SelectVisualStudioInstance(visualStudioInstances);

        _project.Logger?.Information($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");

        // NOTE: Be sure to register an instance with the MSBuildLocator
        //       before calling MSBuildWorkspace.Create()
        //       otherwise, MSBuildWorkspace won't MEF compose.
        MSBuildLocator.RegisterInstance(instance);

        using (var workspace = MSBuildWorkspace.Create())
        {
            // Print message for WorkspaceFailed event to help diagnosing project load failures.
            workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);

            // var solutionPath = args[0];

            _project.Logger?.Information($"Loading solution '{solutionPath}'");

            // Attach progress reporter so we print projects as they are loaded.
            var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());
            _project.Logger?.Information($"Finished loading solution '{solutionPath}'");

            // TODO: Do analysis on the projects in the loaded solution
            var projects = solution.Projects.Select(p => p)
                .Where(p => p.Name.Contains("Test", StringComparison.InvariantCultureIgnoreCase));
            List<string> counter = new List<string>();
            var reporter = new JsonFileReporter(solutionPath);
            int testClassCount = 0, testMethodCount = 0;
            foreach (var project in projects)
            {
                if (counter.Contains(project.FilePath.ToString()))
                {
                    continue;
                }

                _project.Logger?.Information(project.AssemblyName.ToString());
                _project.Logger?.Information(project.DefaultNamespace.ToString());
                counter.Add(project.FilePath.ToString());

                var compilation = await project.GetCompilationAsync();

                var classVisitor = new ClassVirtualizationVisitor();

                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    classVisitor.Visit(syntaxTree.GetRoot());
                }

                //outside loop
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
                foreach (var (classDeclaration, methodDeclarations) in classVisitor.ClassWithOtherMethods)
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

                _project.Logger?.Information("Break");
                foreach (var smell in testSmells)
                {
                    smell.otherMethodTestSmell = otherMethodTestSmell;
                }

                foreach (var (classDeclaration, methodDeclarations) in classVisitor.ClassWithMethods)
                {
                    List<string> methodBodyCollection = new List<string>();
                    var classReporter = new ClassReporter
                    {
                        Name = classDeclaration.Identifier.ValueText
                    };
                    testClassCount++;
                    testMethodCount += methodDeclarations.Count;
                    _project.Logger?.Information(
                        $"Analysis started for class: {classReporter.Name}, ProjectName: {project.Name.ToString()}");
                    foreach (var methodDeclaration in methodDeclarations)
                    {
                        if (methodDeclaration.Body == null)
                        {
                            string errorLine =
                                $"Could not load the body for function: {methodDeclaration.Identifier.Text} in class: {classReporter.Name}";
                            _project.Logger?.Error(errorLine);
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
                    _project.Logger?.Information($"Analysis ended for class: {classReporter.Name}");
                }
            }

            _project.Logger?.Information(
                $"Total Test projects: {counter.Count()}, testClassCount: {testClassCount}, testMethodCount: {testMethodCount}");
            await reporter.SaveReportAsync();
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
        _project.Logger?.Information(testClassCohesionScore.ToString());
        return ((1.0 - testClassCohesionScore) >= 0.6);//from paper
    }
    private VisualStudioInstance SelectVisualStudioInstance(VisualStudioInstance[] visualStudioInstances)
    {
        _project.Logger?.Information("Multiple installs of MSBuild detected please select one:");
        for (int i = 0; i < visualStudioInstances.Length; i++)
        {
            _project.Logger?.Information($"Instance {i + 1}");
             _project.Logger?.Information($"    Name: {visualStudioInstances[i].Name}");
             _project.Logger?.Information($"    Version: {visualStudioInstances[i].Version}");
             _project.Logger?.Information($"    MSBuild Path: {visualStudioInstances[i].MSBuildPath}");
        }

        while (true)
        {
            var userResponse = Console.ReadLine();
            if (int.TryParse(userResponse, out int instanceNumber) &&
                instanceNumber > 0 &&
                instanceNumber <= visualStudioInstances.Length)
            {
                return visualStudioInstances[instanceNumber - 1];
            }
            _project.Logger?.Information("Input not accepted, try again.");
        }
    }

}

