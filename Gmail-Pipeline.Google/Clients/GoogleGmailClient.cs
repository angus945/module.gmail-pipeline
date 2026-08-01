using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Exceptions;
using GmailPipeline.Google.Mapping;
using Google;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailClient : IEmailClient
{
    private readonly GmailServiceFactory _serviceFactory;
    private readonly IGmailMessageMapper _mapper;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly string _userId;
    private GmailService? _service;

    public GoogleGmailClient(
        GmailServiceFactory serviceFactory,
        IGmailMessageMapper mapper,
        GmailApiRetryPolicy retryPolicy,
        GmailAuthenticationOptions options)
    {
        _serviceFactory = serviceFactory;
        _mapper = mapper;
        _retryPolicy = retryPolicy;
        _userId = options.UserId;
    }

    public async Task<EmailSearchResult> SearchAsync(
        EmailSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.List(_userId);
            gmailRequest.Q = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query;
            gmailRequest.MaxResults = Math.Clamp(request.PageSize, 1, 500);
            gmailRequest.PageToken = request.PageToken;
            gmailRequest.IncludeSpamTrash = request.IncludeSpamTrash;

            var response = await _retryPolicy.ExecuteAsync(token => gmailRequest.ExecuteAsync(token), cancellationToken).ConfigureAwait(false);
            var references = (response.Messages ?? [])
                .Select(message => new EmailReference(message.Id, message.ThreadId ?? string.Empty))
                .ToArray();

            return new EmailSearchResult(references, response.NextPageToken);
        }
        catch (Exception exception) when (exception is GoogleApiException or HttpRequestException)
        {
            throw GoogleExceptionMapper.Map(exception, "search");
        }
    }

    public async Task<EmailMessage?> GetAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.Get(_userId, messageId);
            gmailRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var message = await _retryPolicy.ExecuteAsync(token => gmailRequest.ExecuteAsync(token), cancellationToken).ConfigureAwait(false);
            return _mapper.Map(message);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception exception) when (exception is GoogleApiException or HttpRequestException)
        {
            throw GoogleExceptionMapper.Map(exception, "get message");
        }
    }

    public Task AddLabelsAsync(
        string messageId,
        IReadOnlyCollection<string> labelIds,
        CancellationToken cancellationToken = default) =>
        ModifyLabelsAsync(messageId, labelIds, [], cancellationToken);

    public Task RemoveLabelsAsync(
        string messageId,
        IReadOnlyCollection<string> labelIds,
        CancellationToken cancellationToken = default) =>
        ModifyLabelsAsync(messageId, [], labelIds, cancellationToken);

    private async Task ModifyLabelsAsync(
        string messageId,
        IReadOnlyCollection<string> addLabelIds,
        IReadOnlyCollection<string> removeLabelIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var service = await GetServiceAsync(cancellationToken).ConfigureAwait(false);
            var request = new ModifyMessageRequest
            {
                AddLabelIds = addLabelIds.ToArray(),
                RemoveLabelIds = removeLabelIds.ToArray()
            };
            var gmailRequest = service.Users.Messages.Modify(request, _userId, messageId);
            await _retryPolicy.ExecuteAsync(token => gmailRequest.ExecuteAsync(token), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is GoogleApiException or HttpRequestException)
        {
            throw GoogleExceptionMapper.Map(exception, "modify labels");
        }
    }

    private async Task<GmailService> GetServiceAsync(CancellationToken cancellationToken)
    {
        _service ??= await _serviceFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return _service;
    }
}
