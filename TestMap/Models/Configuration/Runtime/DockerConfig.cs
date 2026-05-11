namespace TestMap.Models.Configuration.Runtime;

public class DockerConfig
{
    public string Image { get; set; } = "";
    public string Context { get; set; } = "";
    public string WindowsNetwork { get; set; } = "";
}
