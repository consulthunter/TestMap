using TestMap.Models.Experiment.Assertions;

namespace TestMap.UnitTests.StaticAnalysis.Assertions;

public sealed class AssertionLineageSliceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_DirectProductionInvocation_IsTraced()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "Xunit.Assert.Equal(42, new Product.Service().GetValue());");

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Traced, observation.Category);
        Assert.Equal(AssertionTargetRelation.Candidate, observation.TargetRelation);
        Assert.Contains(observation.Steps, step =>
            step.StepKind == AssertionLineageStepKind.ProductionMember &&
            step.MemberId == harness.IntendedSourceMemberId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_AssertionWrappingOperandCall_CountsOnlyTheAssertion()
    {
        // The operand call (string.Contains) shares a name with an assertion terminal but is
        // not itself an assertion; counting it would double every recognized-assertion total.
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "var actual = new Product.Service().GetName(); Xunit.Assert.True(actual.Contains(\"a\"));",
            productionMembers: "public string GetName() => \"abc\";",
            intendedSourceMemberName: "GetName");

        var result = await harness.AnalyzeAsync();

        var summary = Assert.Single(result.TestSummaries);
        Assert.Equal(1, summary.RecognizedAssertionCount);
        Assert.Single(summary.Observations);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_LocalAssignedFromProduction_IsTraced()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "var value = new Product.Service().GetValue(); Xunit.Assert.Equal(42, value);");

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Traced, observation.Category);
        Assert.Contains(observation.Steps, step =>
            step.StepKind == AssertionLineageStepKind.LocalRead);
        Assert.Equal(1, observation.DepthReached);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_SameMemberDifferentBody_ProducesDistinctContentHashes()
    {
        // A member row is identified by object, kind, name and signature -- not by its body --
        // so every attempt that emits a same-named test resolves to the same member id and the
        // row's own hash cannot tell those attempts apart. The summary must fingerprint the
        // declaration it analysed, or observations from six different generated tests are
        // indistinguishable once written.
        await using var first = await AssertionLineageAnalyzerHarness.CreateAsync(
            "Xunit.Assert.Equal(42, new Product.Service().GetValue());");
        await using var second = await AssertionLineageAnalyzerHarness.CreateAsync(
            "var value = new Product.Service().GetValue(); Xunit.Assert.Equal(42, value);");

        var firstSummary = Assert.Single((await first.AnalyzeAsync()).TestSummaries);
        var secondSummary = Assert.Single((await second.AnalyzeAsync()).TestSummaries);

        Assert.Equal(first.TestMemberId, second.TestMemberId);
        Assert.NotEqual(firstSummary.TestMemberContentHash, secondSummary.TestMemberContentHash);
        Assert.NotEqual(firstSummary.FallbackIdentityHash, secondSummary.FallbackIdentityHash);
        // The seeded member row carries a placeholder identity hash; the summary must not echo it.
        Assert.DoesNotContain("test-", firstSummary.TestMemberContentHash);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_OperandFromFrameworkVirtual_IsNotAmbiguousDispatch()
    {
        // object.ToString() is virtual and nothing in the analysed source overrides it, so
        // dispatch resolution finds zero source implementations. That is not ambiguity: the call
        // already binds to exactly one target. Treating it as ambiguous made the capture-console
        // shape -- write into a TextWriter, assert on writer.ToString() -- report Unresolved for
        // every assertion in it.
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            """
            System.IO.TextWriter writer = new System.IO.StringWriter();
            var actual = writer.ToString();
            Xunit.Assert.True(actual.Contains("a"));
            """);

        var observation = await AnalyzeSingleAsync(harness);

        Assert.DoesNotContain(observation.Steps, step =>
            step.ReasonCode == AssertionLineageReasonCodes.AmbiguousDispatch);
        Assert.NotEqual(AssertionLineageCategory.Unresolved, observation.Category);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_LiteralOnlyAssertion_IsTrivial()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "Xunit.Assert.True(true);");

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Trivial, observation.Category);
        Assert.Equal(AssertionTargetRelation.NoProduction, observation.TargetRelation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_MixedConditionalAlternatives_IsUnresolved()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            """
            var flag = DateTime.UtcNow.Ticks > 0;
            var value = flag ? new Product.Service().GetValue() : 0;
            Xunit.Assert.Equal(42, value);
            """);

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Unresolved, observation.Category);
        Assert.Equal(AssertionLineageReasonCodes.AmbiguousDefinitions, observation.ResolutionCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_ExactlyFourAssignmentHops_IsTraced()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            """
            var a = new Product.Service().GetValue();
            var b = a;
            var c = b;
            var d = c;
            Xunit.Assert.Equal(42, d);
            """);

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Traced, observation.Category);
        Assert.Equal(4, observation.DepthReached);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_RequiredFifthAssignmentHop_IsUnresolved()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            """
            var a = new Product.Service().GetValue();
            var b = a;
            var c = b;
            var d = c;
            var e = d;
            Xunit.Assert.Equal(42, e);
            """);

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Unresolved, observation.Category);
        Assert.Equal(AssertionLineageReasonCodes.DepthExceeded, observation.ResolutionCode);
        Assert.Equal(4, observation.DepthReached);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnalyzeAsync_CyclicTestHelpers_IsUnresolved()
    {
        await using var harness = await AssertionLineageAnalyzerHarness.CreateAsync(
            "Xunit.Assert.Equal(42, HelperA());",
            """
            private int HelperA() => HelperB();
            private int HelperB() => HelperA();
            """);

        var observation = await AnalyzeSingleAsync(harness);

        Assert.Equal(AssertionLineageCategory.Unresolved, observation.Category);
        Assert.Equal(AssertionLineageReasonCodes.CycleDetected, observation.ResolutionCode);
    }

    private static async Task<AssertionObservation> AnalyzeSingleAsync(
        AssertionLineageAnalyzerHarness harness)
    {
        var result = await harness.AnalyzeAsync();
        Assert.True(result.Available, result.FailureReason);
        var summary = Assert.Single(result.TestSummaries);
        Assert.Equal(GeneratedTestAssertionStatus.Classified, summary.Status);
        return Assert.Single(summary.Observations);
    }
}
