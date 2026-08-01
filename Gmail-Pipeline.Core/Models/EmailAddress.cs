namespace GmailPipeline.Core.Models;

public sealed record EmailAddress(
    string Address,
    string? DisplayName = null);
