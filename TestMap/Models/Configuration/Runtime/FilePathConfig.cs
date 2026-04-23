namespace TestMap.Models.Configuration.Runtime;

public class FilePathConfig
{
    public string? TargetFilePath { get; set; }
    public string? LogsDirPath { get; set; }
    public string? TempDirPath { get; set; }
    public string? OutputDirPath { get; set; }
    public string? MigrationsFilePath { get; set; }
}