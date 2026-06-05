namespace TestMap.Models.Configuration.Testing.Generation;

public sealed class GenerationProducerConfig
{
    public TestGenerationProducerMode Mode { get; init; } = TestGenerationProducerMode.TestMap;
    public string? ToolId { get; init; }
}
