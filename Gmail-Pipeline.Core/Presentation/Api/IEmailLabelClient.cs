using GmailPipeline.Core.Contract.Labels;

namespace GmailPipeline.Core.Api;

public interface IEmailLabelClient
{
    Task<IReadOnlyList<EmailLabel>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<EmailLabel?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<EmailLabel> GetOrCreateAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task ModifyMessageLabelsAsync(
        string messageId,
        EmailLabelModification modification,
        CancellationToken cancellationToken = default);
}
