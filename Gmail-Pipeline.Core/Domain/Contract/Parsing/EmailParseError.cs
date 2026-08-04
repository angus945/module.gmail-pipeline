namespace GmailPipeline.Core.Contract.Parsing;

public sealed record EmailParseError(
    string Code,
    string Message);
