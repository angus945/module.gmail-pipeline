using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Infrastructure.Mime;

public interface IGmailMessagePartReader
{
    Task<GmailMimeParseResult> ParseAsync(
        string messageId,
        MessagePart root,
        CancellationToken cancellationToken = default);
}
