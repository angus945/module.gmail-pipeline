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
    private readonly IGmailServiceAccessor _serviceAccessor;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly string _userId;

    public GoogleGmailAttachmentClient(
        IGmailServiceAccessor serviceAccessor,
        GmailApiRetryPolicy retryPolicy,
        GmailAuthenticationOptions options)
    {
        _serviceAccessor = serviceAccessor;
        _retryPolicy = retryPolicy;
        _userId = options.UserId;
    }

    public async Task<Stream> OpenAttachmentAsync(
        string messageId,
        EmailAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        if (attachment.EmbeddedContent.Length > 0)
        {
            return new MemoryStream(attachment.EmbeddedContent.ToArray(), writable: false);
        }

        if (string.IsNullOrWhiteSpace(attachment.ExternalContentId))
        {
            throw new EmailClientException("Email attachment has neither embedded content nor external provider content id.");
        }

        try
        {
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.Attachments.Get(_userId, messageId, attachment.ExternalContentId);
            var response = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "open attachment", cancellationToken)
                .ConfigureAwait(false);

            return new MemoryStream(Base64UrlDecoder.Decode(response.Data), writable: false);
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "open attachment");
        }
    }
}
