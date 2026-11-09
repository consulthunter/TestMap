namespace TestMap.Models.Configuration.Experiment;

public sealed class CandidateCohortConfig
{
    public string? Id { get; set; }
    public CandidateCohortMode Mode { get; set; } = CandidateCohortMode.Disabled;
    public bool Randomize { get; set; } = true;
    public int? RandomSeed { get; set; }
}

public enum CandidateCohortMode
{
    Disabled,
    Create,
    Reuse
}
