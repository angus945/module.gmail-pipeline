namespace GmailPipeline.Core.Contract.Models;

public sealed record EmailReference(
    string Id,
    string ThreadId,
    DateTimeOffset? ReceivedAt = null);
