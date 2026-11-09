using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Configuration.Testing.Generation;
using TestMap.Models.Experiment;
using TestMap.Models.Experiment.Assertions;
using TestMap.Services.Experiment.Execution;
using TestMap.Services.Experiment.Reporting;
using TestMap.Services.StaticAnalysis.Assertions;
using TestMap.Services.TestGeneration.Acceptance;
using TestMap.Services.TestGeneration.Classification;
using TestMap.Services.TestGeneration.Validation;

namespace TestMap.UnitTests.TestGeneration;

public sealed class AssertionLineageLaneParityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void EquivalentEvidence_UsesSharedPolicyAndProducesEquivalentLaneAggregates()
    {
        var config = new AssertionLineageEvaluationConfig { MaxDepth = 4 };
        var generation = MakeMeasurement("testmap", generationAttemptId: 10);
        var tool = MakeMeasurement("agent-tool", toolAttemptId: 20);
        generation.TestSummaries.Add(MakeSummary(testMemberId: 101));
        tool.TestSummaries.Add(MakeSummary(testMemberId: 201));
        var summaryService = new AssertionLineageSummaryService();

        summaryService.ApplyAttemptSummary(generation);
        summaryService.ApplyAttemptSummary(tool);
        var generationPolicy = AssertionLineagePolicy.Resolve(config);
        var toolPolicy = AssertionLineagePolicy.Resolve(config);

        Assert.Equal(generationPolicy, toolPolicy);
        Assert.Equal(AssertionMeasurementStatus.Complete, generation.Status);
        Assert.Equal(generation.Status, tool.Status);
        Assert.Equal(generation.RecognizedAssertionCount, tool.RecognizedAssertionCount);
        Assert.Equal(generation.UnrecognizedAssertionCount, tool.UnrecognizedAssertionCount);
        Assert.Equal(generation.TracedAssertionCount, tool.TracedAssertionCount);
        Assert.Equal(generation.TrivialAssertionCount, tool.TrivialAssertionCount);
        Assert.Equal(generation.UnresolvedAssertionCount, tool.UnresolvedAssertionCount);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AssertionEvidence_DoesNotChangeAcceptanceClassificationOrRepairStopping()
    {
        var validation = new GenerationValidationResult
        {
            Outcome = GenerationValidationOutcome.Passed,
            Confidence = GenerationValidationConfidence.High,
            CodeExtracted = true,
            MethodNameExtracted = true,
            SyntaxValid = true,
            CompilationSucceeded = true,
            TestsExecuted = true,
            AllTestsPassed = true,
            CoverageImproved = true,
            HasUsefulMetricSignal = true,
            ImpactEvaluable = true,
            CoverageImprovement = 0.05
        };
        var acceptanceService = new GenerationAcceptanceService();
        var classificationService = new GenerationClassificationService();
        var acceptanceBefore = acceptanceService.Evaluate(validation, new TestAcceptanceConfig());
        var classificationBefore = classificationService.Classify(validation);
        var repairCalls = 0;
        var rollbackCalls = 0;
        var attempt = new GenerationAttempt
        {
            AttemptNumber = 1,
            TestExecution = new TestMap.Models.Experiment.TestExecution
            {
                CompilationSuccess = true,
                TestsExecuted = true,
                TestPassed = true,
                CoverageAfter = 0.75,
                CoverageImprovement = 0.05,
                BaselineMutationScore = 40,
                MutationScoreAfter = 55,
                MutationScoreImprovement = 15,
                Classification = TestClassification.ValidatedEvidencePositive,
                Accepted = acceptanceBefore.Accepted,
                AssertionLineageAnalysis = new AssertionLineageAnalysisResult
                {
                    Available = true,
                    Policy = new AssertionLineagePolicy(),
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    TestSummaries = [MakeSummary(testMemberId: 101)]
                }
            }
        };

        var evaluations = await new GenerationBudgetExecutor().ExecuteAsync(
            new GenerationBudgetExecutionRequest
            {
                BudgetMode = GenerationBudgetMode.PassAt1RepairAt5,
                GenerateAsync = (_, _) => Task.FromResult(attempt),
                RepairAsync = (_, _, _) =>
                {
                    repairCalls++;
                    return Task.FromResult(new GenerationAttempt());
                },
                ShouldStopRepair = candidate =>
                    candidate.TestExecution?.Classification ==
                    TestClassification.ValidatedEvidencePositive,
                RollbackAsync = _ =>
                {
                    rollbackCalls++;
                    return Task.CompletedTask;
                }
            });
        var acceptanceAfter = acceptanceService.Evaluate(validation, new TestAcceptanceConfig());
        var classificationAfter = classificationService.Classify(validation);

        Assert.Equal(acceptanceBefore.Accepted, acceptanceAfter.Accepted);
        Assert.Equal(acceptanceBefore.Reason, acceptanceAfter.Reason);
        Assert.Equal(classificationBefore.Classification, classificationAfter.Classification);
        Assert.Single(evaluations);
        Assert.Equal(0, repairCalls);
        Assert.Equal(1, rollbackCalls);
        Assert.Equal(0.75, attempt.TestExecution.CoverageAfter);
        Assert.Equal(0.05, attempt.TestExecution.CoverageImprovement);
        Assert.Equal(55d, attempt.TestExecution.MutationScoreAfter);
        Assert.Equal(15d, attempt.TestExecution.MutationScoreImprovement);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResultRow_KeepsAssertionAndDynamicAttributionIndependent()
    {
        var row = new ExperimentResultFileRow
        {
            ImpactAttribution = "attempt_level",
            AssertionAttribution = "generated_test",
            CoverageDelta = 0.05,
            MutationScoreDelta = 4,
            TracedAssertionCount = 1
        };

        Assert.Equal("attempt_level", row.ImpactAttribution);
        Assert.Equal("generated_test", row.AssertionAttribution);
        Assert.Equal(0.05, row.CoverageDelta);
        Assert.Equal(4d, row.MutationScoreDelta);
        Assert.Equal(1, row.TracedAssertionCount);
    }

    private static AssertionLineageMeasurement MakeMeasurement(
        string lane,
        int? generationAttemptId = null,
        int? toolAttemptId = null) => new()
    {
        ProjectId = 1,
        ExperimentRunId = 2,
        CandidateMethodId = 3,
        GenerationAttemptId = generationAttemptId,
        ToolAttemptId = toolAttemptId,
        ProducerLane = lane,
        PolicyVersion = AssertionLineagePolicy.CurrentPolicyVersion,
        AssertionCatalogVersion = AssertionLineagePolicy.CurrentCatalogVersion,
        MaxDepth = 4
    };

    private static GeneratedTestAssertionSummary MakeSummary(int testMemberId) => new()
    {
        TestMemberId = testMemberId,
        Observations =
        [
            new AssertionObservation
            {
                Ordinal = 0,
                Framework = "xunit",
                AssertionMethod = "Equal",
                RecognitionKind = AssertionRecognitionKind.Semantic,
                FilePath = "WidgetTests.cs",
                StartLine = 10,
                StartColumn = 0,
                EndLine = 10,
                EndColumn = 20,
                ExpressionHash = $"{testMemberId:x64}",
                Category = AssertionLineageCategory.Traced,
                ResolutionCode = AssertionLineageReasonCodes.ProductionInvocation,
                DepthReached = 1,
                TargetRelation = AssertionTargetRelation.Candidate,
                TraceSummary = "Assertion input reaches the intended production member.",
                Steps =
                [
                    new AssertionLineageStep
                    {
                        InputIndex = 0,
                        PathIndex = 0,
                        StepIndex = 0,
                        StepKind = AssertionLineageStepKind.ProductionMember,
                        Depth = 1,
                        SymbolDisplay = "Widget.Calculate()",
                        MemberId = 3,
                        Outcome = AssertionLineageStepOutcome.Traced,
                        ReasonCode = AssertionLineageReasonCodes.ProductionInvocation,
                        Summary = "Resolved production member."
                    }
                ]
            }
        ]
    };
}
