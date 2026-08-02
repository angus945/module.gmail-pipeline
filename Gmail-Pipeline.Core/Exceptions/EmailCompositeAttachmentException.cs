using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Exceptions;

public sealed class EmailCompositeAttachmentException : EmailClientException
{
    public EmailCompositeAttachmentException(EmailAttachment attachment)
        : base($"Attachment '{attachment.Id}' is a {attachment.Kind} MIME entity and cannot be opened as a single byte stream.")
    {
        AttachmentId = attachment.Id;
        Kind = attachment.Kind;
    }

    public string AttachmentId { get; }

    public EmailAttachmentKind Kind { get; }
}
