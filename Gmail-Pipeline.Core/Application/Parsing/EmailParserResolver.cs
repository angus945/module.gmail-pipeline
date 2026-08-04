using GmailPipeline.Core.Api;
using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Core.Application.Parsing;

public sealed class EmailParserResolver<TResult> : IEmailParserResolver<TResult>
{
    private readonly IReadOnlyList<IEmailParser<TResult>> _parsers;

    public EmailParserResolver(IEnumerable<IEmailParser<TResult>> parsers)
    {
        _parsers = parsers
            .OrderByDescending(parser => parser.Priority)
            .ThenBy(parser => parser.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public IEmailParser<TResult>? Resolve(EmailMessage message) =>
        _parsers.FirstOrDefault(parser => parser.CanParse(message));
}
