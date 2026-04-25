using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.Services.TestGeneration.Editing;

public interface ITestCodeEditService
{
    bool EnsureTestClassExists(CandidateMethodContext context);

    bool AppendTestMethod(CandidateMethodContext context, string testMethodCode);

    bool ReplaceTestMethod(CandidateMethodContext context, string existingMethodName, string replacementTestMethodCode);
}