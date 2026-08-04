using System.Net;
using GmailPipeline.Google.Application.Ports;
using Google;

namespace GmailPipeline.Google.Infrastructure.Clients;

public sealed class GmailApiRetryPolicy
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private readonly IGmailRetryDelay _delay;

    public GmailApiRetryPolicy(IGmailRetryDelay delay)
    {
        _delay = delay;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < MaxAttempts && IsTransient(exception))
            {
                var classification = GoogleErrorClassifier.Classify(exception);
                var nextDelay = classification.RetryAfter ?? AddJitter(delay);
                await _delay.DelayAsync(nextDelay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, MaxDelay.TotalMilliseconds));
            }
        }
    }

    public static bool IsTransient(Exception exception) =>
        exception is HttpRequestException
        || GoogleErrorClassifier.Classify(exception).IsTransient;

    private static TimeSpan AddJitter(TimeSpan delay)
    {
        var jitterMs = Random.Shared.Next(0, Math.Max(1, (int)Math.Min(250, delay.TotalMilliseconds / 4)));
        return delay + TimeSpan.FromMilliseconds(jitterMs);
    }
}

internal sealed class TaskDelayGmailRetryDelay : IGmailRetryDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
