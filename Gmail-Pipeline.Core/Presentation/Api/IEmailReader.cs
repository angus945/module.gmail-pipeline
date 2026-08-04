using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Core.Contract.Search;

namespace GmailPipeline.Core.Api;

public interface IEmailReader
{
    Task<EmailSearchResult> SearchAsync(
        EmailSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<EmailMessage?> GetAsync(
        string messageId,
        CancellationToken cancellationToken = default);
}
