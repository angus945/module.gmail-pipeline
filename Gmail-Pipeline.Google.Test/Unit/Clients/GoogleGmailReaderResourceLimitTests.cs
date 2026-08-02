using FluentAssertions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Clients;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Test.Unit.Clients;

public sealed class GoogleGmailReaderResourceLimitTests
{
    [Fact]
    public async Task GetAsyncRequestsFullMessageAndNeverRaw()
    {
        var messageClient = new FakeMessageClient
        {
            Message = CreateMessage(Text("text/plain", Encode("hello"), size: 5))
        };
        var reader = CreateReader(messageClient);

        var message = await reader.GetAsync("message-1");

        message!.TextBody.Should().Be("hello");
        messageClient.RequestedFormats.Should().Equal(UsersResource.MessagesResource.GetRequest.FormatEnum.Full);
        messageClient.RequestedFormats.Should().NotContain(UsersResource.MessagesResource.GetRequest.FormatEnum.Raw);
    }

    [Fact]
    public async Task GetAsyncMapsOversizeTextToResourceLimitException()
    {
        var messageClient = new FakeMessageClient
        {
            Message = CreateMessage(Text("text/plain", Encode("hello"), size: 5))
        };
        var reader = CreateReader(messageClient, new GmailContentLimitsOptions { MaxTextBodyBytes = 4 });

        var act = async () => await reader.GetAsync("message-1");

        await act.Should().ThrowAsync<EmailResourceLimitException>();
    }

    [Fact]
    public async Task GetAsyncKeepsCancellationAsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        var messageClient = new FakeMessageClient
        {
            ThrowOnGet = token => new OperationCanceledException(token)
        };
        var reader = CreateReader(messageClient);
        await source.CancelAsync();

        var act = async () => await reader.GetAsync("message-1", source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static GoogleGmailReader CreateReader(
        FakeMessageClient messageClient,
        GmailContentLimitsOptions? limits = null)
    {
        var options = new GmailAuthenticationOptions();
        return new GoogleGmailReader(
            messageClient,
            new GmailMessageMapper(),
            new GmailMessagePartReader(messageClient, limits ?? new GmailContentLimitsOptions(), options),
            options);
    }

    private static Message CreateMessage(MessagePart part) =>
        new()
        {
            Id = "message-1",
            ThreadId = "thread-1",
            Payload = new MessagePart
            {
                MimeType = "multipart/mixed",
                Headers =
                [
                    new MessagePartHeader { Name = "Subject", Value = "Statement" }
                ],
                Parts = [part]
            }
        };

    private static MessagePart Text(string mimeType, string data, int size) =>
        new()
        {
            MimeType = mimeType,
            Headers =
            [
                new MessagePartHeader
                {
                    Name = "Content-Type",
                    Value = $"{mimeType}; charset=utf-8"
                }
            ],
            Body = new MessagePartBody
            {
                Data = data,
                Size = size
            }
        };

    private static string Encode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class FakeMessageClient : IGmailMessageClient
    {
        public Message? Message { get; init; }

        public Func<CancellationToken, Exception>? ThrowOnGet { get; init; }

        public List<UsersResource.MessagesResource.GetRequest.FormatEnum> RequestedFormats { get; } = [];

        public Task<ListMessagesResponse> SearchAsync(
            string userId,
            EmailSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ListMessagesResponse());

        public Task<Message> GetAsync(
            string userId,
            string messageId,
            UsersResource.MessagesResource.GetRequest.FormatEnum format,
            CancellationToken cancellationToken = default)
        {
            RequestedFormats.Add(format);
            if (ThrowOnGet is not null)
            {
                throw ThrowOnGet(cancellationToken);
            }

            return Task.FromResult(Message!);
        }

        public Task<MessagePartBody> GetAttachmentAsync(
            string userId,
            string messageId,
            string attachmentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
