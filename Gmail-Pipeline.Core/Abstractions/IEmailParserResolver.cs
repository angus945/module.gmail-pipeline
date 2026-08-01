using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailParserResolver<TResult>
{
    IEmailParser<TResult>? Resolve(EmailMessage message);
}
