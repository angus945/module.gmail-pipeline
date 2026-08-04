namespace GmailPipeline.Google.Contract;

public sealed class GmailContentLimitsOptions
{
    public long MaxTextBodyBytes { get; set; } = 4 * 1024 * 1024;

    public long MaxEmbeddedAttachmentBytes { get; set; } = 256 * 1024;

    public long MaxTotalEmbeddedAttachmentBytes { get; set; } = 8 * 1024 * 1024;

    public long MaxOpenedAttachmentBytes { get; set; } = 32 * 1024 * 1024;

    public int MaxAttachmentCount { get; set; } = 256;

    public int MaxMimePartCount { get; set; } = 2048;

    public int MaxMimeDepth { get; set; } = 64;

    public long? MaxMessageSizeEstimateBytes { get; set; } = 64 * 1024 * 1024;

    public void Validate()
    {
        ThrowIfNotPositive(MaxTextBodyBytes, nameof(MaxTextBodyBytes));
        ThrowIfNotPositive(MaxEmbeddedAttachmentBytes, nameof(MaxEmbeddedAttachmentBytes));
        ThrowIfNotPositive(MaxTotalEmbeddedAttachmentBytes, nameof(MaxTotalEmbeddedAttachmentBytes));
        ThrowIfNotPositive(MaxOpenedAttachmentBytes, nameof(MaxOpenedAttachmentBytes));
        ThrowIfNotPositive(MaxAttachmentCount, nameof(MaxAttachmentCount));
        ThrowIfNotPositive(MaxMimePartCount, nameof(MaxMimePartCount));
        ThrowIfNotPositive(MaxMimeDepth, nameof(MaxMimeDepth));

        if (MaxMessageSizeEstimateBytes is not null)
        {
            ThrowIfNotPositive(MaxMessageSizeEstimateBytes.Value, nameof(MaxMessageSizeEstimateBytes));
        }
    }

    private static void ThrowIfNotPositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be greater than zero.");
        }
    }

    private static void ThrowIfNotPositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be greater than zero.");
        }
    }
}
