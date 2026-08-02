using Google.Apis.Services;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Authentication;

public interface IGmailServiceFactory
{
    Task<GmailService> CreateAsync(CancellationToken cancellationToken = default);
}

public sealed class GmailServiceFactory : IGmailServiceFactory
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
