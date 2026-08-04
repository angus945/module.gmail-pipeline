using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Core.Api;

public interface IEmailParserResolver<TResult>
{
    IEmailParser<TResult>? Resolve(EmailMessage message);
}
