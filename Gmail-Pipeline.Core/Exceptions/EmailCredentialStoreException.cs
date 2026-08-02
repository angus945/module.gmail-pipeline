namespace GmailPipeline.Core.Exceptions;

public sealed class EmailCredentialStoreException : EmailClientException
{
    public EmailCredentialStoreException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
