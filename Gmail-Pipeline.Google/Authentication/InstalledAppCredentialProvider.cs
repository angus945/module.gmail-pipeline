using Google.Apis.Auth.OAuth2;

namespace GmailPipeline.Google.Authentication;

public sealed class InstalledAppCredentialProvider : IGmailCredentialProvider
{
    private readonly GmailAuthenticationOptions _options;

    public InstalledAppCredentialProvider(GmailAuthenticationOptions options)
    {
        _options = options;
    }

    public async Task<ICredential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        var clientSecretPath = ExpandPath(_options.ClientSecretPath);
        var tokenDirectory = ExpandPath(_options.TokenDirectory);

        await using var stream = File.OpenRead(clientSecretPath);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

        return await GoogleWebAuthorizationBroker
            .AuthorizeAsync(
                secrets,
                _options.Scopes,
                _options.UserKey,
                cancellationToken,
                new ProtectedFileDataStore(tokenDirectory))
            .ConfigureAwait(false);
    }

    private static string ExpandPath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Replace('/', Path.DirectorySeparatorChar)));
}
