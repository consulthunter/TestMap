using TestMap.Models.Generation;

namespace TestMap.Services.TestGeneration.Bootstrap;

public sealed class TestProjectScaffoldingService : ITestProjectScaffoldingService
{
    public async Task<TestProjectScaffoldingResult> ScaffoldAsync(
        TestBootstrapRequest request,
        bool applyChanges = false,
        CancellationToken cancellationToken = default)
    {
        var sourceProjectName = Path.GetFileNameWithoutExtension(request.SourceProjectPath);
        var testProjectName = $"{sourceProjectName}{request.TestProjectSuffix}";
        var sourceProjectDirectory = Path.GetDirectoryName(request.SourceProjectPath) ?? string.Empty;
        var parentDirectory = Directory.GetParent(sourceProjectDirectory)?.FullName ?? sourceProjectDirectory;
        var testProjectDirectory = Path.Combine(parentDirectory, testProjectName);
        var testClassName = $"{sourceProjectName}Tests";
        var testFilePath = Path.Combine(testProjectDirectory, $"{testClassName}.cs");
        var scaffoldPreview = BuildScaffoldPreview(testClassName, request.Framework);

        if (applyChanges)
        {
            Directory.CreateDirectory(testProjectDirectory);
            if (!File.Exists(testFilePath))
                await File.WriteAllTextAsync(testFilePath, scaffoldPreview, cancellationToken);
        }

        return new TestProjectScaffoldingResult
        {
            Success = true,
            TestClassName = testClassName,
            TestFilePath = testFilePath,
            Framework = request.Framework,
            ScaffoldPreview = scaffoldPreview
        };
    }

    private static string BuildScaffoldPreview(string testClassName, string framework)
    {
        var usingLine = framework switch
        {
            "NUnit" => "using NUnit.Framework;",
            "MSTest" => "using Microsoft.VisualStudio.TestTools.UnitTesting;",
            _ => "using Xunit;"
        };

        var classAttribute = framework switch
        {
            "NUnit" => "[TestFixture]\n",
            "MSTest" => "[TestClass]\n",
            _ => string.Empty
        };

        return $"{usingLine}\n\n{classAttribute}public class {testClassName}\n{{\n}}\n";
    }
}