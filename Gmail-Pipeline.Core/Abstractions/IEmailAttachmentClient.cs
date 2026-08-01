using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailAttachmentClient
{
    Task<Stream> OpenAttachmentAsync(
        string messageId,
        EmailAttachment attachment,
        CancellationToken cancellationToken = default);
}
