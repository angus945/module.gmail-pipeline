namespace GmailPipeline.Core.Parsing;

public sealed record EmailParseError(
    string Code,
    string Message);
