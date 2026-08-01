namespace GmailPipeline.Core.Labels;

public sealed record EmailLabelModification(
    IReadOnlyCollection<string> AddLabelIds,
    IReadOnlyCollection<string> RemoveLabelIds);
