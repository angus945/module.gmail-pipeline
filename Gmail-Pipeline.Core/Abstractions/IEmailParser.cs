using GmailPipeline.Core.Models;
using GmailPipeline.Core.Parsing;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailParser<TResult>
{
    string Name { get; }

    int Priority { get; }

    bool CanParse(EmailMessage message);

    Task<EmailParseResult<TResult>> ParseAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
