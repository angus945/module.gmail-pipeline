using GmailPipeline.Core.Models;
using GmailPipeline.Core.Parsing;

namespace GmailPipeline.Core.Abstractions;

public interface IEmailPipeline<TResult>
{
    Task<EmailPipelineResult<TResult>> ProcessAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
