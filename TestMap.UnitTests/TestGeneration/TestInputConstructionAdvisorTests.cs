using TestMap.Services.TestGeneration.Construction;

namespace TestMap.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for <see cref="TestInputConstructionAdvisor"/>. All methods under test are
/// pure static functions with no dependencies, so no test doubles or databases are needed.
/// </summary>
public sealed class TestInputConstructionAdvisorTests
{
    // ── NormalizeTypeName ─────────────────────────────────────────────────────

    /// <summary>
    /// A fully-qualified name returns only the last segment after the final dot.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeTypeName_FullyQualifiedName_ReturnsLastSegment()
    {
        Assert.Equal("MyClass", TestInputConstructionAdvisor.NormalizeTypeName("MyNamespace.MyClass"));
    }

    /// <summary>
    /// A multi-segment qualified name returns only the rightmost segment.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeTypeName_ThreePartQualifiedName_ReturnsRightmostSegment()
    {
        Assert.Equal("Leaf", TestInputConstructionAdvisor.NormalizeTypeName("A.B.Leaf"));
    }

    /// <summary>
    /// A bare type name with no dots is returned unchanged (modulo trimming).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeTypeName_UnqualifiedName_ReturnedUnchanged()
    {
        Assert.Equal("int", TestInputConstructionAdvisor.NormalizeTypeName("int"));
    }

    /// <summary>
    /// Leading and trailing whitespace is trimmed before processing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NormalizeTypeName_WithWhitespace_Trimmed()
    {
        Assert.Equal("string", TestInputConstructionAdvisor.NormalizeTypeName("  string  "));
    }

    // ── RequiresMocking ────────────────────────────────────────────────────────

    /// <summary>
    /// A type name starting with a capital I followed by another uppercase letter is treated
    /// as an interface and requires mocking.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RequiresMocking_InterfaceTypeName_ReturnsTrue()
    {
        Assert.True(TestInputConstructionAdvisor.RequiresMocking("IService"));
        Assert.True(TestInputConstructionAdvisor.RequiresMocking("IMyRepository"));
        Assert.True(TestInputConstructionAdvisor.RequiresMocking("ILogger<T>"));
    }

    /// <summary>
    /// "Int32" starts with 'I' followed by uppercase 'n', but is explicitly excluded from
    /// the mocking heuristic because it is a primitive alias.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RequiresMocking_Int32_ReturnsFalse()
    {
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("Int32"));
    }

    /// <summary>
    /// A type with a lowercase second character (e.g. "In", "inner") is not an interface
    /// by convention and does not require mocking.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RequiresMocking_LowercaseSecondChar_ReturnsFalse()
    {
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("Inner"));
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("Impl"));
    }

    /// <summary>
    /// Primitive and concrete types do not require mocking.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RequiresMocking_PrimitiveAndConcreteTypes_ReturnsFalse()
    {
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("string"));
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("MyService"));
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("List<string>"));
    }

    /// <summary>
    /// A single-character type name "I" is not treated as an interface (requires length > 1).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void RequiresMocking_SingleCharI_ReturnsFalse()
    {
        Assert.False(TestInputConstructionAdvisor.RequiresMocking("I"));
    }

    // ── ForTypeName ────────────────────────────────────────────────────────────

    /// <summary>
    /// An empty or whitespace-only type name produces a strategy of "unknown".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ForTypeName_EmptyTypeName_ReturnsUnknownStrategy()
    {
        var advice = TestInputConstructionAdvisor.ForTypeName("", "x");
        Assert.Equal("unknown", advice.Strategy);

        var adviceWs = TestInputConstructionAdvisor.ForTypeName("   ", "x");
        Assert.Equal("unknown", adviceWs.Strategy);
    }

    /// <summary>
    /// "Type" and "System.Type" are recognized as the type-token pattern and produce the
    /// "type-token" strategy with instructions not to instantiate System.Type.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Type")]
    [InlineData("System.Type")]
    public void ForTypeName_SystemTypeInput_ReturnsTypeTokenStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "t");
        Assert.Equal("type-token", advice.Strategy);
        Assert.Contains("typeof", advice.Summary);
    }

    /// <summary>
    /// Known primitive types (int, string, bool, double, DateTime, Guid, etc.) produce a
    /// "primitive-or-default" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("int")]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("double")]
    [InlineData("decimal")]
    [InlineData("DateTime")]
    [InlineData("Guid")]
    [InlineData("CancellationToken")]
    public void ForTypeName_PrimitiveKnownType_ReturnsPrimitiveOrDefaultStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "p");
        Assert.Equal("primitive-or-default", advice.Strategy);
    }

    /// <summary>
    /// A nullable type name ending with '?' (e.g. "int?", "string?") also produces a
    /// "primitive-or-default" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("int?")]
    [InlineData("string?")]
    public void ForTypeName_NullableType_ReturnsPrimitiveOrDefaultStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "n");
        Assert.Equal("primitive-or-default", advice.Strategy);
    }

    /// <summary>
    /// Array, List, IEnumerable, IReadOnlyList, and ICollection types produce a
    /// "collection" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("string[]")]
    [InlineData("IEnumerable<string>")]
    [InlineData("List<int>")]
    [InlineData("IReadOnlyList<MyDto>")]
    [InlineData("ICollection<string>")]
    public void ForTypeName_CollectionType_ReturnsCollectionStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "col");
        Assert.Equal("collection", advice.Strategy);
    }

    /// <summary>
    /// Interface-named types (I + uppercase second char) produce a "mock" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("IService")]
    [InlineData("IRepository")]
    [InlineData("ILogger")]
    public void ForTypeName_InterfaceType_ReturnsMockStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "svc");
        Assert.Equal("mock", advice.Strategy);
        Assert.Contains("mock", advice.Summary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Type names ending with "Status", "Kind", "Mode", or "State" are heuristically treated
    /// as enum-like and produce the "enum-or-named-value" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("OrderStatus")]
    [InlineData("RuleConfidenceKind")]
    [InlineData("BuildMode")]
    [InlineData("ConnectionState")]
    public void ForTypeName_EnumLikeName_ReturnsEnumOrNamedValueStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "e");
        Assert.Equal("enum-or-named-value", advice.Strategy);
    }

    /// <summary>
    /// A concrete class name with no recognised pattern produces the "construct" strategy.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("MyService")]
    [InlineData("CustomerDto")]
    [InlineData("RequestModel")]
    public void ForTypeName_ConcreteClass_ReturnsConstructStrategy(string typeName)
    {
        var advice = TestInputConstructionAdvisor.ForTypeName(typeName, "obj");
        Assert.Equal("construct", advice.Strategy);
    }

    /// <summary>
    /// A fully-qualified name is normalized before dispatch; the last segment governs
    /// the strategy selection.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ForTypeName_FullyQualifiedConcreteClass_UsesLastSegmentForStrategy()
    {
        // "My.Namespace.MyService" → normalizes to "MyService" → construct
        var advice = TestInputConstructionAdvisor.ForTypeName("My.Namespace.MyService", "svc");
        Assert.Equal("construct", advice.Strategy);
    }

    /// <summary>
    /// The variable name passed to ForTypeName appears in the advice summary so the caller
    /// can produce meaningful output.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ForTypeName_VariableNameIncludedInSummary()
    {
        var advice = TestInputConstructionAdvisor.ForTypeName("MyService", "myVariable");
        Assert.Contains("myVariable", advice.Summary);
    }

    // ── BuildParameterSnippet ─────────────────────────────────────────────────

    /// <summary>
    /// "string" and "String" produce a string literal snippet.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("string")]
    [InlineData("String")]
    public void BuildParameterSnippet_StringType_ProducesStringLiteralSnippet(string typeName)
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet(typeName, "s");
        Assert.Contains("\"value\"", snippet);
        Assert.Contains("var s =", snippet);
    }

    /// <summary>
    /// "int" and "Int32" produce an integer literal snippet.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("int")]
    [InlineData("Int32")]
    public void BuildParameterSnippet_IntType_ProducesIntegerLiteralSnippet(string typeName)
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet(typeName, "n");
        Assert.Equal("var n = 1;", snippet);
    }

    /// <summary>
    /// "long" and "Int64" produce a long literal snippet with the L suffix.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("long")]
    [InlineData("Int64")]
    public void BuildParameterSnippet_LongType_ProducesLongLiteralSnippet(string typeName)
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet(typeName, "l");
        Assert.Equal("var l = 1L;", snippet);
    }

    /// <summary>
    /// "bool" and "Boolean" produce a true literal snippet.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("bool")]
    [InlineData("Boolean")]
    public void BuildParameterSnippet_BoolType_ProducesTrueLiteralSnippet(string typeName)
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet(typeName, "b");
        Assert.Equal("var b = true;", snippet);
    }

    /// <summary>
    /// "Type" produces a typeof(object) token snippet.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildParameterSnippet_TypeType_ProducesTypeofSnippet()
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet("Type", "t");
        Assert.Equal("var t = typeof(object);", snippet);
    }

    /// <summary>
    /// Interface types produce a comment suggesting a mock or fake should be arranged.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildParameterSnippet_InterfaceType_ProducesMockComment()
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet("IService", "svc");
        Assert.StartsWith("//", snippet);
        Assert.Contains("mock", snippet, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A concrete class type produces a new-expression snippet.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildParameterSnippet_ConcreteClass_ProducesNewExpressionSnippet()
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet("MyRequest", "req");
        Assert.Contains("new MyRequest()", snippet);
        Assert.Contains("var req =", snippet);
    }

    /// <summary>
    /// Nullable types (e.g. "int?") strip the trailing '?' before generating the snippet.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildParameterSnippet_NullableInt_StripsNullableAndUsesBaseType()
    {
        var snippet = TestInputConstructionAdvisor.BuildParameterSnippet("int?", "n");
        Assert.Equal("var n = 1;", snippet);
    }
}
