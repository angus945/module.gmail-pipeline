using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Search;

public sealed record EmailSearchResult(
    IReadOnlyList<EmailReference> Messages,
    string? NextPageToken);
