using GmailPipeline.Google.Application.Ports;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Application.Ports;

public interface IGmailServiceAccessor : IDisposable
{
    Task<GmailService> GetAsync(CancellationToken cancellationToken = default);
}
