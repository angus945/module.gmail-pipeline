using GmailPipeline.Core.Api;
using GmailPipeline.Core.Contract.Labels;
using GmailPipeline.Core.Contract.Search;
using GmailPipeline.Google.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Google.Test.Integration;

public sealed class GmailApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchGetAndModifyLabelSmokeTest()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("GMAIL_PIPELINE_RUN_INTEGRATION"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var clientSecretPath = RequireEnvironment("GMAIL_PIPELINE_CLIENT_SECRET_PATH");
        var tokenDirectory = Environment.GetEnvironmentVariable("GMAIL_PIPELINE_TOKEN_DIRECTORY")
            ?? Path.Combine(Path.GetTempPath(), "gmail-pipeline-integration-tokens");
        var query = Environment.GetEnvironmentVariable("GMAIL_PIPELINE_QUERY") ?? "newer_than:30d";
        var labelName = Environment.GetEnvironmentVariable("GMAIL_PIPELINE_LABEL") ?? "GmailPipelineIntegration";
        var services = new ServiceCollection();
        services.AddGmailPipelineGoogleModify(options =>
        {
            options.ClientSecretPath = clientSecretPath;
            options.TokenDirectory = tokenDirectory;
            options.UserKey = "integration";
        });
        using var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<IEmailReader>();
        var labels = provider.GetRequiredService<IEmailLabelClient>();

        var page = await reader.SearchAsync(new EmailSearchRequest
        {
            Query = query,
            PageSize = 1
        });
        var reference = page.Messages.FirstOrDefault();
        if (reference is null)
        {
            return;
        }

        var message = await reader.GetAsync(reference.Id);
        Assert.NotNull(message);
        var label = await labels.GetOrCreateAsync(labelName);
        await labels.ModifyMessageLabelsAsync(reference.Id, new EmailLabelModification([label.Id], []));
        await labels.ModifyMessageLabelsAsync(reference.Id, new EmailLabelModification([], [label.Id]));
    }

    private static string RequireEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} must be set when GMAIL_PIPELINE_RUN_INTEGRATION=true.");
}
