namespace TestMap.Services.Testing.Providers;

using Google.Apis.Auth.OAuth2;
using System.Threading;

public class VertexAiTokenProvider
{
    private readonly GoogleCredential _credential;

    public VertexAiTokenProvider(string serviceAccountPath)
    {
        _credential = GoogleCredential
            .FromFile(serviceAccountPath)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
    }

    public async ValueTask<string> GetTokenAsync()
    {
        return await _credential
            .UnderlyingCredential
            .GetAccessTokenForRequestAsync();
    }
}