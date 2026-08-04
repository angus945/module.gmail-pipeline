using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Google.Infrastructure.Mime;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Infrastructure.Mapping;

public interface IGmailMessageMapper
{
    EmailMessage Map(Message message, GmailMimeParseResult parsedMime);
}
