namespace GmailPipeline.Core.Contract.Models;

public sealed record EmailAddress(
    string Address,
    string? DisplayName = null);
