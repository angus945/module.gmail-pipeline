using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Core.Contract.Search;

public sealed record EmailSearchResult(
    IReadOnlyList<EmailReference> Messages,
    string? NextPageToken);
