using FluentAssertions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Clients;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Test.Unit.Clients;

public sealed class GoogleGmailAttachmentClientTests
{
    [Fact]
    public async Task OpenAttachmentAsyncOpensZeroByteEmbeddedAttachment()
    {
        var client = CreateClient(new FakeMessageClient());
        var attachment = CreateAttachment() with
        {
            EmbeddedContent = ReadOnlyMemory<byte>.Empty
        };

        await using var stream = await client.OpenAttachmentAsync("message-1", attachment);

        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task OpenAttachmentAsyncOpensZeroByteExternalAttachment()
    {
        var messageClient = new FakeMessageClient();
        messageClient.Attachments["external-1"] = new MessagePartBody { Data = string.Empty, Size = 0 };
        var client = CreateClient(messageClient);
        var attachment = CreateAttachment() with
        {
            ExternalContentId = "external-1"
        };

        await using var stream = await client.OpenAttachmentAsync("message-1", attachment);

        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task OpenAttachmentAsyncRetrievesProviderPartOnlyWhenRequested()
    {
        var messageClient = new FakeMessageClient
        {
            Message = new Message
            {
                Id = "message-1",
                Payload = new MessagePart
                {
                    MimeType = "multipart/mixed",
                    Parts =
                    [
                        new MessagePart
                        {
                            MimeType = "application/pdf",
                            Body = new MessagePartBody
                            {
                                Data = Encode("pdf"),
                                Size = 3
                            }
                        }
                    ]
                }
            }
        };
        var client = CreateClient(messageClient);
        var attachment = CreateAttachment() with
        {
            ProviderPartId = "0.0"
        };

        await using var stream = await client.OpenAttachmentAsync("message-1", attachment);
        using var reader = new StreamReader(stream);

        (await reader.ReadToEndAsync()).Should().Be("pdf");
        messageClient.RequestedFormats.Should().Equal(UsersResource.MessagesResource.GetRequest.FormatEnum.Full);
    }

    [Fact]
    public async Task OpenAttachmentAsyncRejectsKnownExternalAttachmentAboveConfiguredLimitBeforeFetchingContent()
    {
        var messageClient = new FakeMessageClient();
        var client = CreateClient(messageClient, new GmailContentLimitsOptions { MaxOpenedAttachmentBytes = 4 });
        var attachment = CreateAttachment() with
        {
            ExternalContentId = "external-1",
            Size = 5
        };

        var act = async () => await client.OpenAttachmentAsync("message-1", attachment);

        await act.Should().ThrowAsync<EmailResourceLimitException>();
        messageClient.GetAttachmentCallCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenAttachmentAsyncRejectsKnownProviderPartAboveConfiguredLimitBeforeFetchingFullMessage()
    {
        var messageClient = new FakeMessageClient();
        var client = CreateClient(messageClient, new GmailContentLimitsOptions { MaxOpenedAttachmentBytes = 4 });
        var attachment = CreateAttachment() with
        {
            ProviderPartId = "0.0",
            Size = 5
        };

        var act = async () => await client.OpenAttachmentAsync("message-1", attachment);

        await act.Should().ThrowAsync<EmailResourceLimitException>();
        messageClient.RequestedFormats.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAttachmentAsyncRejectsExternalAttachmentResponseAboveConfiguredLimit()
    {
        var messageClient = new FakeMessageClient();
        messageClient.Attachments["external-1"] = new MessagePartBody { Data = Encode("hello"), Size = 5 };
        var client = CreateClient(messageClient, new GmailContentLimitsOptions { MaxOpenedAttachmentBytes = 4 });
        var attachment = CreateAttachment() with
        {
            ExternalContentId = "external-1"
        };

        var act = async () => await client.OpenAttachmentAsync("message-1", attachment);

        await act.Should().ThrowAsync<EmailResourceLimitException>();
        messageClient.GetAttachmentCallCount.Should().Be(1);
    }

    private static GoogleGmailAttachmentClient CreateClient(
        FakeMessageClient messageClient,
        GmailContentLimitsOptions? limits = null) =>
        new(messageClient, limits ?? new GmailContentLimitsOptions(), new GmailAuthenticationOptions());

    private static EmailAttachment CreateAttachment() =>
        new()
        {
            Id = "0.0",
            MediaType = "application/octet-stream",
            PartPath = "0.0"
        };

    private static string Encode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class FakeMessageClient : IGmailMessageClient
    {
        public Message? Message { get; init; }

        public Dictionary<string, MessagePartBody> Attachments { get; } = [];

        public List<UsersResource.MessagesResource.GetRequest.FormatEnum> RequestedFormats { get; } = [];

        public int GetAttachmentCallCount { get; private set; }

        public Task<ListMessagesResponse> SearchAsync(
            string userId,
            EmailSearchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Message> GetAsync(
            string userId,
            string messageId,
            UsersResource.MessagesResource.GetRequest.FormatEnum format,
            CancellationToken cancellationToken = default)
        {
            RequestedFormats.Add(format);
            return Task.FromResult(Message!);
        }

        public Task<MessagePartBody> GetAttachmentAsync(
            string userId,
            string messageId,
            string attachmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAttachment(attachmentId));

        private MessagePartBody GetAttachment(string attachmentId)
        {
            GetAttachmentCallCount++;
            return Attachments[attachmentId];
        }
    }
}
