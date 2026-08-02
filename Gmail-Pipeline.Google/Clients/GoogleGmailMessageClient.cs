using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailMessageClient : IGmailMessageClient
{
    private readonly IGmailServiceAccessor _serviceAccessor;
    private readonly GmailApiRetryPolicy _retryPolicy;

    public GoogleGmailMessageClient(
        IGmailServiceAccessor serviceAccessor,
        GmailApiRetryPolicy retryPolicy)
    {
        _serviceAccessor = serviceAccessor;
        _retryPolicy = retryPolicy;
    }

    public async Task<ListMessagesResponse> SearchAsync(
        string userId,
        EmailSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
        var gmailRequest = service.Users.Messages.List(userId);
        gmailRequest.Q = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query;
        gmailRequest.MaxResults = Math.Clamp(request.PageSize, 1, 500);
        gmailRequest.PageToken = request.PageToken;
        gmailRequest.IncludeSpamTrash = request.IncludeSpamTrash;

        return await _retryPolicy
            .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "search messages", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Message> GetAsync(
        string userId,
        string messageId,
        UsersResource.MessagesResource.GetRequest.FormatEnum format,
        CancellationToken cancellationToken = default)
    {
        var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
        var gmailRequest = service.Users.Messages.Get(userId, messageId);
        gmailRequest.Format = format;
        return await _retryPolicy
            .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), $"get {format} message", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MessagePartBody> GetAttachmentAsync(
        string userId,
        string messageId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
        var gmailRequest = service.Users.Messages.Attachments.Get(userId, messageId, attachmentId);
        return await _retryPolicy
            .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "get attachment", cancellationToken)
            .ConfigureAwait(false);
    }
}
