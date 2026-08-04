namespace GmailPipeline.Core.Contract.Labels;

public sealed record EmailLabelModification(
    IReadOnlyCollection<string> AddLabelIds,
    IReadOnlyCollection<string> RemoveLabelIds);
