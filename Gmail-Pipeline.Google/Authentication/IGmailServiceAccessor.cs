using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Authentication;

public interface IGmailServiceAccessor : IDisposable
{
    Task<GmailService> GetAsync(CancellationToken cancellationToken = default);
}
