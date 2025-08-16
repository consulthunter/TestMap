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

public class GenerateConfigurationService(string configurationFilePath, string basePath)
{
    public void GenerateConfiguration()
    {
        var config = new Dictionary<string, object>()
        {
            ["FilePaths"] = new Dictionary<string, string>()
            {
                ["TargetFilePath"] = Path.Combine(basePath, "TestMap", "Data", "example_project.txt"),
                ["LogsDirPath"] = Path.Combine(basePath, "TestMap" ,"Logs"),
                ["TempDirPath"] = Path.Combine(basePath, "Temp"),
                ["OutputDirPath"] = Path.Combine(basePath, "TestMap", "Output"),
                ["MigrationsFilePath"] = Path.Combine(basePath, "TestMap", "Migrations", "testmap.sql")
            },
            ["Settings"] = new Dictionary<string, object>()
            {
                ["MaxConcurrency"] = 5,
                ["RunDateFormat"] = "yyyy-MM-dd"
            },
            ["Docker"] =  new Dictionary<string, string>()
            {
                ["all"] = "net-sdk-all"
            },
            ["Frameworks"] = new Dictionary<string, List<string>>()
            {
                ["NUnit"] = new List<string> { "Test", "TestCase", "TestCaseSource", "Theory" },
                ["xUnit"] = new List<string> { "Fact", "Theory" },
                ["MSTest"] = new List<string> { "TestMethod", "DataSource" },
                ["Microsoft.VisualStudio.TestTools.UnitTesting"] = new List<string> { "TestMethod", "DataSource" }
            },
            ["Persistence"] = new Dictionary<string, object>()
            {
                ["KeepProjectFiles"] = true,
            },
            ["Generation"] = new Dictionary<string, object>()
            {
                ["Provider"] = "heuristic",
                ["Parameters"] = new Dictionary<string, object>()
                {
                    ["maxTests"] = 20,
                    ["timeoutSeconds"] = 30
                }
            }
            ["Export"] = new Dictionary<string, object>()
            {
                ["Format"] = "json",
                ["Type"] = "TestMethods",
                ["FilePath"] = Path.Combine(basePath, "TestMap", "Output")
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