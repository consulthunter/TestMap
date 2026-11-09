using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using TestMap.Models.Targets;
using TestMap.Services.Targets.Contracts;

namespace TestMap.Services.Targets;

public sealed class DelimitedTargetImportService(TargetIdentityService identity) : ITargetImportService
{
    public async Task<TargetImportResult> ImportAsync(
        string path,
        TargetDelimiter delimiter = TargetDelimiter.Auto,
        CancellationToken cancellationToken = default)
    {
        var selectedDelimiter = delimiter == TargetDelimiter.Auto
            ? await DetectDelimiterAsync(path, cancellationToken)
            : delimiter;
        var delimiterText = selectedDelimiter == TargetDelimiter.Tab ? "\t" : ",";
        var sourceFile = Path.GetFileName(path);
        var targets = new Dictionary<string, RepositoryTarget>(StringComparer.Ordinal);
        var rejections = new List<TargetRejectionRecord>();
        var totalRecords = 0;
        var deduplicatedRecords = 0;

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiterText,
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            NewLine = "\n"
        };

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, useAsync: true);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, configuration);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
            throw new InvalidDataException("Target input must contain a header record.");

        var headers = csv.HeaderRecord ?? [];
        var nameIndex = FindHeader(headers, "name");
        var commitIndex = FindHeader(headers, "lastCommitSHA");
        if (nameIndex < 0 || commitIndex < 0)
            throw new InvalidDataException("Target input requires name and lastCommitSHA headers.");

        try
        {
            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalRecords++;
                var rawName = Sanitize(csv.GetField(nameIndex));
                var rawCommit = Sanitize(csv.GetField(commitIndex));

                var rejection = Validate(sourceFile, totalRecords, rawName, rawCommit);
                if (rejection is not null)
                {
                    rejections.Add(rejection);
                    continue;
                }

                identity.TryNormalizeRepository(rawName, out var repository);
                identity.TryNormalizeCommit(rawCommit, out var commit);
                var id = TargetIdentityService.CreateTargetId(repository, commit);
                if (targets.TryGetValue(id, out var existing))
                {
                    targets[id] = existing with { SourceRows = existing.SourceRows.Append(totalRecords).Distinct().Order().ToArray() };
                    deduplicatedRecords++;
                }
                else
                {
                    targets[id] = identity.Create(repository, commit, [totalRecords]);
                }
            }
        }
        catch (CsvHelperException ex)
        {
            totalRecords++;
            rejections.Add(new TargetRejectionRecord(
                "1.0", sourceFile, totalRecords, null, null, RejectionKind.MalformedRecord,
                $"Malformed delimited record: {ex.GetType().Name}."));
        }

        return new TargetImportResult(targets.Values.ToArray(), rejections, totalRecords, deduplicatedRecords);
    }

    private TargetRejectionRecord? Validate(string sourceFile, int row, string? name, string? commit)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Reject(RejectionKind.MissingName, "Repository name is required.");
        if (!identity.TryNormalizeRepository(name, out _))
            return Reject(RejectionKind.InvalidRepositoryName, "Repository name must use owner/name format.");
        if (string.IsNullOrWhiteSpace(commit))
            return Reject(RejectionKind.MissingCommit, "A full commit SHA is required.");
        if (!identity.TryNormalizeCommit(commit, out _))
            return Reject(RejectionKind.InvalidCommit, "Commit must contain exactly 40 hexadecimal characters.");
        return null;

        TargetRejectionRecord Reject(RejectionKind kind, string summary) =>
            new("1.0", sourceFile, row, name, commit, kind, summary);
    }

    private static int FindHeader(IReadOnlyList<string> headers, string expected)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (string.Equals(headers[index].Trim().TrimStart('\uFEFF'), expected, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private static string? Sanitize(string? value)
    {
        if (value is null) return null;
        var sanitized = value.Replace('\0', ' ').Trim();
        return sanitized.Length <= 512 ? sanitized : sanitized[..512];
    }

    private static async Task<TargetDelimiter> DetectDelimiterAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
        var comma = 0;
        var tab = 0;
        var quoted = false;
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken) > 0)
        {
            var character = buffer[0];
            if (character == '"') quoted = !quoted;
            else if (!quoted && character == ',') comma++;
            else if (!quoted && character == '\t') tab++;
            else if (!quoted && character is '\r' or '\n') break;
        }

        if (comma > 0 && tab == 0) return TargetDelimiter.Comma;
        if (tab > 0 && comma == 0) return TargetDelimiter.Tab;
        if (comma > 0 && tab > 0)
            throw new InvalidDataException("Target delimiter is ambiguous; specify comma or tab explicitly.");
        throw new InvalidDataException("Target input is not comma- or tab-delimited.");
    }
}
