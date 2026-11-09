using System.Text;

namespace TestMap.Services.TestExecution;

internal static class CoverageTypeName
{
    public static bool Matches(string coverageType, string targetType)
    {
        if (string.IsNullOrWhiteSpace(targetType)) return true;

        var coverage = Normalize(coverageType);
        var target = Normalize(targetType);
        return coverage.Equals(target, StringComparison.OrdinalIgnoreCase) ||
               coverage.EndsWith("." + target, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        var source = value
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace('+', '.')
            .Replace('/', '.')
            .Trim();
        var builder = new StringBuilder(source.Length);
        var genericDepth = 0;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character == '<')
            {
                genericDepth++;
                continue;
            }

            if (character == '>')
            {
                if (genericDepth > 0) genericDepth--;
                continue;
            }

            if (genericDepth > 0) continue;

            if (character == '`')
            {
                while (index + 1 < source.Length && char.IsDigit(source[index + 1])) index++;
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim('.');
    }

    public static string SimpleName(string value)
    {
        var normalized = Normalize(value);
        var separator = normalized.LastIndexOf('.');
        return separator < 0 ? normalized : normalized[(separator + 1)..];
    }
}
