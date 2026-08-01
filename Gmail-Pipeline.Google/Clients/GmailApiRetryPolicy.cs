using System.Net;
using Google;

namespace GmailPipeline.Google.Clients;

public sealed class GmailApiRetryPolicy
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 5 && IsTransient(exception))
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, TimeSpan.FromSeconds(30).TotalMilliseconds));
            }
        }
    }

    public static bool IsTransient(Exception exception) =>
        exception is HttpRequestException
        || exception is GoogleApiException { HttpStatusCode: HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError };
}
