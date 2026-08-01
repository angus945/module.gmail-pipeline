using GmailPipeline.Core.Models;

namespace GmailPipeline.Google.Mime;

public sealed record GmailMimeParseResult(
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<EmailAttachment> Attachments);
