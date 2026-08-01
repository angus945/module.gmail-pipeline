using GmailPipeline.Core.Abstractions;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Clients;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Google.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGmailPipelineGoogle(
        this IServiceCollection services,
        Action<GmailAuthenticationOptions> configure)
    {
        var options = new GmailAuthenticationOptions
        {
            ClientSecretPath = "%LOCALAPPDATA%/GmailPipeline/auth/client_secret.json",
            TokenDirectory = "%LOCALAPPDATA%/GmailPipeline/auth/tokens"
        };
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IGmailCredentialProvider, InstalledAppCredentialProvider>();
        services.AddSingleton<GmailServiceFactory>();
        services.AddSingleton<GmailApiRetryPolicy>();
        services.AddSingleton<GmailMimeParser>();
        services.AddSingleton<IGmailMessageMapper, GmailMessageMapper>();
        services.AddSingleton<IEmailClient, GoogleGmailClient>();
        services.AddSingleton<IEmailAttachmentClient, GoogleGmailAttachmentClient>();

        return services;
    }
}
