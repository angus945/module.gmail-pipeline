using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Core.Contract.Parsing;

namespace GmailPipeline.Core.Api;

public interface IEmailPipeline<TResult>
{
    Task<EmailPipelineResult<TResult>> ProcessAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
