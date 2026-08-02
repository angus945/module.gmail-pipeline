using GmailPipeline.Core.Search;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailReader
{
    Task<EmailSearchResult> SearchAsync(
        EmailSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Models.EmailMessage?> GetAsync(
        string messageId,
        CancellationToken cancellationToken = default);
}
