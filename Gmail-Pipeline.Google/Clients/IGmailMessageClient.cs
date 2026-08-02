using GmailPipeline.Core.Search;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Clients;

public interface IGmailMessageClient
{
    Task<ListMessagesResponse> SearchAsync(
        string userId,
        EmailSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Message> GetAsync(
        string userId,
        string messageId,
        UsersResource.MessagesResource.GetRequest.FormatEnum format,
        CancellationToken cancellationToken = default);

    Task<MessagePartBody> GetAttachmentAsync(
        string userId,
        string messageId,
        string attachmentId,
        CancellationToken cancellationToken = default);
}
