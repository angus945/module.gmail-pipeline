namespace GmailPipeline.Core.Contract.Models;

public sealed record EmailAttachment
{
    public required string Id { get; init; }

    public string? ExternalContentId { get; init; }

    public string? ProviderPartId { get; init; }

    public ReadOnlyMemory<byte>? EmbeddedContent { get; init; }

    public EmailAttachmentKind Kind { get; init; } = EmailAttachmentKind.Binary;

    public string? FileName { get; init; }

    public required string MediaType { get; init; }

    public string? ContentId { get; init; }

    public EmailAttachmentDisposition Disposition { get; init; } = EmailAttachmentDisposition.Unknown;

    public long? Size { get; init; }

    public required string PartPath { get; init; }

    public IReadOnlyList<EmailBodySection> BodySections { get; init; } = [];

    public IReadOnlyList<EmailAttachment> Children { get; init; } = [];

    public bool HasEmbeddedContent => EmbeddedContent.HasValue;

    public bool IsInline => Disposition == EmailAttachmentDisposition.Inline
        || Kind == EmailAttachmentKind.InlineResource;
}
