using System.Security.Cryptography;
using System.Text;

namespace TestMap.Services.Experiment.Reporting;

public static class AttemptKeyFactory
{
    public static string Create(
        string repositoryIdentity,
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

        var material = string.Join("|",
            repositoryIdentity.Trim().ToLowerInvariant(),
            experimentSeriesId.Trim(),
            experimentRunUid.Trim(),
            producerLane.Trim().ToLowerInvariant(),
            resumeStableKey.Trim(),
            attemptNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }
}
