
namespace TestMap.Models.Configuration.AiProviders.Amazon;

public class AmazonConfig : IAiProviderConfig
{
    public string Model { get; set; } = "";
    public string AwsAccessKey { get; set; } = "";
    // api key is known as secret key
    public string ApiKey { get; set; } = "";
    public AiProvider Provider { get; set; } = AiProvider.Amazon;
    public string AwsRegion { get; set; } = "";
}