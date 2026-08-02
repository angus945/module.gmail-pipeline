namespace GmailPipeline.Core.Exceptions;

public sealed class EmailRateLimitException : EmailClientException
{
    public EmailRateLimitException(
        string message,
        string operation,
        string? reason = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        Reason = reason;
        RetryAfter = retryAfter;
    }

    public string Operation { get; }

    public string? Reason { get; }

    public TimeSpan? RetryAfter { get; }
}
