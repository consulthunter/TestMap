/*
 * consulthunter
 * 2025-03-26
 *
 * Generates the config for TestMap
 *
 * GenerateConfigurationService.cs
 */

using System.Text.Json;

namespace TestMap.Services.Configuration;

public class GenerateConfigurationService(string configurationFilePath, string basePath, string basePathParent)
{
    public void GenerateConfiguration()
    {
        var config = new Dictionary<string, object>
        {
            ["FilePaths"] = new Dictionary<string, string>
            {
                ["TargetFilePath"] = Path.Combine(basePath, "Data", "example_project.txt"),
                ["LogsDirPath"] = Path.Combine(basePath, "Logs"),
                ["TempDirPath"] = Path.Combine(basePathParent, "Temp"),
                ["OutputDirPath"] = Path.Combine(basePath, "Output")
            },
            ["Settings"] = new Dictionary<string, object>
            {
                ["MaxConcurrency"] = 5,
                ["RunDateFormat"] = "yyyy-MM-dd"
            },
            ["Docker"] = new Dictionary<string, string>
            {
                ["Context"] = "desktop-linux",
                ["Image"] = "net-sdk-all"
            },
            ["Frameworks"] = new Dictionary<string, List<string>>
            {
                ["NUnit"] = new() { "Test", "TestCase", "TestCaseSource", "Theory" },
                ["xUnit"] = new() { "Fact", "Theory" },
                ["MSTest"] = new() { "TestMethod", "DataSource" },
                ["Microsoft.VisualStudio.TestTools.UnitTesting"] = new() { "TestMethod", "DataSource" }
            },
            ["Persistence"] = new Dictionary<string, object>
            {
                ["KeepProjectFiles"] = true
            },
            ["Generation"] = new Dictionary<string, object>
            {
                ["Provider"] = "openai",
                ["Model"] = "gpt-3.5-turbo",
                ["MaxRetries"] = 1
            },
            ["Amazon"] = new Dictionary<string, string>
            {
                ["AwsRegion"] = "us-east-1"
            },
            ["Ollama"] = new Dictionary<string, string>
            {
                ["Endpoint"] = "http://localhost:10000/"
            },
            ["Custom"] = new Dictionary<string, string>
            {
                ["Endpoint"] = "http://localhost:11434/"
            }
        };

        // Use System.Text.Json for serialization
        var options = new JsonSerializerOptions
        {
            WriteIndented = true // Makes the output more readable (pretty print)
        };

        // Serialize the config data to a JSON string
        var json = JsonSerializer.Serialize(config, options);

        // Write the JSON configuration to the file
        File.WriteAllText(configurationFilePath, json);
    }
}