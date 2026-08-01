using Google.Apis.Services;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Authentication;

public sealed class GmailServiceFactory
{
    private readonly IGmailCredentialProvider _credentialProvider;
    private readonly GmailAuthenticationOptions _options;

    public GmailServiceFactory(
        IGmailCredentialProvider credentialProvider,
        GmailAuthenticationOptions options)
    {
        _credentialProvider = credentialProvider;
        _options = options;
    }

    public async Task<GmailService> CreateAsync(CancellationToken cancellationToken = default)
    {
        var credential = await _credentialProvider.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _options.ApplicationName
        });
    }
}
