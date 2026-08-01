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
    private readonly GmailServiceFactory _serviceFactory;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly string _userId;
    private GmailService? _service;

    public GoogleGmailAttachmentClient(
        GmailServiceFactory serviceFactory,
        GmailApiRetryPolicy retryPolicy)
        : this(serviceFactory, retryPolicy, "me")
    {
    }

    public GoogleGmailAttachmentClient(
        GmailServiceFactory serviceFactory,
        GmailApiRetryPolicy retryPolicy,
        string userId)
    {
        _serviceFactory = serviceFactory;
        _retryPolicy = retryPolicy;
        _userId = userId;
    }

    public async Task<Stream> OpenAttachmentAsync(
        string messageId,
        EmailAttachment attachment,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(attachment.InlineContentBase64Url))
        {
            return new MemoryStream(Base64UrlDecoder.Decode(attachment.InlineContentBase64Url), writable: false);
        }

        if (string.IsNullOrWhiteSpace(attachment.ProviderAttachmentId))
        {
            throw new EmailClientException("Email attachment has neither provider attachment id nor inline MIME data.");
        }

        try
        {
            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.Attachments.Get(_userId, messageId, attachment.ProviderAttachmentId);
            var response = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), cancellationToken)
                .ConfigureAwait(false);

            return new MemoryStream(Base64UrlDecoder.Decode(response.Data), writable: false);
        }
        catch (Exception exception) when (exception is GoogleApiException or HttpRequestException)
        {
            throw GoogleExceptionMapper.Map(exception, "open attachment");
        }
    }

    private async Task<GmailService> GetServiceAsync(CancellationToken cancellationToken)
    {
        _service ??= await _serviceFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return _service;
    }
}
