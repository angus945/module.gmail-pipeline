using GmailPipeline.Google.Contract;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace GmailPipeline.Google.Api;

public interface IGmailTokenStoreFactory
{
    IDataStore Create(GmailAuthenticationOptions options, ClientSecrets secrets);
}
