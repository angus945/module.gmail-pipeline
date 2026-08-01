using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Parsing;

public sealed class EmailPipeline<TResult> : IEmailPipeline<TResult>
{
    private readonly IEmailParserResolver<TResult> _resolver;

    public EmailPipeline(IEmailParserResolver<TResult> resolver)
    {
        _resolver = resolver;
    }

    public async Task<EmailPipelineResult<TResult>> ProcessAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var parser = _resolver.Resolve(message);
        if (parser is null)
        {
            return EmailPipelineResult<TResult>.NoParser();
        }

        var result = await parser.ParseAsync(message, cancellationToken).ConfigureAwait(false);
        return new EmailPipelineResult<TResult>(parser.Name, result);
    }
}
