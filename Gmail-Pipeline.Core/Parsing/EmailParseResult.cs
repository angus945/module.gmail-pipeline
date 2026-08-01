namespace GmailPipeline.Core.Parsing;

public sealed record EmailParseResult<TResult>
{
    public bool IsSuccess { get; init; }

    public TResult? Value { get; init; }

    public IReadOnlyList<EmailParseError> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static EmailParseResult<TResult> Success(TResult value, IReadOnlyList<string>? warnings = null) =>
        new() { IsSuccess = true, Value = value, Warnings = warnings ?? [] };

    public static EmailParseResult<TResult> Failed(params EmailParseError[] errors) =>
        new() { IsSuccess = false, Errors = errors };
}
