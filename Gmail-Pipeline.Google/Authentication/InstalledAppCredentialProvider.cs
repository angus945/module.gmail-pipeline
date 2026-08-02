using GmailPipeline.Core.Exceptions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace GmailPipeline.Google.Authentication;

public sealed class InstalledAppCredentialProvider : IGmailCredentialProvider
{
    private readonly GmailAuthenticationOptions _options;
    private readonly IGmailTokenStoreFactory _tokenStoreFactory;

    public InstalledAppCredentialProvider(
        GmailAuthenticationOptions options,
        IGmailTokenStoreFactory tokenStoreFactory)
    {
        _options = options;
        _tokenStoreFactory = tokenStoreFactory;
    }

    public async Task<ICredential> GetCredentialAsync(CancellationToken cancellationToken = default)
    {
        var clientSecretPath = ExpandPath(_options.ClientSecretPath);
        ClientSecrets secrets;
        IDataStore tokenStore;
        try
        {
            await using var stream = File.OpenRead(clientSecretPath);
            secrets = GoogleClientSecrets.FromStream(stream).Secrets;
            tokenStore = _tokenStoreFactory.Create(_options, secrets);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not EmailClientException)
        {
            throw new EmailCredentialStoreException("Failed to read Gmail OAuth client secret or initialize the token store.", exception);
        }

        return await GoogleWebAuthorizationBroker
            .AuthorizeAsync(
                secrets,
                _options.Scopes,
                _options.UserKey,
                cancellationToken,
                tokenStore)
            .ConfigureAwait(false);
    }

    private static string ExpandPath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Replace('/', Path.DirectorySeparatorChar)));
}
