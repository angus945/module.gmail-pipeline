using System.Security.Cryptography;
using System.Text;
using GmailPipeline.Core.Contract.Exceptions;
using GmailPipeline.Google.Api;
using GmailPipeline.Google.Contract;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace GmailPipeline.Google.Infrastructure.Authentication;

public sealed class DefaultGmailTokenStoreFactory : IGmailTokenStoreFactory
{
    public IDataStore Create(GmailAuthenticationOptions options, ClientSecrets secrets)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new EmailCredentialStoreException(
                "The default Gmail token store uses Windows DPAPI. Register a custom IGmailTokenStoreFactory for non-Windows hosts.");
        }

        var tokenDirectory = ExpandPath(options.TokenDirectory);
        var clientId = string.IsNullOrWhiteSpace(secrets.ClientId) ? "unknown-client" : secrets.ClientId;
        var scopeFingerprint = CreateScopeFingerprint(options.Scopes);
        var keyNamespace = $"gmail-pipeline:v2:{clientId}:{options.UserKey}:{scopeFingerprint}";
        return new ProtectedFileDataStore(tokenDirectory, keyNamespace);
    }

    private static string CreateScopeFingerprint(IEnumerable<string> scopes)
    {
        var canonicalScopes = string.Join("\n", scopes.Order(StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalScopes))).ToLowerInvariant();
    }

    private static string ExpandPath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Replace('/', Path.DirectorySeparatorChar)));
}
