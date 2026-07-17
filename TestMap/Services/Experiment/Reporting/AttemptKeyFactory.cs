using System.Security.Cryptography;
using System.Text;

namespace TestMap.Services.Experiment.Reporting;

public static class AttemptKeyFactory
{
    public static string Create(
        string repositoryIdentity,
        string resolvedCommit,
        string experimentSeriesId,
        string experimentRunUid,
        string producerLane,
        string resumeStableKey,
        int attemptNumber)
    {
        if (string.IsNullOrWhiteSpace(repositoryIdentity))
            throw new ArgumentException("Repository identity is required.", nameof(repositoryIdentity));
        if (string.IsNullOrWhiteSpace(experimentRunUid))
            throw new ArgumentException("Experiment run UID is required.", nameof(experimentRunUid));
        if (string.IsNullOrWhiteSpace(resumeStableKey))
            throw new ArgumentException("Resume stable key is required.", nameof(resumeStableKey));
        if (resolvedCommit.Length != 40 || !resolvedCommit.All(Uri.IsHexDigit))
            throw new ArgumentException("Resolved commit must be a full 40-character SHA.", nameof(resolvedCommit));

        var material = string.Join("|",
            repositoryIdentity.Trim().ToLowerInvariant(),
            resolvedCommit.Trim().ToLowerInvariant(),
            experimentSeriesId.Trim(),
            experimentRunUid.Trim(),
            producerLane.Trim().ToLowerInvariant(),
            resumeStableKey.Trim(),
            attemptNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}
