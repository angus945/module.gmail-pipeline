using GmailPipeline.Core.Abstractions;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Clients;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GmailPipeline.Google.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGmailPipelineGoogleReadOnly(
        this IServiceCollection services,
        Action<GmailAuthenticationOptions> configure)
    {
        AddCommonServices(services, configure, GmailService.Scope.GmailReadonly);
        services.TryAddSingleton<IEmailReader, GoogleGmailReader>();
        services.TryAddSingleton<IEmailAttachmentClient, GoogleGmailAttachmentClient>();
        return services;
    }

    public static IServiceCollection AddGmailPipelineGoogleModify(
        this IServiceCollection services,
        Action<GmailAuthenticationOptions> configure)
    {
        AddCommonServices(services, configure, GmailService.Scope.GmailModify);
        services.TryAddSingleton<IEmailReader, GoogleGmailReader>();
        services.TryAddSingleton<IEmailAttachmentClient, GoogleGmailAttachmentClient>();
        services.TryAddSingleton<GmailLabelCache>();
        services.TryAddSingleton<IEmailLabelClient, GoogleGmailLabelClient>();
        return services;
    }

    public static IServiceCollection AddGmailPipelineGoogleReadOnlyWithCredentialProvider<TProvider>(
        this IServiceCollection services,
        Action<GmailAuthenticationOptions> configure)
        where TProvider : class, IGmailCredentialProvider
    {
        services.AddSingleton<IGmailCredentialProvider, TProvider>();
        return services.AddGmailPipelineGoogleReadOnly(configure);
    }

    public static IServiceCollection AddGmailPipelineGoogleModifyWithCredentialProvider<TProvider>(
        this IServiceCollection services,
        Action<GmailAuthenticationOptions> configure)
        where TProvider : class, IGmailCredentialProvider
    {
        services.AddSingleton<IGmailCredentialProvider, TProvider>();
        return services.AddGmailPipelineGoogleModify(configure);
    }

    private static void AddCommonServices(
        IServiceCollection services,
        Action<GmailAuthenticationOptions> configure,
        string scope)
    {
        var options = new GmailAuthenticationOptions
        {
            ClientSecretPath = "%LOCALAPPDATA%/GmailPipeline/auth/client_secret.json",
            TokenDirectory = "%LOCALAPPDATA%/GmailPipeline/auth/tokens"
        };
        configure(options);
        options.Scopes = [scope];

        services.AddSingleton(options);
        services.TryAddSingleton<IGmailTokenStoreFactory, DefaultGmailTokenStoreFactory>();
        services.TryAddSingleton<IGmailCredentialProvider, InstalledAppCredentialProvider>();
        services.TryAddSingleton<IGmailServiceFactory, GmailServiceFactory>();
        services.TryAddSingleton<IGmailServiceAccessor, GmailServiceAccessor>();
        services.TryAddSingleton<IGmailRetryDelay, TaskDelayGmailRetryDelay>();
        services.TryAddSingleton<GmailApiRetryPolicy>();
        services.TryAddSingleton<GmailContentLimitsOptions>();
        services.TryAddSingleton<IGmailMessageClient, GoogleGmailMessageClient>();
        services.TryAddSingleton<IGmailMessagePartReader, GmailMessagePartReader>();
        services.TryAddSingleton<GmailMimeParser>();
        services.TryAddSingleton<IGmailMessageMapper, GmailMessageMapper>();
    }
}
