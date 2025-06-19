namespace TestMap.Models.Configuration;

public class SettingsConfig
{
    public int MaxConcurrency { get; set; }
    public string RunDateFormat { get; set; } = "yyyy-MM-dd";
}