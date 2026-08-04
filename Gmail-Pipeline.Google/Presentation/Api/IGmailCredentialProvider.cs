using Google.Apis.Auth.OAuth2;

namespace GmailPipeline.Google.Api;

public interface IGmailCredentialProvider
{
    Task<ICredential> GetCredentialAsync(CancellationToken cancellationToken = default);
}
