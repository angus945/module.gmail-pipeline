namespace GmailPipeline.Core.Exceptions;

public class EmailClientException : Exception
{
    public EmailClientException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
