using TestMap.Models.Coverage;
using TestMap.Models.Testing;
using TestMap.Services.TestExecution;

namespace TestMap.UnitTests.TestExecution;

public sealed class CoverageTargetResolverTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_UsesTypeFileSignatureAndLineForOverloads()
    {
        var report = Report(
            Object("Demo.Service", "src/Service.cs",
                Method("Process", "void Process(int value)", 10, 15, 0.2),
                Method("Process", "void Process(int value, string label)", 30, 35, 0.8)),
            Object("Demo.Other", "src/Other.cs",
                Method("Process", "void Process(int value, string label)", 30, 35, 0.4)));
        var target = new TargetMemberDescriptor(
            7,
            "Process",
            "void Process(int value, string label)",
            "Demo.Service",
            "src/Service.cs",
            30,
            35);

        var resolution = CoverageTargetResolver.Resolve(report, target);

        Assert.Equal("Resolved", resolution.Status);
        Assert.Equal(0.8, resolution.LineRate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_DoesNotChooseFirstWhenBareNameIsAmbiguous()
    {
        var report = Report(
            Object("Demo.Service", "src/Service.cs",
                Method("Process", "void Process(int value)", 10, 15, 0.2),
                Method("Process", "void Process(string value)", 30, 35, 0.8)));
        var target = new TargetMemberDescriptor(7, "Process", string.Empty, string.Empty, string.Empty, 0, 0);

        var resolution = CoverageTargetResolver.Resolve(report, target);

        Assert.Equal("AmbiguousTargetMethod", resolution.Status);
        Assert.Null(resolution.LineRate);
    }

    /// <summary>
    /// Gridify's coverage lists the generic extension method IsValid&lt;T&gt; under both
    /// "Gridify.GridifyExtensions" and "Gridify.GridifyExtensions&lt;T&gt;". The two entries are
    /// one method, so the target must resolve rather than read as ambiguous.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_SameMethodListedUnderGenericAndNonGenericClass_Resolves()
    {
        const string signature = "<T>(Demo.IFiltering, out System.Collections.Generic.List<string>, Demo.IMapper<T>)";
        var report = Report(
            Object("Demo.Extensions", "src/Extensions.cs",
                Method("IsValid", "<T>(Demo.IFiltering, Demo.IMapper<T>)", 10, 15, 1.0),
                Method("IsValid", signature, 40, 60, 0.87)),
            Object("Demo.Extensions<T>", "src/Extensions.cs",
                Method("IsValid", signature, 40, 60, 0.87)));
        var target = new TargetMemberDescriptor(
            7,
            "IsValid",
            "public static bool IsValid<T>(this IFiltering filtering, out List<string> errors, IMapper<T>? mapper = null)",
            "Demo.Extensions",
            "src/Extensions.cs",
            39,
            59);

        var resolution = CoverageTargetResolver.Resolve(report, target);

        Assert.Equal("Resolved", resolution.Status);
        Assert.Equal("DuplicateEntries", resolution.MatchKind);
        Assert.Equal(0.87, resolution.LineRate);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_ConvertsZeroBasedSourceLineToOneBasedCoverageLine()
    {
        var report = Report(
            Object("Demo.Service", "src/Service.cs",
                Method("Process", "void Process()", 30, 30, 0.75)));
        var target = new TargetMemberDescriptor(
            7,
            "Process",
            "void Process()",
            "Demo.Service",
            "src/Service.cs",
            29,
            29);

        var resolution = CoverageTargetResolver.Resolve(report, target);

        Assert.Equal("Resolved", resolution.Status);
        Assert.Equal("SourceLine", resolution.MatchKind);
        Assert.Equal(0.75, resolution.LineRate);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Demo.Outer`1/Inner`1")]
    [InlineData("Demo.Outer<T>.Inner<TValue>")]
    public void Resolve_MatchesNestedGenericCoverageTypeNames(string coverageType)
    {
        var report = Report(
            Object(coverageType, "src/Outer.cs",
                Method("Process", "void Process()", 30, 30, 0.75)));
        var target = new TargetMemberDescriptor(
            7,
            "Process",
            "void Process()",
            "Demo.Outer.Inner",
            "src/Outer.cs",
            29,
            29);

        var resolution = CoverageTargetResolver.Resolve(report, target);

        Assert.Equal("Resolved", resolution.Status);
        Assert.Equal(0.75, resolution.LineRate);
    }

    private static CoverageReportModel Report(params ObjectCoverageModel[] objects) => new()
    {
        Packages =
        [
            new PackageCoverage
            {
                Classes = objects.ToList()
            }
        ]
    };

    private static ObjectCoverageModel Object(
        string type,
        string file,
        params MemberCoverageModel[] methods) => new()
    {
        Name = type,
        Filename = file,
        Methods = methods.ToList()
    };

    private static MemberCoverageModel Method(
        string name,
        string signature,
        int start,
        int end,
        double coverage) => new()
    {
        Name = name,
        Signature = signature,
        Lines = Enumerable.Range(start, end - start + 1)
            .Select(line => new LineCoverageModel { Number = line })
            .ToList(),
        LineRate = coverage
    };
}
