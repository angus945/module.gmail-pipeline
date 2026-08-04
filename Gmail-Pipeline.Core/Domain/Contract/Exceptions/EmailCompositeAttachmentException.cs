using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Core.Contract.Exceptions;

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
