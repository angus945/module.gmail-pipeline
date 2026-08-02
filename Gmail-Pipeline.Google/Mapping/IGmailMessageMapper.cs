using GmailPipeline.Core.Models;
using Google.Apis.Gmail.v1.Data;
using MimeKit;

namespace GmailPipeline.Google.Mapping;

public interface IGmailMessageMapper
{
    EmailMessage Map(Message message, MimeMessage mimeMessage);
}
