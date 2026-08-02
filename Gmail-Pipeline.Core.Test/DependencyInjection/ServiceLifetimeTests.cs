using FluentAssertions;
using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.DependencyInjection;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Core.Test.DependencyInjection;

public sealed class ServiceLifetimeTests
{
    [Fact]
    public void EmailPipelineDefaultsToTransientLifetime()
    {
        var services = new ServiceCollection();
        services.AddEmailPipeline<string>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEmailPipeline<string>>()
            .Should()
            .NotBeSameAs(provider.GetRequiredService<IEmailPipeline<string>>());
    }

    [Fact]
    public async Task ScopedParserCanBeResolvedWithScopeValidation()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddEmailParser<string, ScopedParser>(ServiceLifetime.Scoped);
        services.AddEmailPipeline<string>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        var pipeline = scope.ServiceProvider.GetRequiredService<IEmailPipeline<string>>();
        var result = await pipeline.ProcessAsync(new EmailMessage
        {
            Id = "message",
            ThreadId = "thread"
        });

        result.ParseResult.Value.Should().Be("scoped");
    }

    private sealed class ScopedDependency
    {
    }

    private sealed class ScopedParser : IEmailParser<string>
    {
        public ScopedParser(ScopedDependency dependency)
        {
            _ = dependency;
        }

        public string Name => "scoped";

        public int Priority => 0;

        public bool CanParse(EmailMessage message) => true;

        public Task<EmailParseResult<string>> ParseAsync(
            EmailMessage message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(EmailParseResult<string>.Succeeded("scoped"));
    }
}
