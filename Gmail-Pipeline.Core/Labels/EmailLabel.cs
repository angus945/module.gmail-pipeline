namespace GmailPipeline.Core.Labels;

public sealed record EmailLabel(
    string Id,
    string Name,
    string? Type = null);
