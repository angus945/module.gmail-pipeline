using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Google.Infrastructure.Mime;

public sealed record GmailMimeParseResult
{
    public GmailMimeParseResult(
        string? textBody,
        string? htmlBody,
        IReadOnlyList<EmailAttachment> attachments)
        : this(textBody, htmlBody, attachments, [])
    {
    }

    public GmailMimeParseResult(
        string? textBody,
        string? htmlBody,
        IReadOnlyList<EmailAttachment> attachments,
        IReadOnlyList<EmailBodySection> bodySections)
    {
        TextBody = textBody;
        HtmlBody = htmlBody;
        Attachments = attachments;
        BodySections = bodySections;
    }

    public string? TextBody { get; }

    public string? HtmlBody { get; }

    public IReadOnlyList<EmailAttachment> Attachments { get; }

    public IReadOnlyList<EmailBodySection> BodySections { get; }
}
