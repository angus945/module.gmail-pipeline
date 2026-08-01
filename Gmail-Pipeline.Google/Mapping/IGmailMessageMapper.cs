using GmailPipeline.Core.Models;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Mapping;

public interface IGmailMessageMapper
{
    EmailMessage Map(Message message);
}
