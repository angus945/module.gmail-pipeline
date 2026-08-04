namespace GmailPipeline.Core.Contract.Exceptions;

public sealed class EmailAuthorizationException : EmailClientException
{
    public EmailAuthorizationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
