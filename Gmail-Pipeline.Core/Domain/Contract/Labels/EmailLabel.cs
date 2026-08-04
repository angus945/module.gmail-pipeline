namespace GmailPipeline.Core.Contract.Labels;

public sealed record EmailLabel(
    string Id,
    string Name,
    string? Type = null);
