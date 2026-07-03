namespace TestMap.Models.Configuration.AiProviders;

public interface IAiProviderConfig
{
    string Model { get; set; }
    string ApiKey { get; set; }
    AiProvider Provider { get; set; }
}