using System.Globalization;
using System.Text;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Google.Clients;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Mime;

public sealed class GmailMessagePartReader : IGmailMessagePartReader
{
    private readonly IGmailMessageClient _messageClient;
    private readonly GmailContentLimitsOptions _limits;
    private readonly string _userId;

    public GmailMessagePartReader(
        IGmailMessageClient messageClient,
        GmailContentLimitsOptions limits,
        Authentication.GmailAuthenticationOptions options)
    {
        _messageClient = messageClient;
        _limits = limits;
        _userId = options.UserId;
    }

    public async Task<GmailMimeParseResult> ParseAsync(
        string messageId,
        MessagePart root,
        CancellationToken cancellationToken = default)
    {
        var state = new ParseState();
        await ReadPartAsync(messageId, root, "0", state, cancellationToken).ConfigureAwait(false);
        return new GmailMimeParseResult(state.TextBody, state.HtmlBody, state.Attachments);
    }

    private async Task ReadPartAsync(
        string messageId,
        MessagePart part,
        string path,
        ParseState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsTextPart(part, "text/plain"))
        {
            state.TextBody ??= await ReadTextAsync(messageId, part, path, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsTextPart(part, "text/html"))
        {
            state.HtmlBody ??= await ReadTextAsync(messageId, part, path, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (ShouldMaterializeAttachment(part))
        {
            state.Attachments.Add(CreateAttachment(part, path));
            return;
        }

        if (part.Parts is null)
        {
            return;
        }

        for (var index = 0; index < part.Parts.Count; index++)
        {
            await ReadPartAsync(messageId, part.Parts[index], $"{path}.{index}", state, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> ReadTextAsync(
        string messageId,
        MessagePart part,
        string path,
        CancellationToken cancellationToken)
    {
        var body = part.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var data = body.Data;
        var resource = $"{part.MimeType ?? "text"} body at {path}";
        if (data is null && !string.IsNullOrWhiteSpace(body.AttachmentId))
        {
            var response = await _messageClient
                .GetAttachmentAsync(_userId, messageId, body.AttachmentId, cancellationToken)
                .ConfigureAwait(false);
            data = response.Data ?? string.Empty;
            EnsureWithinLimit(resource, ToNullableLong(response.Size), _limits.MaxTextBodyBytes);
        }
        else
        {
            EnsureWithinLimit(resource, ToNullableLong(body.Size), _limits.MaxTextBodyBytes);
        }

        var bytes = Base64UrlDecoder.Decode(data ?? string.Empty, resource, _limits.MaxTextBodyBytes);
        return GetEncoding(part).GetString(bytes);
    }

    private EmailAttachment CreateAttachment(MessagePart part, string path)
    {
        var body = part.Body;
        var size = ToNullableLong(body?.Size);
        var data = body?.Data;
        ReadOnlyMemory<byte>? embeddedContent = null;
        string? providerPartId = null;

        if (data is not null && CanEmbed(data, size))
        {
            embeddedContent = Base64UrlDecoder.Decode(data, $"attachment {path}", _limits.MaxEmbeddedAttachmentBytes);
        }
        else if (data is not null)
        {
            providerPartId = path;
        }
        else if (string.IsNullOrWhiteSpace(body?.AttachmentId) && size == 0)
        {
            embeddedContent = ReadOnlyMemory<byte>.Empty;
        }

        return new EmailAttachment
        {
            Id = path,
            ExternalContentId = string.IsNullOrWhiteSpace(body?.AttachmentId) ? null : body.AttachmentId,
            ProviderPartId = providerPartId,
            EmbeddedContent = embeddedContent,
            FileName = FirstNonBlank(part.Filename, GetContentTypeParameter(part, "name")),
            MediaType = part.MimeType ?? "application/octet-stream",
            ContentId = NormalizeContentId(GetHeader(part, "Content-ID")),
            Disposition = GetDisposition(part),
            Size = size,
            PartPath = path
        };
    }

    private bool CanEmbed(string data, long? size)
    {
        if (size is not null)
        {
            return size.Value <= _limits.MaxEmbeddedAttachmentBytes;
        }

        var padding = (4 - data.Length % 4) % 4;
        var maxDecodedBytes = (data.Length + padding) / 4 * 3;
        return maxDecodedBytes <= _limits.MaxEmbeddedAttachmentBytes + 2;
    }

    private static bool IsTextPart(MessagePart part, string mimeType) =>
        string.Equals(part.MimeType, mimeType, StringComparison.OrdinalIgnoreCase);

    private static bool ShouldMaterializeAttachment(MessagePart part)
    {
        if (part.Parts is not null && part.Parts.Count > 0 && !IsAttachedMessage(part))
        {
            return false;
        }

        var disposition = GetDispositionToken(part);
        return string.Equals(disposition, "attachment", StringComparison.OrdinalIgnoreCase)
            || string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(part.Filename)
            || !string.IsNullOrWhiteSpace(GetContentTypeParameter(part, "name"))
            || !string.IsNullOrWhiteSpace(GetHeader(part, "Content-ID"));
    }

    private static bool IsAttachedMessage(MessagePart part) =>
        string.Equals(part.MimeType, "message/rfc822", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(GetDispositionToken(part));

    private static EmailAttachmentDisposition GetDisposition(MessagePart part)
    {
        var disposition = GetDispositionToken(part);
        if (string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GetHeader(part, "Content-ID")) && !string.Equals(disposition, "attachment", StringComparison.OrdinalIgnoreCase))
        {
            return EmailAttachmentDisposition.Inline;
        }

        return string.Equals(disposition, "attachment", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(part.Filename)
            || !string.IsNullOrWhiteSpace(GetContentTypeParameter(part, "name"))
            ? EmailAttachmentDisposition.Attachment
            : EmailAttachmentDisposition.Unknown;
    }

    private static Encoding GetEncoding(MessagePart part)
    {
        var charset = GetContentTypeParameter(part, "charset");
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static string? GetDispositionToken(MessagePart part)
    {
        var disposition = GetHeader(part, "Content-Disposition");
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return null;
        }

        var separator = disposition.IndexOf(';', StringComparison.Ordinal);
        return (separator < 0 ? disposition : disposition[..separator]).Trim();
    }

    private static string? GetContentTypeParameter(MessagePart part, string parameterName)
    {
        var contentType = GetHeader(part, "Content-Type");
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        foreach (var segment in contentType.Split(';').Skip(1))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0].Trim(), parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].Trim().Trim('"');
            }
        }

        return null;
    }

    internal static MessagePart? FindPartByPath(MessagePart root, string path)
    {
        var current = root;
        var segments = path.Split('.');
        if (segments.Length == 0 || segments[0] != "0")
        {
            return null;
        }

        foreach (var segment in segments.Skip(1))
        {
            if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || current.Parts is null
                || index < 0
                || index >= current.Parts.Count)
            {
                return null;
            }

            current = current.Parts[index];
        }

        return current;
    }

    private static void EnsureWithinLimit(string resource, long? actualBytes, long allowedBytes)
    {
        if (actualBytes is not null && actualBytes.Value > allowedBytes)
        {
            throw new EmailResourceLimitException(resource, actualBytes.Value, allowedBytes);
        }
    }

    private static long? ToNullableLong(long? value) => value;

    private static string? GetHeader(MessagePart part, string name) =>
        part.Headers?
            .FirstOrDefault(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static string? NormalizeContentId(string? contentId) =>
        string.IsNullOrWhiteSpace(contentId)
            ? null
            : contentId.Trim('<', '>');

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private sealed class ParseState
    {
        public string? TextBody { get; set; }

        public string? HtmlBody { get; set; }

        public List<EmailAttachment> Attachments { get; } = [];
    }
}
