using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Exceptions;
using GmailPipeline.Google.Mime;
using Google;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailAttachmentClient : IEmailAttachmentClient
{
    private readonly IGmailMessageClient _messageClient;
    private readonly GmailContentLimitsOptions _limits;
    private readonly string _userId;

    public GoogleGmailAttachmentClient(
        IGmailMessageClient messageClient,
        GmailContentLimitsOptions limits,
        GmailAuthenticationOptions options)
    {
        _messageClient = messageClient;
        _limits = limits;
        _limits.Validate();
        _userId = options.UserId;
    }

    public async Task<Stream> OpenAttachmentAsync(
        string messageId,
        EmailAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        if (attachment.Kind is EmailAttachmentKind.Composite or EmailAttachmentKind.EncapsulatedMessage
            && attachment.EmbeddedContent is null
            && string.IsNullOrWhiteSpace(attachment.ExternalContentId)
            && string.IsNullOrWhiteSpace(attachment.ProviderPartId))
        {
            throw new EmailCompositeAttachmentException(attachment);
        }

        EnsureWithinOpenedLimit($"attachment {attachment.Id}", attachment.Size ?? attachment.EmbeddedContent?.Length);

        if (attachment.EmbeddedContent is { } embeddedContent)
        {
            return new ReadOnlyMemoryStream(embeddedContent);
        }

        if (!string.IsNullOrWhiteSpace(attachment.ExternalContentId))
        {
            return await OpenExternalAttachmentAsync(messageId, attachment.ExternalContentId, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(attachment.ProviderPartId))
        {
            return await OpenProviderPartAsync(messageId, attachment.ProviderPartId, cancellationToken).ConfigureAwait(false);
        }

        throw new EmailClientException("Email attachment has no embedded content, external provider content id, or provider part id.");
    }

    public async Task CopyAttachmentToAsync(
        string messageId,
        EmailAttachment attachment,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        await using var source = await OpenAttachmentAsync(messageId, attachment, cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Stream> OpenExternalAttachmentAsync(
        string messageId,
        string externalContentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _messageClient
                .GetAttachmentAsync(_userId, messageId, externalContentId, cancellationToken)
                .ConfigureAwait(false);
            EnsureWithinOpenedLimit("external attachment", response.Size);

            return new MemoryStream(
                Base64UrlDecoder.Decode(response.Data ?? string.Empty, "external attachment", _limits.MaxOpenedAttachmentBytes),
                writable: false);
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "open attachment");
        }
    }

    private async Task<Stream> OpenProviderPartAsync(
        string messageId,
        string providerPartId,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await _messageClient
                .GetAsync(_userId, messageId, UsersResource.MessagesResource.GetRequest.FormatEnum.Full, cancellationToken)
                .ConfigureAwait(false);
            if (message.Payload is null)
            {
                throw new EmailContentFormatException("Gmail returned a FULL message without a MIME payload.");
            }

            var part = GmailMessagePartReader.FindPartByProviderId(message.Payload, providerPartId)
                ?? throw new EmailContentFormatException($"Gmail MIME part '{providerPartId}' was not found.");
            if (!string.IsNullOrWhiteSpace(part.Body?.AttachmentId))
            {
                return await OpenExternalAttachmentAsync(messageId, part.Body.AttachmentId, cancellationToken).ConfigureAwait(false);
            }

            EnsureWithinOpenedLimit($"attachment part {providerPartId}", part.Body?.Size);
            return new MemoryStream(
                Base64UrlDecoder.Decode(part.Body?.Data ?? string.Empty, $"attachment part {providerPartId}", _limits.MaxOpenedAttachmentBytes),
                writable: false);
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "open attachment part");
        }
    }

    private void EnsureWithinOpenedLimit(string resource, long? actualBytes)
    {
        if (actualBytes is not null && actualBytes.Value > _limits.MaxOpenedAttachmentBytes)
        {
            throw new EmailResourceLimitException(resource, actualBytes.Value, _limits.MaxOpenedAttachmentBytes);
        }
    }
}
