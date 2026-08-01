namespace GmailPipeline.Core.Models;

public sealed record EmailReference(
    string Id,
    string ThreadId,
    DateTimeOffset? ReceivedAt = null);
