namespace GmailPipeline.Core.Contract.Exceptions;

public sealed class EmailContentFormatException : EmailClientException
{
    public EmailContentFormatException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
