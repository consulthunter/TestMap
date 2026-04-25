namespace TestMap.Models.Configuration.Testing.Generation;

public class TestSuiteImprovementWeightsConfig
{
    public double MutationWeakness { get; set; } = 0.30;
    public double TestSmells { get; set; } = 0.20;
    public double AssertionQuality { get; set; } = 0.15;
    public double Flakiness { get; set; } = 0.15;
    public double CoverageValue { get; set; } = 0.10;
    public double MaintenanceRisk { get; set; } = 0.10;

    public IReadOnlyDictionary<string, double> ToDictionary()
    {
        return new Dictionary<string, double>
        {
            [nameof(MutationWeakness)] = MutationWeakness,
            [nameof(TestSmells)] = TestSmells,
            [nameof(AssertionQuality)] = AssertionQuality,
            [nameof(Flakiness)] = Flakiness,
            [nameof(CoverageValue)] = CoverageValue,
            [nameof(MaintenanceRisk)] = MaintenanceRisk
        };
    }
}