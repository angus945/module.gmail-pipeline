namespace GmailPipeline.Core.Exceptions;

public sealed class EmailNotFoundException : EmailClientException
{
    public EmailNotFoundException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
