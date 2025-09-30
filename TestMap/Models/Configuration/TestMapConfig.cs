namespace TestMap.Models.Configuration;


public class TestMapConfig
{
    public FilePathConfig FilePaths { get; set; } = new();
    public SettingsConfig Settings { get; set; } = new();
    public Dictionary<string, string>? Docker { get; set; }
    public Dictionary<string, List<string>>? Frameworks { get; set; }
    public PersistenceConfig Persistence { get; set; } = new();
    public GenerationConfig Generation { get; set; } = new();
}