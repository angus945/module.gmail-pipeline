using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Core.Contract.Parsing;

namespace GmailPipeline.Core.Api;

public interface IEmailParser<TResult>
{
    string Name { get; }

    int Priority { get; }

    bool CanParse(EmailMessage message);

    Task<EmailParseResult<TResult>> ParseAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
