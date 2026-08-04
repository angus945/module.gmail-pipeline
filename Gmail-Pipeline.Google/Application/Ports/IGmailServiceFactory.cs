using GmailPipeline.Google.Application.Ports;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Application.Ports;

public interface IGmailServiceFactory
{
    Task<GmailService> CreateAsync(CancellationToken cancellationToken = default);
}
