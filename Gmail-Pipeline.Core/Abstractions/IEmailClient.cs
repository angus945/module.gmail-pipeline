using GmailPipeline.Core.Search;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailClient
{
    Task<EmailSearchResult> SearchAsync(
        EmailSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Models.EmailMessage?> GetAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task AddLabelsAsync(
        string messageId,
        IReadOnlyCollection<string> labelIds,
        CancellationToken cancellationToken = default);

    Task RemoveLabelsAsync(
        string messageId,
        IReadOnlyCollection<string> labelIds,
        CancellationToken cancellationToken = default);
}
