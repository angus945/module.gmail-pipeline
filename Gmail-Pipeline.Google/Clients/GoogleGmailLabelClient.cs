using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Labels;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Exceptions;
using Google;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailLabelClient : IEmailLabelClient
{
    private static readonly HashSet<string> SystemLabelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHAT",
        "SENT",
        "INBOX",
        "IMPORTANT",
        "TRASH",
        "DRAFT",
        "SPAM",
        "CATEGORY_FORUMS",
        "CATEGORY_UPDATES",
        "CATEGORY_PERSONAL",
        "CATEGORY_PROMOTIONS",
        "CATEGORY_SOCIAL",
        "STARRED",
        "UNREAD"
    };

    private readonly IGmailServiceAccessor _serviceAccessor;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly GmailLabelCache _labelCache;
    private readonly string _userId;

    public GoogleGmailLabelClient(
        IGmailServiceAccessor serviceAccessor,
        GmailApiRetryPolicy retryPolicy,
        GmailLabelCache labelCache,
        GmailAuthenticationOptions options)
    {
        _serviceAccessor = serviceAccessor;
        _retryPolicy = retryPolicy;
        _labelCache = labelCache;
        _userId = options.UserId;
    }

    public Task<IReadOnlyList<EmailLabel>> ListAsync(CancellationToken cancellationToken = default) =>
        _labelCache.GetAsync(cancellationToken);

    public async Task<EmailLabel?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var labels = await _labelCache.GetAsync(cancellationToken).ConfigureAwait(false);
        return labels.FirstOrDefault(label => string.Equals(label.Name, name, StringComparison.Ordinal));
    }

    public async Task<EmailLabel> GetOrCreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var existing = await FindByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        if (SystemLabelNames.Contains(name))
        {
            throw new EmailClientException($"Gmail system label '{name}' was not returned by the account and cannot be created.");
        }

        try
        {
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var label = new Label
            {
                Name = name,
                LabelListVisibility = "labelShow",
                MessageListVisibility = "show"
            };
            var gmailRequest = service.Users.Labels.Create(label, _userId);
            var created = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "create label", cancellationToken)
                .ConfigureAwait(false);
            _labelCache.Invalidate();
            return GmailLabelCache.ToEmailLabel(created);
        }
        catch (GoogleApiException exception) when (IsDuplicateLabelFailure(exception))
        {
            _labelCache.Invalidate();
            var createdByRace = await FindByNameAsync(name, cancellationToken).ConfigureAwait(false);
            if (createdByRace is not null)
            {
                return createdByRace;
            }

            throw GoogleExceptionMapper.Map(exception, "create label");
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "create label");
        }
    }

    public async Task ModifyMessageLabelsAsync(
        string messageId,
        EmailLabelModification modification,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (modification.AddLabelIds.Count == 0 && modification.RemoveLabelIds.Count == 0)
        {
            return;
        }

        try
        {
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var request = new ModifyMessageRequest
            {
                AddLabelIds = modification.AddLabelIds.ToArray(),
                RemoveLabelIds = modification.RemoveLabelIds.ToArray()
            };
            var gmailRequest = service.Users.Messages.Modify(request, _userId, messageId);
            await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "modify labels", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "modify labels");
        }
    }

    private static bool IsDuplicateLabelFailure(GoogleApiException exception) =>
        exception.HttpStatusCode == System.Net.HttpStatusCode.Conflict
        || exception.Error?.Errors?.Any(error =>
            string.Equals(error.Reason, "duplicate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error.Reason, "alreadyExists", StringComparison.OrdinalIgnoreCase)) == true;
}
