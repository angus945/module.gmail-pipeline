using GmailPipeline.Core.Contract.Labels;
using GmailPipeline.Google.Application.Ports;
using GmailPipeline.Google.Contract;
using GmailPipeline.Google.Infrastructure.Authentication;
using GmailPipeline.Google.Infrastructure.Exceptions;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Infrastructure.Clients;

public sealed class GmailLabelCache
{
    private readonly IGmailServiceAccessor _serviceAccessor;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly string _userId;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<EmailLabel>? _labels;

    public GmailLabelCache(
        IGmailServiceAccessor serviceAccessor,
        GmailApiRetryPolicy retryPolicy,
        GmailAuthenticationOptions options)
    {
        _serviceAccessor = serviceAccessor;
        _retryPolicy = retryPolicy;
        _userId = options.UserId;
    }

    public async Task<IReadOnlyList<EmailLabel>> GetAsync(CancellationToken cancellationToken)
    {
        if (_labels is not null)
        {
            return _labels;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _labels ??= await LoadAsync(cancellationToken).ConfigureAwait(false);
            return _labels;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _labels = null;

    private async Task<IReadOnlyList<EmailLabel>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Labels.List(_userId);
            var response = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "list labels", cancellationToken)
                .ConfigureAwait(false);
            return (response.Labels ?? [])
                .Select(ToEmailLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label.Id))
                .ToArray();
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "list labels");
        }
    }

    internal static EmailLabel ToEmailLabel(Label label) =>
        new(label.Id ?? string.Empty, label.Name ?? string.Empty, label.Type);
}
