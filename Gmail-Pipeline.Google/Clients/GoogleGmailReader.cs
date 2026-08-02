using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Exceptions;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Google;
using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailReader : IEmailReader
{
    private readonly IGmailMessageClient _messageClient;
    private readonly IGmailMessageMapper _mapper;
    private readonly IGmailMessagePartReader _partReader;
    private readonly GmailContentLimitsOptions _limits;
    private readonly string _userId;

    public GoogleGmailReader(
        IGmailMessageClient messageClient,
        IGmailMessageMapper mapper,
        IGmailMessagePartReader partReader,
        GmailContentLimitsOptions limits,
        GmailAuthenticationOptions options)
    {
        _messageClient = messageClient;
        _mapper = mapper;
        _partReader = partReader;
        _limits = limits;
        _limits.Validate();
        _userId = options.UserId;
    }

    public async Task<EmailSearchResult> SearchAsync(
        EmailSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _messageClient
                .SearchAsync(_userId, request, cancellationToken)
                .ConfigureAwait(false);
            var references = (response.Messages ?? [])
                .Select(message => new EmailReference(message.Id, message.ThreadId ?? string.Empty))
                .ToArray();

            return new EmailSearchResult(references, response.NextPageToken);
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "search messages");
        }
    }

    public async Task<EmailMessage?> GetAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _messageClient
                .GetAsync(_userId, messageId, UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata, cancellationToken)
                .ConfigureAwait(false);
            EnsureMessageSizeWithinLimit(metadata);

            var message = await _messageClient
                .GetAsync(_userId, messageId, UsersResource.MessagesResource.GetRequest.FormatEnum.Full, cancellationToken)
                .ConfigureAwait(false);
            if (message.Payload is null)
            {
                throw new EmailContentFormatException("Gmail returned a FULL message without a MIME payload.");
            }

            var parsedMime = await _partReader.ParseAsync(message.Id, message.Payload, cancellationToken).ConfigureAwait(false);
            return _mapper.Map(message, parsedMime);
        }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception exception) when (GoogleExceptionMapper.CanMap(exception))
        {
            throw GoogleExceptionMapper.Map(exception, "get message");
        }
    }

    private void EnsureMessageSizeWithinLimit(global::Google.Apis.Gmail.v1.Data.Message metadata)
    {
        if (_limits.MaxMessageSizeEstimateBytes is not { } limit || metadata.SizeEstimate is null)
        {
            return;
        }

        if (metadata.SizeEstimate.Value > limit)
        {
            throw new EmailResourceLimitException("message size estimate", metadata.SizeEstimate.Value, limit);
        }
    }
}
