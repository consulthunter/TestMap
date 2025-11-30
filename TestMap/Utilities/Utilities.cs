using System.Security.Cryptography;
using System.Text;

namespace TestMap.Utilities;

public static class Utilities
{
    public static void Load()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static (string, string) ExtractOwnerAndRepo(string url)
    {
        var uri = new Uri(url);
        return (uri.Segments[1].Trim('/'), uri.Segments[2].Trim('/'));
    }
    
    public static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

}