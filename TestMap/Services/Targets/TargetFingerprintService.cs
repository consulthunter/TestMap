using System.Security.Cryptography;
using System.Text;

namespace TestMap.Services.Targets;

public sealed class TargetFingerprintService
{
    public string ComputeSha256(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public async Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    public static byte[] EncodeDeterministic(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(normalized);
    }
}
