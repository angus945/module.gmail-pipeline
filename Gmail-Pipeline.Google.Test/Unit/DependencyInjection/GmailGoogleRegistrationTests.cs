using FluentAssertions;
using GmailPipeline.Core.Api;
using GmailPipeline.Google.Api;
using GmailPipeline.Google.Contract;
using Google.Apis.Gmail.v1;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Google.Test.Unit.DependencyInjection;

public sealed class GmailGoogleRegistrationTests
{
    [Fact]
    public void ReadOnlyRegistrationExposesOnlyReadCapabilitiesAndReadonlyScope()
    {
        var services = new ServiceCollection();

        services.AddGmailPipelineGoogleReadOnly(options =>
        {
            options.ClientSecretPath = "client.json";
            options.TokenDirectory = "tokens";
            options.Scopes = ["custom-scope"];
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetService<IEmailReader>().Should().NotBeNull();
        provider.GetService<IEmailAttachmentClient>().Should().NotBeNull();
        provider.GetService<IEmailLabelClient>().Should().BeNull();
        provider.GetRequiredService<GmailAuthenticationOptions>().Scopes.Should().Equal(GmailService.Scope.GmailReadonly);
        services.Any(IsLegacyMimeParserRegistration).Should().BeFalse();
    }

    [Fact]
    public void ModifyRegistrationExposesLabelClientAndModifyScope()
    {
        var services = new ServiceCollection();

        services.AddGmailPipelineGoogleModify(options =>
        {
            options.ClientSecretPath = "client.json";
            options.TokenDirectory = "tokens";
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.GetService<IEmailReader>().Should().NotBeNull();
        provider.GetService<IEmailAttachmentClient>().Should().NotBeNull();
        provider.GetService<IEmailLabelClient>().Should().NotBeNull();
        provider.GetRequiredService<GmailAuthenticationOptions>().Scopes.Should().Equal(GmailService.Scope.GmailModify);
    }

    private static bool IsLegacyMimeParserRegistration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType.FullName == "GmailPipeline.Google.Infrastructure.Mime.GmailMimeParser"
        || descriptor.ImplementationType is { FullName: "GmailPipeline.Google.Infrastructure.Mime.GmailMimeParser" };
}
