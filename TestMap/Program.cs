using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestMap.Services;

namespace TestMap;

public class Program
{
    static async Task Main(string[] args)
    {
        var settings = LoadConfig(args[0]);
        ConfigurationService configurationService = new ConfigurationService(settings);
        TestMapRunner testMapRunner = new TestMapRunner(configurationService);
        await testMapRunner.RunAsync();
    }

    public static JObject LoadConfig(string configPath)
    {
        var json = string.Empty;
        var settings = new JObject();
        // Load the configuration from JSON file
        if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }
        try
        {
            json = File.ReadAllText(configPath);
            settings = JObject.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Failed to parse config file as JSON.", ex);
        }
        return settings;
    }
}