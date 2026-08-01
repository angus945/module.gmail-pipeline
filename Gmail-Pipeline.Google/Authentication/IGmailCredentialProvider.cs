using Google.Apis.Auth.OAuth2;

namespace GmailPipeline.Google.Authentication;

public interface IGmailCredentialProvider
{
    Task<ICredential> GetCredentialAsync(CancellationToken cancellationToken = default);
}
