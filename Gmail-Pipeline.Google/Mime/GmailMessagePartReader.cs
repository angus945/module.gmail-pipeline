using System.Globalization;
using System.Text;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Google.Clients;
using Google.Apis.Gmail.v1.Data;
using MimeKit;
using GmailMessagePart = Google.Apis.Gmail.v1.Data.MessagePart;

namespace GmailPipeline.Google.Mime;

public sealed class GmailMessagePartReader : IGmailMessagePartReader
{
    private readonly IGmailMessageClient _messageClient;
    private readonly GmailContentLimitsOptions _limits;
    private readonly IEmailCharsetResolver _charsetResolver;
    private readonly string _userId;

    public GmailMessagePartReader(
        IGmailMessageClient messageClient,
        GmailContentLimitsOptions limits,
        IEmailCharsetResolver charsetResolver,
        Authentication.GmailAuthenticationOptions options)
    {
        _messageClient = messageClient;
        _limits = limits;
        _limits.Validate();
        _charsetResolver = charsetResolver;
        _userId = options.UserId;
    }

    public async Task<GmailMimeParseResult> ParseAsync(
        string messageId,
        GmailMessagePart root,
        CancellationToken cancellationToken = default)
    {
        var state = new ParseState();
        await ReadPartAsync(messageId, root, "0", depth: 1, state, cancellationToken).ConfigureAwait(false);
        return new GmailMimeParseResult(state.TextBody, state.HtmlBody, state.Attachments);
    }

    private async Task ReadPartAsync(
        string messageId,
        GmailMessagePart part,
        string path,
        int depth,
        ParseState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.CountPart(path, depth, _limits);

        if (IsExplicitAttachment(part))
        {
            state.AddAttachment(CreateAttachment(part, path, state), _limits);
            return;
        }

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

        if (IsInlineResource(part))
        {
            state.AddAttachment(CreateAttachment(part, path, state), _limits);
            return;
        }

        if (part.Parts is null)
        {
            return;
        }

        for (var index = 0; index < part.Parts.Count; index++)
        {
            await ReadPartAsync(messageId, part.Parts[index], $"{path}.{index}", depth + 1, state, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<string> ReadTextAsync(
        string messageId,
        GmailMessagePart part,
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
            EnsureWithinLimit(resource, ToNullableLong(body.Size), _limits.MaxTextBodyBytes);
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
        var encoding = _charsetResolver.Resolve(GetContentTypeParameter(part, "charset"), resource);
        try
        {
            return encoding.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new EmailContentFormatException($"Failed to decode {resource} using charset '{encoding.WebName}'.", exception);
        }
    }

    private EmailAttachment CreateAttachment(GmailMessagePart part, string path, ParseState state)
    {
        var body = part.Body;
        var size = ToNullableLong(body?.Size);
        var data = body?.Data;
        ReadOnlyMemory<byte>? embeddedContent = null;
        string? providerPartId = null;

        if (data is not null && CanEmbed(data, size))
        {
            state.EnsureCanEmbed($"attachment {path}", GetMaximumDecodedByteCount(data, size), _limits);
            embeddedContent = Base64UrlDecoder.Decode(data, $"attachment {path}", _limits.MaxEmbeddedAttachmentBytes);
            state.RegisterEmbeddedBytes($"attachment {path}", embeddedContent.Value.Length, _limits);
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
        return GetMaximumDecodedByteCount(data, null) <= _limits.MaxEmbeddedAttachmentBytes + 2;
    }

    private static bool IsTextPart(GmailMessagePart part, string mimeType) =>
        string.Equals(part.MimeType, mimeType, StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitAttachment(GmailMessagePart part)
    {
        if (part.Parts is not null && part.Parts.Count > 0 && !IsAttachedMessage(part))
        {
            return false;
        }

        var disposition = GetDispositionToken(part);
        return string.Equals(disposition, "attachment", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(part.Filename)
            || !string.IsNullOrWhiteSpace(GetContentTypeParameter(part, "name"));
    }

    private static bool IsInlineResource(GmailMessagePart part)
    {
        if (part.Parts is not null && part.Parts.Count > 0 && !IsAttachedMessage(part))
        {
            return false;
        }

        var disposition = GetDispositionToken(part);
        return string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GetHeader(part, "Content-ID"));
    }

    private static bool IsAttachedMessage(GmailMessagePart part) =>
        string.Equals(part.MimeType, "message/rfc822", StringComparison.OrdinalIgnoreCase)
        && (!string.IsNullOrWhiteSpace(GetDispositionToken(part))
            || !string.IsNullOrWhiteSpace(part.Filename)
            || !string.IsNullOrWhiteSpace(GetContentTypeParameter(part, "name")));

    private static EmailAttachmentDisposition GetDisposition(GmailMessagePart part)
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

    private static string? GetDispositionToken(GmailMessagePart part)
    {
        var disposition = GetHeader(part, "Content-Disposition");
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return null;
        }

        var separator = disposition.IndexOf(';', StringComparison.Ordinal);
        return (separator < 0 ? disposition : disposition[..separator]).Trim();
    }

    private static string? GetContentTypeParameter(GmailMessagePart part, string parameterName)
    {
        var contentType = GetHeader(part, "Content-Type");
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        try
        {
            var parsed = ContentType.Parse(contentType);
            if (string.Equals(parameterName, "charset", StringComparison.OrdinalIgnoreCase))
            {
                return parsed.Charset;
            }

            return parsed.Parameters[parameterName];
        }
        catch (ParseException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static long GetMaximumDecodedByteCount(string data, long? size)
    {
        if (size is not null)
        {
            return size.Value;
        }

        var padding = (4 - data.Length % 4) % 4;
        return (data.Length + padding) / 4 * 3;
    }

    internal static GmailMessagePart? FindPartByPath(GmailMessagePart root, string path)
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

    private static string? GetHeader(GmailMessagePart part, string name) =>
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

        public long EmbeddedAttachmentBytes { get; private set; }

        public int AttachmentCount { get; private set; }

        public int PartCount { get; private set; }

        public void CountPart(string path, int depth, GmailContentLimitsOptions limits)
        {
            if (depth > limits.MaxMimeDepth)
            {
                throw new EmailResourceLimitException($"MIME depth at {path}", depth, limits.MaxMimeDepth);
            }

            PartCount++;
            if (PartCount > limits.MaxMimePartCount)
            {
                throw new EmailResourceLimitException("MIME part count", PartCount, limits.MaxMimePartCount);
            }
        }

        public void AddAttachment(EmailAttachment attachment, GmailContentLimitsOptions limits)
        {
            AttachmentCount++;
            if (AttachmentCount > limits.MaxAttachmentCount)
            {
                throw new EmailResourceLimitException("attachment count", AttachmentCount, limits.MaxAttachmentCount);
            }

            Attachments.Add(attachment);
        }

        public void EnsureCanEmbed(string resource, long bytes, GmailContentLimitsOptions limits)
        {
            EnsureWithinLimit(resource, bytes, limits.MaxEmbeddedAttachmentBytes);
            EnsureWithinLimit("total embedded attachment bytes", EmbeddedAttachmentBytes + bytes, limits.MaxTotalEmbeddedAttachmentBytes);
        }

        public void RegisterEmbeddedBytes(string resource, long bytes, GmailContentLimitsOptions limits)
        {
            EnsureWithinLimit(resource, bytes, limits.MaxEmbeddedAttachmentBytes);
            EmbeddedAttachmentBytes += bytes;
            EnsureWithinLimit("total embedded attachment bytes", EmbeddedAttachmentBytes, limits.MaxTotalEmbeddedAttachmentBytes);
        }
    }
}
