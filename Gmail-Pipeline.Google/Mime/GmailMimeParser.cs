using System.Globalization;
using GmailPipeline.Core.Models;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Mime;

public sealed class GmailMimeParser
{
    public GmailMimeParseResult Parse(Message message)
    {
        if (message.Payload is null)
        {
            return new GmailMimeParseResult(null, null, []);
        }

        var attachments = new List<EmailAttachment>();
        string? textBody = null;
        string? htmlBody = null;

        foreach (var (part, path) in GmailMessagePartWalker.Walk(message.Payload))
        {
            var fileName = part.Filename ?? string.Empty;
            var attachmentId = part.Body?.AttachmentId;
            var inlineData = part.Body?.Data;
            var hasFileName = !string.IsNullOrWhiteSpace(fileName);
            var hasProviderAttachment = hasFileName && !string.IsNullOrWhiteSpace(attachmentId);
            var hasInlineAttachment = hasFileName && !string.IsNullOrWhiteSpace(inlineData);

            if (hasProviderAttachment || hasInlineAttachment)
            {
                attachments.Add(new EmailAttachment
                {
                    Id = path,
                    ProviderAttachmentId = attachmentId,
                    InlineContentBase64Url = hasProviderAttachment ? null : inlineData,
                    FileName = fileName,
                    MediaType = part.MimeType ?? "application/octet-stream",
                    Size = part.Body?.Size is null ? null : Convert.ToInt64(part.Body.Size, CultureInfo.InvariantCulture),
                    PartPath = path
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(inlineData))
            {
                continue;
            }

            if (string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                textBody ??= Base64UrlDecoder.DecodeUtf8(inlineData);
            }
            else if (string.Equals(part.MimeType, "text/html", StringComparison.OrdinalIgnoreCase))
            {
                htmlBody ??= Base64UrlDecoder.DecodeUtf8(inlineData);
            }
        }

        return new GmailMimeParseResult(textBody, htmlBody, attachments);
    }
}
