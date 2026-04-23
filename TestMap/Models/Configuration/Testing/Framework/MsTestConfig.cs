namespace TestMap.Models.Configuration.Testing.Framework;

public class MsTestConfig : IFrameworkConfig
{
    public List<string> patterns { get; set; } = new();
}
