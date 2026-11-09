using TestMap.Models.Configuration;
using TestMap.Models.Configuration.Experiment;
using TestMap.Models.Experiment;
using TestMap.Services.TestGeneration.TargetSelection;

namespace TestMap.UnitTests.TestGeneration;

public sealed class CandidateCohortRandomizationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildRandomOrderKey_SameSeedProducesStableOrder()
    {
        var candidates = Enumerable.Range(1, 8)
            .Select(id => new CandidateMethod
            {
                MemberId = id,
                MethodName = $"Method{id}",
                Signature = $"void Method{id}()"
            })
            .ToList();
        var config = Config(seed: 1234);

        var first = Order(candidates, config);
        var second = Order(candidates.AsEnumerable().Reverse(), config);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildRandomOrderKey_DifferentSeedProducesDifferentOrder()
    {
        var candidates = Enumerable.Range(1, 8)
            .Select(id => new CandidateMethod
            {
                MemberId = id,
                MethodName = $"Method{id}",
                Signature = $"void Method{id}()"
            })
            .ToList();

        var first = Order(candidates, Config(seed: 1234));
        var second = Order(candidates, Config(seed: 5678));

        Assert.NotEqual(first, second);
    }

    private static List<int> Order(IEnumerable<CandidateMethod> candidates, ExperimentConfig config)
    {
        return candidates
            .OrderBy(candidate => MethodSelectionService.BuildRandomOrderKey(config, candidate))
            .ThenBy(candidate => candidate.MemberId)
            .Select(candidate => candidate.MemberId)
            .ToList();
    }

    private static ExperimentConfig Config(int seed)
    {
        return new ExperimentConfig
        {
            CandidateCohort = new CandidateCohortConfig
            {
                Id = "cohort-a",
                Mode = CandidateCohortMode.Create,
                RandomSeed = seed
            }
        };
    }
}
