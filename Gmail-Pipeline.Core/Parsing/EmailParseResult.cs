namespace GmailPipeline.Core.Parsing;

public abstract record EmailParseResult<TResult>
{
    private EmailParseResult()
    {
    }

    public bool IsSuccess => this is Success;

    public TResult? Value => this is Success success ? success.Result : default;

    public IReadOnlyList<EmailParseError> Errors => this is Failure failure ? failure.ParseErrors : [];

    public IReadOnlyList<string> Warnings => this is Success success ? success.ParseWarnings : [];

    public sealed record Success(
        TResult Result,
        IReadOnlyList<string> ParseWarnings) : EmailParseResult<TResult>;

    public sealed record Failure(
        IReadOnlyList<EmailParseError> ParseErrors) : EmailParseResult<TResult>;

    public static EmailParseResult<TResult> Succeeded(TResult value, IReadOnlyList<string>? warnings = null) =>
        new Success(value, warnings ?? []);

    public static EmailParseResult<TResult> Failed(params EmailParseError[] errors) =>
        new Failure(errors);
}
