using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using MimeKit;

namespace GmailPipeline.Google.Mime;

public sealed class GmailMimeParser
{
    public GmailMimeParseResult Parse(MimeMessage message)
    {
        try
        {
            return new GmailMimeParseResult(
                message.TextBody,
                message.HtmlBody,
                CollectAttachments(message).ToArray());
        }
        catch (Exception exception) when (exception is not EmailClientException)
        {
            throw new EmailContentFormatException("Failed to parse Gmail RAW MIME content.", exception);
        }
    }

    private static IEnumerable<EmailAttachment> CollectAttachments(MimeMessage message)
    {
        foreach (var (entity, path) in Walk(message.Body, "0"))
        {
            if (!ShouldMaterialize(entity))
            {
                continue;
            }

            var content = ReadEntityContent(entity);
            yield return new EmailAttachment
            {
                Id = path,
                EmbeddedContent = content,
                FileName = GetFileName(entity),
                MediaType = entity.ContentType?.MimeType ?? "application/octet-stream",
                ContentId = NormalizeContentId(entity.ContentId),
                Disposition = GetDisposition(entity),
                Size = content.LongLength,
                PartPath = path
            };
        }
    }

    private static IEnumerable<(MimeEntity Entity, string Path)> Walk(MimeEntity? entity, string path)
    {
        if (entity is null)
        {
            yield break;
        }

        yield return (entity, path);

        switch (entity)
        {
            case Multipart multipart:
                for (var index = 0; index < multipart.Count; index++)
                {
                    foreach (var child in Walk(multipart[index], $"{path}.{index}"))
                    {
                        yield return child;
                    }
                }

                break;
            case MessagePart messagePart:
                foreach (var child in Walk(messagePart.Message?.Body, $"{path}.message"))
                {
                    yield return child;
                }

                break;
        }
    }

    private static bool ShouldMaterialize(MimeEntity entity)
    {
        if (entity is Multipart)
        {
            return false;
        }

        var disposition = entity.ContentDisposition?.Disposition;
        return string.Equals(disposition, ContentDisposition.Attachment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(entity.ContentId)
            || !string.IsNullOrWhiteSpace(GetFileName(entity))
            || entity is MessagePart && entity.ContentDisposition is not null;
    }

    private static byte[] ReadEntityContent(MimeEntity entity)
    {
        using var stream = new MemoryStream();
        switch (entity)
        {
            case MimePart mimePart:
                if (mimePart.Content is null)
                {
                    mimePart.WriteTo(stream);
                }
                else
                {
                    mimePart.Content.DecodeTo(stream);
                }

                break;
            case MessagePart messagePart:
                messagePart.Message?.WriteTo(stream);
                break;
            default:
                entity.WriteTo(stream);
                break;
        }

        return stream.ToArray();
    }

    private static string? GetFileName(MimeEntity entity) =>
        entity switch
        {
            MimePart mimePart => FirstNonBlank(mimePart.FileName, entity.ContentDisposition?.FileName, entity.ContentType?.Name),
            MessagePart => FirstNonBlank(entity.ContentDisposition?.FileName, entity.ContentType?.Name),
            _ => FirstNonBlank(entity.ContentDisposition?.FileName, entity.ContentType?.Name)
        };

    private static EmailAttachmentDisposition GetDisposition(MimeEntity entity)
    {
        var disposition = entity.ContentDisposition?.Disposition;
        if (string.Equals(disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(entity.ContentId) && !string.Equals(disposition, ContentDisposition.Attachment, StringComparison.OrdinalIgnoreCase))
        {
            return EmailAttachmentDisposition.Inline;
        }

        return string.Equals(disposition, ContentDisposition.Attachment, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GetFileName(entity))
            ? EmailAttachmentDisposition.Attachment
            : EmailAttachmentDisposition.Unknown;
    }

    private static string? NormalizeContentId(string? contentId) =>
        string.IsNullOrWhiteSpace(contentId)
            ? null
            : contentId.Trim('<', '>');

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
