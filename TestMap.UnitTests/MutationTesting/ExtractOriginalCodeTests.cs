using TestMap.Models.Code;
using TestMap.Persistence.Ef.Repositories.MutationTesting;

namespace TestMap.UnitTests.MutationTesting;

public sealed class ExtractOriginalCodeTests
{
    // Helper: Location(startLine, startCol, endLine, endCol) — all 0-based.
    private static Location At(int startLine, int startCol, int endLine, int endCol)
        => new(startLine, startCol, endLine, endCol);

    [Fact]
    public void SingleLine_ExtractsExactSpan()
    {
        const string source = "var x = a + b;";
        // "a + b" is at columns 8–13 (0-based, end exclusive)
        var loc = At(0, 8, 0, 13);

        var result = MutationTestingReportRepository.ExtractOriginalCode(source, loc);

        Assert.Equal("a + b", result);
    }

    [Fact]
    public void SingleLine_SecondLine_ExtractsCorrectly()
    {
        const string source = "line one\nvar x = a + b;";
        var loc = At(1, 8, 1, 13);

        var result = MutationTestingReportRepository.ExtractOriginalCode(source, loc);

        Assert.Equal("a + b", result);
    }

    [Fact]
    public void MultiLine_ExtractsAndTrims()
    {
        const string source = "if (x > 0\n    && y > 0)";
        // Span starts at col 4 on line 0 ("x > 0") through col 12 on line 1 ("&& y > 0")
        var loc = At(0, 4, 1, 12);

        var result = MutationTestingReportRepository.ExtractOriginalCode(source, loc);

        Assert.Equal("x > 0\n    && y > 0", result);
    }

    [Fact]
    public void EmptySource_ReturnsEmpty()
    {
        var result = MutationTestingReportRepository.ExtractOriginalCode(string.Empty, At(0, 0, 0, 5));

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StartLineBeyondSource_ReturnsEmpty()
    {
        const string source = "one line";
        var result = MutationTestingReportRepository.ExtractOriginalCode(source, At(5, 0, 5, 4));

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ColumnsClampToLineLength_NoException()
    {
        const string source = "short";
        // Ask for columns well past the end
        var loc = At(0, 0, 0, 100);

        var result = MutationTestingReportRepository.ExtractOriginalCode(source, loc);

        Assert.Equal("short", result);
    }

    [Fact]
    public void CrLfSource_StripsCarriageReturn()
    {
        const string source = "line one\r\nvar x = a + b;\r\n";
        var loc = At(1, 8, 1, 13);

        var result = MutationTestingReportRepository.ExtractOriginalCode(source, loc);

        Assert.Equal("a + b", result);
    }
}
