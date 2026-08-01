namespace GmailPipeline.Core.Search;

public sealed record EmailSearchRequest
{
    public string? Query { get; init; }

    public int PageSize { get; init; } = 100;

    public string? PageToken { get; init; }

    public bool IncludeSpamTrash { get; init; }
}
