using GmailPipeline.Core.Contract.Search;
using GmailPipeline.Google.Application.Ports;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Application.Ports;

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
