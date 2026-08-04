namespace GmailPipeline.Core.Contract.Parsing;

public sealed record EmailPipelineResult<TResult>(
    string? ParserName,
    EmailParseResult<TResult> ParseResult)
{
    public bool HasParser => ParserName is not null;

    public static EmailPipelineResult<TResult> NoParser() =>
        new(null, EmailParseResult<TResult>.Failed(new EmailParseError("NoParser", "No parser accepted this email message.")));
}
