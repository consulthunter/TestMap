namespace TestMap.Models.Experiment;

public enum CandidateActionKind
{
    None,
    GenerateNewTest,
    ImproveExistingTest,
    ExtendExistingTestSuite,
    Skip
}