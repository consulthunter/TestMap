using Google.Apis.Auth.OAuth2;
using TestMap.Models.Configuration.AiProviders.Google;

namespace TestMap.Services.TestGeneration.Providers.Google;

public class VertexAiTokenProvider
{
    private const string CloudPlatformScope = "https://www.googleapis.com/auth/cloud-platform";
    private const string AccessTokenEnvironmentVariable = "GOOGLE_CLOUD_ACCESS_TOKEN";
    private const string ApplicationCredentialsEnvironmentVariable = "GOOGLE_APPLICATION_CREDENTIALS";

    public async Task<string> GetTokenAsync(
        GoogleCloudConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(config.AccessToken)) return config.AccessToken;

        var accessToken = Environment.GetEnvironmentVariable(AccessTokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(accessToken)) return accessToken;

        GoogleCredential credential;

        if (!string.IsNullOrWhiteSpace(config.TokenPath))
        {
            credential = await CredentialFactory
                .FromFileAsync(config.TokenPath, null, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var applicationCredentials = Environment.GetEnvironmentVariable(ApplicationCredentialsEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(applicationCredentials) && File.Exists(applicationCredentials))
                credential = await CredentialFactory
                    .FromFileAsync(applicationCredentials, null, cancellationToken)
                    .ConfigureAwait(false);
            else
                credential = await GoogleCredential
                    .GetApplicationDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
        }

        credential = credential.CreateScoped(CloudPlatformScope);
        return await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
            cancellationToken: cancellationToken);
    }
}