using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Parsing;

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
