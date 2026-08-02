using FluentAssertions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Core.Models;
using GmailPipeline.Core.Search;
using GmailPipeline.Google.Authentication;
using GmailPipeline.Google.Clients;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Test.Unit.Mime;

public sealed class GmailMessagePartReaderTests
{
    [Fact]
    public async Task ParseAsyncDoesNotPopulateEmbeddedContentForLargeExternalAttachment()
    {
        var reader = CreateReader();
        var root = Mixed(
            Attachment("statement.pdf", "application/pdf", attachmentId: "external-1", size: 100 * 1024 * 1024));

        var result = await reader.ParseAsync("message-1", root);

        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("statement.pdf");
        attachment.ExternalContentId.Should().Be("external-1");
        attachment.HasEmbeddedContent.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsyncRetainsProviderPartIdForLargeEmbeddedAttachmentData()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxEmbeddedAttachmentBytes = 4 });
        var root = Mixed(new MessagePart
        {
            MimeType = "application/pdf",
            Filename = "statement.pdf",
            Headers = [Header("Content-Disposition", "attachment; filename=\"statement.pdf\"")],
            Body = new MessagePartBody
            {
                Data = Encode("tiny"),
                Size = 5
            }
        });

        var result = await reader.ParseAsync("message-1", root);

        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.ProviderPartId.Should().Be("0.0");
        attachment.HasEmbeddedContent.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsyncReadsExternalizedPlainAndHtmlBodies()
    {
        var messageClient = new FakeMessageClient();
        messageClient.Attachments["plain-body"] = new MessagePartBody { Data = Encode("plain"), Size = 5 };
        messageClient.Attachments["html-body"] = new MessagePartBody { Data = Encode("<b>html</b>"), Size = 11 };
        var reader = CreateReader(messageClient: messageClient);
        var root = Mixed(
            Text("text/plain", attachmentId: "plain-body", size: 5),
            Text("text/html", attachmentId: "html-body", size: 11));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("plain");
        result.HtmlBody.Should().Be("<b>html</b>");
    }

    [Fact]
    public async Task ParseAsyncRejectsTextBodyAboveConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxTextBodyBytes = 4 });
        var root = Mixed(Text("text/plain", data: Encode("hello"), size: 5));

        var act = async () => await reader.ParseAsync("message-1", root);

        var exception = await act.Should().ThrowAsync<EmailResourceLimitException>();
        exception.Which.Resource.Should().Contain("text/plain");
    }

    [Fact]
    public async Task ParseAsyncAcceptsTextBodyAtConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxTextBodyBytes = 5 });
        var root = Mixed(Text("text/plain", data: Encode("hello"), size: 5));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("hello");
    }

    [Fact]
    public async Task ParseAsyncRepresentsNestedMessageRfc822OnlyOnce()
    {
        var reader = CreateReader();
        var nested = new MessagePart
        {
            MimeType = "message/rfc822",
            Filename = "forwarded.eml",
            Headers = [Header("Content-Disposition", "attachment; filename=\"forwarded.eml\"")],
            Body = new MessagePartBody { Data = Encode("From: nested@example.test\r\n\r\nbody"), Size = 32 },
            Parts =
            [
                Attachment("nested.pdf", "application/pdf", attachmentId: "nested-attachment", size: 10)
            ]
        };

        var result = await reader.ParseAsync("message-1", Mixed(nested));

        result.Attachments.Should().ContainSingle();
        result.Attachments[0].FileName.Should().Be("forwarded.eml");
    }

    private static GmailMessagePartReader CreateReader(
        GmailContentLimitsOptions? limits = null,
        FakeMessageClient? messageClient = null) =>
        new(
            messageClient ?? new FakeMessageClient(),
            limits ?? new GmailContentLimitsOptions(),
            new GmailAuthenticationOptions());

    private static MessagePart Mixed(params MessagePart[] parts) =>
        new()
        {
            MimeType = "multipart/mixed",
            Parts = parts
        };

    private static MessagePart Text(
        string mimeType,
        string? data = null,
        string? attachmentId = null,
        int size = 0) =>
        new()
        {
            MimeType = mimeType,
            Headers = [Header("Content-Type", $"{mimeType}; charset=utf-8")],
            Body = new MessagePartBody
            {
                Data = data,
                AttachmentId = attachmentId,
                Size = size
            }
        };

    private static MessagePart Attachment(
        string fileName,
        string mimeType,
        string? attachmentId,
        int size) =>
        new()
        {
            MimeType = mimeType,
            Filename = fileName,
            Headers = [Header("Content-Disposition", $"attachment; filename=\"{fileName}\"")],
            Body = new MessagePartBody
            {
                AttachmentId = attachmentId,
                Size = size
            }
        };

    private static MessagePartHeader Header(string name, string value) =>
        new()
        {
            Name = name,
            Value = value
        };

    private static string Encode(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class FakeMessageClient : IGmailMessageClient
    {
        public Dictionary<string, MessagePartBody> Attachments { get; } = [];

        public Task<ListMessagesResponse> SearchAsync(
            string userId,
            EmailSearchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Message> GetAsync(
            string userId,
            string messageId,
            UsersResource.MessagesResource.GetRequest.FormatEnum format,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MessagePartBody> GetAttachmentAsync(
            string userId,
            string messageId,
            string attachmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Attachments[attachmentId]);
    }
}
