namespace GmailPipeline.Core.Models;

public sealed record EmailMessage
{
    public required string Id { get; init; }

    public required string ThreadId { get; init; }

    public string? Subject { get; init; }

    public EmailAddress? From { get; init; }

    public IReadOnlyList<EmailAddress> To { get; init; } = [];

    public IReadOnlyList<EmailAddress> Cc { get; init; } = [];

    public IReadOnlyList<EmailAddress> Bcc { get; init; } = [];

    public DateTimeOffset? SentAt { get; init; }

    public DateTimeOffset? ReceivedAt { get; init; }

    public string? TextBody { get; init; }

    public string? HtmlBody { get; init; }

    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];

    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> LabelIds { get; init; } = [];
}
