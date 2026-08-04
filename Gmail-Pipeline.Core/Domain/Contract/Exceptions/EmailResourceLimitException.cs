namespace GmailPipeline.Core.Contract.Exceptions;

public sealed class EmailResourceLimitException : EmailClientException
{
    public EmailResourceLimitException(
        string resource,
        long actualBytes,
        long allowedBytes)
        : base($"{resource} is {actualBytes} bytes; the configured limit is {allowedBytes} bytes.")
    {
        Resource = resource;
        ActualBytes = actualBytes;
        AllowedBytes = allowedBytes;
    }

    public string Resource { get; }

    public long ActualBytes { get; }

    public long AllowedBytes { get; }
}
