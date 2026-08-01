namespace GmailPipeline.Core.Exceptions;

public sealed class EmailAuthenticationException : EmailClientException
{
    public EmailAuthenticationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
