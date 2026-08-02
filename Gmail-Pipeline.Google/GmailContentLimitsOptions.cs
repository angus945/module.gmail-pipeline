namespace GmailPipeline.Google;

public sealed class GmailContentLimitsOptions
{
    public long MaxTextBodyBytes { get; set; } = 4 * 1024 * 1024;

    public long MaxEmbeddedAttachmentBytes { get; set; } = 256 * 1024;

    public long MaxOpenedAttachmentBytes { get; set; } = 32 * 1024 * 1024;
}
