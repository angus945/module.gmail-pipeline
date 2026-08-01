using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Authentication;

public sealed class GmailAuthenticationOptions
{
    public string ClientSecretPath { get; set; } = "%LOCALAPPDATA%/GmailPipeline/auth/client_secret.json";

    public string TokenDirectory { get; set; } = "%LOCALAPPDATA%/GmailPipeline/auth/tokens";

    public string UserKey { get; set; } = "default";

    public string UserId { get; set; } = "me";

    public IReadOnlyList<string> Scopes { get; set; } =
    [
        GmailService.Scope.GmailReadonly
    ];

    public string ApplicationName { get; set; } = "Gmail Pipeline";
}
