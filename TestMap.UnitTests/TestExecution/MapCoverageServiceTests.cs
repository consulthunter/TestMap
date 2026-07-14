using TestMap.Models.Code;
using TestMap.Models.Coverage;
using TestMap.Services.TestExecution.Mapping;

namespace TestMap.UnitTests.TestExecution;

public sealed class MapCoverageServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CoverageLinesOverlapMember_ConvertsZeroBasedSourceLineToOneBasedCoverageLine()
    {
        var member = new MemberModel(
            [],
            [],
            [],
            new Location(29, 0, 29, 20),
            name: "Process",
            kind: "method");
        var coverage = new MemberCoverageModel
        {
            Lines = [new LineCoverageModel { Number = 30 }]
        };

        Assert.True(MapCoverageService.CoverageLinesOverlapMember(coverage, member));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CoverageLinesOverlapMember_DoesNotMatchAdjacentPreviousLine()
    {
        var member = new MemberModel(
            [],
            [],
            [],
            new Location(29, 0, 29, 20),
            name: "Process",
            kind: "method");
        var coverage = new MemberCoverageModel
        {
            Lines = [new LineCoverageModel { Number = 29 }]
        };

        Assert.False(MapCoverageService.CoverageLinesOverlapMember(coverage, member));
    }
}
