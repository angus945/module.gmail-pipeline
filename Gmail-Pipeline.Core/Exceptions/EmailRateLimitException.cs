namespace GmailPipeline.Core.Exceptions;

public sealed class EmailRateLimitException : EmailClientException
{
    public EmailRateLimitException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
