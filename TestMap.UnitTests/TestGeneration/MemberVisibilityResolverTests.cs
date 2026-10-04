using TestMap.Services.StaticAnalysis;
using TestMap.Services.TestGeneration.TargetSelection;
using Location = TestMap.Models.Code.Location;

namespace TestMap.UnitTests.TestGeneration;

public sealed class MemberVisibilityResolverTests
{
    // Interface members carry no access modifier: they are implicitly public, and a test can
    // call them directly (pilot gridify: IGridifyMapper.GenerateMappings).
    private const string InterfaceMethod = """
                                           /// <summary>
                                           /// Generates property mappings for the specified class type.
                                           /// </summary>
                                           IGridifyMapper<T> GenerateMappings(ushort maxNestingDepth);
                                           """;

    // The persisted FullString includes the body; its text says nothing about the member's
    // own accessibility.
    private const string PrivateMethodMentioningPublic = """
                                                         private string Describe()
                                                         {
                                                             return "public API surface";
                                                         }
                                                         """;

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_InterfaceMethodWithoutModifier_IsPublic()
    {
        var visibility = MemberVisibilityResolver.Resolve([], InterfaceMethod, "interface");

        Assert.Equal(MemberVisibility.Public, visibility);
        Assert.Equal(TestAccessStrategy.DirectPublicCall, MethodSelectionService.ResolveDirectAccessStrategy(visibility));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(true)]
    [InlineData(false)] // no persisted modifiers: resolved from the declaration header
    public void Resolve_PrivateMethodWhoseBodyMentionsPublic_StaysPrivate(bool persistedModifier)
    {
        string[] modifiers = persistedModifier ? ["private"] : [];
        var visibility = MemberVisibilityResolver.Resolve(modifiers, PrivateMethodMentioningPublic, "class");

        Assert.Equal(MemberVisibility.Private, visibility);
        Assert.Equal(TestAccessStrategy.NotReasonablyTestable, MethodSelectionService.ResolveDirectAccessStrategy(visibility));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ClassMethodWithoutModifier_IgnoresDocCommentText()
    {
        const string method = """
                              /// Mirrors the public overload.
                              string Describe() => "x";
                              """;

        Assert.Equal(MemberVisibility.Unknown, MemberVisibilityResolver.Resolve([], method, "class"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ExplicitInterfaceImplementation_IsExplicitInterface()
    {
        const string method = "IGridifyMapper<T> IGridifyMapper<T>.GenerateMappings() => this;";

        Assert.Equal(MemberVisibility.ExplicitInterface, MemberVisibilityResolver.Resolve([], method, "class"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveAccessPathStrategy_TestCallsInterfaceMethod_IsDirectPublicCall()
    {
        var index = new RoslynMemberSymbolIndex([
            CreateRow(1, "GenerateNestedMappings", "void GenerateNestedMappings() { }", [], "GridifyMapperShould", "class", isTest: true),
            CreateRow(2, "GenerateMappings", InterfaceMethod, [], "IGridifyMapper", "interface", isTest: false)
        ]);

        Assert.Equal(
            nameof(TestAccessStrategy.DirectPublicCall),
            RoslynSourceTestTraceService.ResolveAccessPathStrategy([1, 2], index));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResolveAccessPathStrategy_TestCallsPrivateMethodMentioningPublic_IsNotReasonablyTestable()
    {
        var index = new RoslynMemberSymbolIndex([
            CreateRow(1, "Describes", "void Describes() { }", [], "DescriberTests", "class", isTest: true),
            CreateRow(2, "Describe", PrivateMethodMentioningPublic, ["private"], "Describer", "class", isTest: false)
        ]);

        Assert.Equal(
            nameof(TestAccessStrategy.NotReasonablyTestable),
            RoslynSourceTestTraceService.ResolveAccessPathStrategy([1, 2], index));
    }

    private static RoslynMemberSymbolRow CreateRow(
        int id,
        string name,
        string fullString,
        IReadOnlyList<string> modifiers,
        string objectName,
        string objectKind,
        bool isTest)
    {
        var directory = isTest ? "tests/Product.Tests" : "src/Product";
        var project = isTest ? "tests/Product.Tests/Product.Tests.csproj" : "src/Product/Product.csproj";
        return new RoslynMemberSymbolRow(
            id,
            name,
            "method",
            fullString,
            modifiers,
            isTest,
            false,
            "hash",
            objectName,
            "Product",
            isTest,
            objectKind,
            $"{directory}/{objectName}.cs",
            project,
            isTest,
            new Location(id, 0, id, 0));
    }
}
