namespace TestMap.Models.Configuration.Testing;

public class FrameworkConfig : IFrameworkConfig
{
    public List<string> patterns { get; set; } = new();
}