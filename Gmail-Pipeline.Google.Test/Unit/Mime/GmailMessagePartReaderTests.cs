using System.Text;
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
    public void Base64UrlDecoderHandlesMissingPaddingAndUrlAlphabet()
    {
        Base64UrlDecoder.DecodeUtf8("SGVsbG8td29ybGQ").Should().Be("Hello-world");
    }

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
    public async Task ParseAsyncRejectsExternalizedTextBodyAboveConfiguredLimitBeforeFetchingContent()
    {
        var messageClient = new FakeMessageClient();
        var reader = CreateReader(new GmailContentLimitsOptions { MaxTextBodyBytes = 4 }, messageClient);
        var root = Mixed(Text("text/plain", attachmentId: "plain-body", size: 5));

        var act = async () => await reader.ParseAsync("message-1", root);

        await act.Should().ThrowAsync<EmailResourceLimitException>();
        messageClient.GetAttachmentCallCount.Should().Be(0);
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
    public async Task ParseAsyncTreatsTextPlainPartWithFilenameAsAttachment()
    {
        var reader = CreateReader();
        var root = Mixed(
            Text("text/plain", data: Encode("body"), size: 4),
            TextAttachment("note.txt", "text/plain", "attachment"));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("body");
        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("note.txt");
        Decode(attachment.EmbeddedContent).Should().Be("attachment");
    }

    [Fact]
    public async Task ParseAsyncTreatsTextHtmlPartWithFilenameAsAttachment()
    {
        var reader = CreateReader();
        var root = Mixed(
            Text("text/html", data: Encode("<p>body</p>"), size: 11),
            TextAttachment("receipt.html", "text/html", "<p>attachment</p>"));

        var result = await reader.ParseAsync("message-1", root);

        result.HtmlBody.Should().Be("<p>body</p>");
        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("receipt.html");
        Decode(attachment.EmbeddedContent).Should().Be("<p>attachment</p>");
    }

    [Fact]
    public async Task ParseAsyncKeepsInlineHtmlBodyWithContentIdAsBody()
    {
        var reader = CreateReader();
        var root = Mixed(new MessagePart
        {
            MimeType = "text/html",
            Headers =
            [
                Header("Content-Type", "text/html; charset=utf-8"),
                Header("Content-ID", "<html-body>")
            ],
            Body = new MessagePartBody
            {
                Data = Encode("<p>body</p>"),
                Size = 11
            }
        });

        var result = await reader.ParseAsync("message-1", root);

        result.HtmlBody.Should().Be("<p>body</p>");
        result.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsyncKeepsInlineResourceWithContentIdAsAttachment()
    {
        var reader = CreateReader();
        var root = Mixed(new MessagePart
        {
            MimeType = "image/png",
            Headers = [Header("Content-ID", "<logo>")],
            Body = new MessagePartBody
            {
                Data = Encode("png"),
                Size = 3
            }
        });

        var result = await reader.ParseAsync("message-1", root);

        var attachment = result.Attachments.Should().ContainSingle().Subject;
        attachment.ContentId.Should().Be("logo");
        attachment.Disposition.Should().Be(EmailAttachmentDisposition.Inline);
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

    [Fact]
    public async Task ParseAsyncRepresentsMessageRfc822WithFilenameOnlyOnce()
    {
        var reader = CreateReader();
        var nested = new MessagePart
        {
            MimeType = "message/rfc822",
            Filename = "forwarded.eml",
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

    [Fact]
    public async Task ParseAsyncHandlesMultipartAlternativeWithoutConsumingAttachments()
    {
        var reader = CreateReader();
        var root = Mixed(
            Alternative(
                Text("text/plain", data: Encode("plain"), size: 5),
                Text("text/html", data: Encode("<b>html</b>"), size: 11)),
            TextAttachment("notes.txt", "text/plain", "attachment"));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("plain");
        result.HtmlBody.Should().Be("<b>html</b>");
        result.Attachments.Should().ContainSingle(attachment => attachment.FileName == "notes.txt");
    }

    [Fact]
    public async Task ParseAsyncRejectsTotalEmbeddedAttachmentBytesAboveConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxTotalEmbeddedAttachmentBytes = 6 });
        var root = Mixed(
            Attachment("first.bin", "application/octet-stream", data: "1234", size: 4),
            Attachment("second.bin", "application/octet-stream", data: "5678", size: 4));

        var act = async () => await reader.ParseAsync("message-1", root);

        var exception = await act.Should().ThrowAsync<EmailResourceLimitException>();
        exception.Which.Resource.Should().Be("total embedded attachment bytes");
    }

    [Fact]
    public async Task ParseAsyncRejectsAttachmentCountAboveConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxAttachmentCount = 1 });
        var root = Mixed(
            Attachment("first.bin", "application/octet-stream", attachmentId: "external-1", size: 1),
            Attachment("second.bin", "application/octet-stream", attachmentId: "external-2", size: 1));

        var act = async () => await reader.ParseAsync("message-1", root);

        var exception = await act.Should().ThrowAsync<EmailResourceLimitException>();
        exception.Which.Resource.Should().Be("attachment count");
    }

    [Fact]
    public async Task ParseAsyncRejectsMimePartCountAboveConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxMimePartCount = 2 });
        var root = Mixed(
            Text("text/plain", data: Encode("one"), size: 3),
            Text("text/html", data: Encode("two"), size: 3));

        var act = async () => await reader.ParseAsync("message-1", root);

        var exception = await act.Should().ThrowAsync<EmailResourceLimitException>();
        exception.Which.Resource.Should().Be("MIME part count");
    }

    [Fact]
    public async Task ParseAsyncRejectsMimeDepthAboveConfiguredLimit()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxMimeDepth = 2 });
        var root = Mixed(Mixed(Text("text/plain", data: Encode("deep"), size: 4)));

        var act = async () => await reader.ParseAsync("message-1", root);

        var exception = await act.Should().ThrowAsync<EmailResourceLimitException>();
        exception.Which.Resource.Should().Contain("MIME depth");
    }

    [Theory]
    [InlineData("utf-8", "hello")]
    [InlineData("iso-8859-1", "café")]
    [InlineData("big5", "繁體中文")]
    [InlineData("shift_jis", "日本語")]
    public async Task ParseAsyncDecodesBodyWithDeclaredCharset(string charset, string expected)
    {
        var reader = CreateReader();
        var bytes = new DefaultEmailCharsetResolver().Resolve(charset, "test body").GetBytes(expected);
        var root = Mixed(Text("text/plain", data: Encode(bytes), size: bytes.Length, contentType: $"text/plain; charset={charset}"));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be(expected);
    }

    [Fact]
    public async Task ParseAsyncDecodesBodyWithQuotedCharset()
    {
        var reader = CreateReader();
        var root = Mixed(Text("text/plain", data: Encode("quoted"), size: 6, contentType: "text/plain; charset=\"utf-8\""));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("quoted");
    }

    [Fact]
    public async Task ParseAsyncRejectsUnsupportedCharset()
    {
        var reader = CreateReader();
        var root = Mixed(Text("text/plain", data: Encode("hello"), size: 5, contentType: "text/plain; charset=x-unknown-mail-charset"));

        var act = async () => await reader.ParseAsync("message-1", root);

        await act.Should().ThrowAsync<EmailContentFormatException>();
    }

    [Fact]
    public async Task ParseAsyncParsesQuotedSemicolonInContentTypeParameter()
    {
        var reader = CreateReader();
        var root = Mixed(new MessagePart
        {
            MimeType = "application/octet-stream",
            Headers = [Header("Content-Type", "application/octet-stream; name=\"a;b.txt\"")],
            Body = new MessagePartBody
            {
                Data = Encode("attachment"),
                Size = 10
            }
        });

        var result = await reader.ParseAsync("message-1", root);

        result.Attachments.Should().ContainSingle().Which.FileName.Should().Be("a;b.txt");
    }

    private static GmailMessagePartReader CreateReader(
        GmailContentLimitsOptions? limits = null,
        FakeMessageClient? messageClient = null) =>
        new(
            messageClient ?? new FakeMessageClient(),
            limits ?? new GmailContentLimitsOptions(),
            new DefaultEmailCharsetResolver(),
            new GmailAuthenticationOptions());

    private static MessagePart Mixed(params MessagePart[] parts) =>
        new()
        {
            MimeType = "multipart/mixed",
            Parts = parts
        };

    private static MessagePart Alternative(params MessagePart[] parts) =>
        new()
        {
            MimeType = "multipart/alternative",
            Parts = parts
        };

    private static MessagePart Text(
        string mimeType,
        string? data = null,
        string? attachmentId = null,
        int size = 0,
        string? contentType = null) =>
        new()
        {
            MimeType = mimeType,
            Headers = [Header("Content-Type", contentType ?? $"{mimeType}; charset=utf-8")],
            Body = new MessagePartBody
            {
                Data = data,
                AttachmentId = attachmentId,
                Size = size
            }
        };

    private static MessagePart TextAttachment(
        string fileName,
        string mimeType,
        string value) =>
        new()
        {
            MimeType = mimeType,
            Filename = fileName,
            Headers =
            [
                Header("Content-Type", $"{mimeType}; charset=utf-8"),
                Header("Content-Disposition", $"attachment; filename=\"{fileName}\"")
            ],
            Body = new MessagePartBody
            {
                Data = Encode(value),
                Size = Encoding.UTF8.GetByteCount(value)
            }
        };

    private static MessagePart Attachment(
        string fileName,
        string mimeType,
        string? attachmentId = null,
        int size = 0,
        string? data = null) =>
        new()
        {
            MimeType = mimeType,
            Filename = fileName,
            Headers = [Header("Content-Disposition", $"attachment; filename=\"{fileName}\"")],
            Body = new MessagePartBody
            {
                AttachmentId = attachmentId,
                Data = data is null ? null : Encode(data),
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
        Encode(Encoding.UTF8.GetBytes(value));

    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Decode(ReadOnlyMemory<byte>? value) =>
        Encoding.UTF8.GetString(value!.Value.Span);

    private sealed class FakeMessageClient : IGmailMessageClient
    {
        public Dictionary<string, MessagePartBody> Attachments { get; } = [];

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
