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
using MimeKit;

namespace GmailPipeline.Google.Clients;

public sealed class GoogleGmailReader : IEmailReader
{
    private readonly IGmailServiceAccessor _serviceAccessor;
    private readonly IGmailMessageMapper _mapper;
    private readonly GmailApiRetryPolicy _retryPolicy;
    private readonly string _userId;

    public GoogleGmailReader(
        IGmailServiceAccessor serviceAccessor,
        IGmailMessageMapper mapper,
        GmailApiRetryPolicy retryPolicy,
        GmailAuthenticationOptions options)
    {
        _serviceAccessor = serviceAccessor;
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
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.List(_userId);
            gmailRequest.Q = string.IsNullOrWhiteSpace(request.Query) ? null : request.Query;
            gmailRequest.MaxResults = Math.Clamp(request.PageSize, 1, 500);
            gmailRequest.PageToken = request.PageToken;
            gmailRequest.IncludeSpamTrash = request.IncludeSpamTrash;

            var response = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "search messages", cancellationToken)
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
            var service = await _serviceAccessor.GetAsync(cancellationToken).ConfigureAwait(false);
            var gmailRequest = service.Users.Messages.Get(_userId, messageId);
            gmailRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Raw;
            var message = await _retryPolicy
                .ExecuteAsync(token => gmailRequest.ExecuteAsync(token), "get message", cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message.Raw))
            {
                throw new EmailContentFormatException("Gmail returned an empty RAW message payload.");
            }

            await using var stream = new MemoryStream(Base64UrlDecoder.Decode(message.Raw));
            var mimeMessage = await MimeMessage.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
            return _mapper.Map(message, mimeMessage);
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
}
