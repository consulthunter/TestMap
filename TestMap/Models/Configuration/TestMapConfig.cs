using TestMap.Models.Configuration.Providers;

namespace TestMap.Models.Configuration;

public class TestMapConfig
{
    public FilePathConfig FilePaths { get; set; } = new();
    public SettingsConfig Settings { get; set; } = new();
    public Dictionary<string, string>? Docker { get; set; }
    public Dictionary<string, List<string>>? Frameworks { get; set; }
    public PersistenceConfig Persistence { get; set; } = new();
    public GenerationConfig Generation { get; set; } = new();
    public AmazonConfig Amazon { get; set; } = new();
    public OllamaConfig Ollama { get; set; } = new();
    public OpenAiConfig OpenAi { get; set; } = new();
    public GoogleConfig Google { get; set; } = new();
    public CustomConfig Custom { get; set; } = new();
}