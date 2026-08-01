namespace GmailPipeline.Core.Models;

public sealed record EmailAttachment
{
    public required string Id { get; init; }

    public string? ProviderAttachmentId { get; init; }

    public string? InlineContentBase64Url { get; init; }

    public required string FileName { get; init; }

    public required string MediaType { get; init; }

    public long? Size { get; init; }

    public required string PartPath { get; init; }

    public bool IsInline => !string.IsNullOrWhiteSpace(InlineContentBase64Url);
}
