namespace TestMap.Models.Configuration.Testing.Framework;

public class NunitConfig : IFrameworkConfig
{
    public List<string> patterns { get; set; } = new();
}
