using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace GmailPipeline.Google.Authentication;

public interface IGmailTokenStoreFactory
{
    IDataStore Create(GmailAuthenticationOptions options, ClientSecrets secrets);
}
