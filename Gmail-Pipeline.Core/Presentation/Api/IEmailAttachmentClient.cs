using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Core.Api;

public interface IEmailAttachmentClient
{
    Task<Stream> OpenAttachmentAsync(
        string messageId,
        EmailAttachment attachment,
        CancellationToken cancellationToken = default);

    async Task CopyAttachmentToAsync(
        string messageId,
        EmailAttachment attachment,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        await using var source = await OpenAttachmentAsync(messageId, attachment, cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }
}
