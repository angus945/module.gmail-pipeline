using System.Net;
using Google;

namespace GmailPipeline.Google.Clients;

public sealed record GoogleErrorClassification(
    bool IsTransient,
    bool IsRateLimit,
    string? Reason,
    TimeSpan? RetryAfter);

public static class GoogleErrorClassifier
{
    private static readonly HashSet<string> RateLimitReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "rateLimitExceeded",
        "userRateLimitExceeded"
    };

    public static GoogleErrorClassification Classify(Exception exception)
    {
        if (exception is HttpRequestException)
        {
            return new GoogleErrorClassification(true, false, null, null);
        }

        if (exception is not GoogleApiException googleException)
        {
            return new GoogleErrorClassification(false, false, null, null);
        }

        var reason = googleException.Error?.Errors?
            .Select(error => error.Reason)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var statusCode = googleException.HttpStatusCode;
        var isRateLimit = statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.Forbidden && reason is not null && RateLimitReasons.Contains(reason);
        var isBackend = statusCode >= HttpStatusCode.InternalServerError
            || string.Equals(reason, "backendError", StringComparison.OrdinalIgnoreCase);

        return new GoogleErrorClassification(
            isBackend || isRateLimit,
            isRateLimit,
            reason,
            TryGetRetryAfter(googleException));
    }

    private static TimeSpan? TryGetRetryAfter(GoogleApiException exception)
    {
        foreach (var propertyName in new[] { "Headers", "ResponseHeaders" })
        {
            var property = exception.GetType().GetProperty(propertyName);
            if (property?.GetValue(exception) is not object headers)
            {
                continue;
            }

            var retryAfter = TryReadRetryAfter(headers);
            if (retryAfter is not null)
            {
                return retryAfter;
            }
        }

        return null;
    }

    private static TimeSpan? TryReadRetryAfter(object headers)
    {
        if (headers is IEnumerable<KeyValuePair<string, IEnumerable<string>>> enumerableHeaders)
        {
            foreach (var header in enumerableHeaders)
            {
                if (string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseRetryAfter(header.Value.FirstOrDefault());
                }
            }
        }

        if (headers is IEnumerable<KeyValuePair<string, string>> stringHeaders)
        {
            foreach (var header in stringHeaders)
            {
                if (string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseRetryAfter(header.Value);
                }
            }
        }

        return null;
    }

    private static TimeSpan? ParseRetryAfter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        return null;
    }
}
