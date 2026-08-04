namespace GmailPipeline.Core.Contract.Models;

public sealed record EmailBodySection
{
    public required string MediaType { get; init; }

    public required string Content { get; init; }

    public required string PartPath { get; init; }

    public string? ContentId { get; init; }
}
