using GmailPipeline.Google.Application.Ports;
namespace GmailPipeline.Google.Application.Ports;

public interface IGmailRetryDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
