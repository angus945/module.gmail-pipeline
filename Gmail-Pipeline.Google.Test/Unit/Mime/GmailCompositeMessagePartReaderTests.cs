using System.Text;
using FluentAssertions;
using GmailPipeline.Google.Application.Ports;
using GmailPipeline.Core.Contract.Models;
using GmailPipeline.Core.Contract.Search;
using GmailPipeline.Google.Contract;
using GmailPipeline.Google.Infrastructure.Authentication;
using GmailPipeline.Google.Infrastructure.Clients;
using GmailPipeline.Google.Infrastructure.Mime;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Test.Unit.Mime;

public sealed class GmailCompositeMessagePartReaderTests
{
    [Fact]
    public async Task ParseAsyncKeepsMultipartAttachmentAsComposite()
    {
        var reader = CreateReader();
        var root = Mixed(
            Text("outer body"),
            new MessagePart
            {
                MimeType = "multipart/mixed",
                Filename = "bundle.mime",
                Headers = [Header("Content-Disposition", "attachment; filename=\"bundle.mime\"")],
                Parts =
                [
                    Text("inner text"),
                    Attachment("inner.pdf", "application/pdf", "pdf")
                ]
            });

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("outer body");
        result.BodySections.Should().ContainSingle(section => section.Content == "outer body");
        var bundle = result.Attachments.Should().ContainSingle().Subject;
        bundle.Kind.Should().Be(EmailAttachmentKind.Composite);
        bundle.FileName.Should().Be("bundle.mime");
        bundle.BodySections.Should().ContainSingle(section => section.Content == "inner text");
        bundle.Children.Should().ContainSingle(child => child.FileName == "inner.pdf");
    }

    [Fact]
    public async Task ParseAsyncKeepsMultipartAttachmentWithoutFilenameAsComposite()
    {
        var reader = CreateReader();
        var root = Mixed(new MessagePart
        {
            MimeType = "multipart/mixed",
            Headers = [Header("Content-Disposition", "attachment")],
            Parts = [Text("inner text")]
        });

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().BeNull();
        var bundle = result.Attachments.Should().ContainSingle().Subject;
        bundle.Kind.Should().Be(EmailAttachmentKind.Composite);
        bundle.BodySections.Should().ContainSingle(section => section.Content == "inner text");
    }

    [Fact]
    public async Task ParseAsyncKeepsBareMessageRfc822AsEncapsulatedMessage()
    {
        var reader = CreateReader();
        var root = Mixed(new MessagePart
        {
            MimeType = "message/rfc822",
            Parts = [Text("nested body")]
        });

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().BeNull();
        var nested = result.Attachments.Should().ContainSingle().Subject;
        nested.Kind.Should().Be(EmailAttachmentKind.EncapsulatedMessage);
        nested.MediaType.Should().Be("message/rfc822");
        nested.BodySections.Should().ContainSingle(section => section.Content == "nested body");
    }

    [Fact]
    public async Task ParseAsyncTreatsMultipartDigestChildrenWithoutMimeTypeAsEncapsulatedMessages()
    {
        var reader = CreateReader();
        var root = new MessagePart
        {
            MimeType = "multipart/digest",
            Parts =
            [
                new MessagePart { Parts = [Text("first nested body")] },
                new MessagePart { Parts = [Text("second nested body")] }
            ]
        };

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().BeNull();
        result.Attachments.Should().HaveCount(2);
        result.Attachments.Should().OnlyContain(attachment => attachment.Kind == EmailAttachmentKind.EncapsulatedMessage);
        result.Attachments.SelectMany(attachment => attachment.BodySections).Select(section => section.Content)
            .Should().Equal("first nested body", "second nested body");
    }

    [Fact]
    public async Task ParseAsyncPreservesMultipleInlineTextSectionsInOrder()
    {
        var reader = CreateReader();
        var root = Mixed(
            Text("first"),
            new MessagePart
            {
                MimeType = "image/png",
                Headers = [Header("Content-ID", "<logo>")],
                Body = new MessagePartBody { Data = Encode("png"), Size = 3 }
            },
            Text("second"));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("first");
        result.BodySections.Select(section => section.Content).Should().Equal("first", "second");
        result.Attachments.Should().ContainSingle(attachment => attachment.Kind == EmailAttachmentKind.InlineResource);
    }

    [Fact]
    public async Task ParseAsyncKeepsMultipartAlternativeAsBodyRepresentations()
    {
        var reader = CreateReader();
        var root = Mixed(
            Alternative(
                Text("plain"),
                Text("<b>html</b>", "text/html")),
            Attachment("receipt.pdf", "application/pdf", "pdf"));

        var result = await reader.ParseAsync("message-1", root);

        result.TextBody.Should().Be("plain");
        result.HtmlBody.Should().Be("<b>html</b>");
        result.BodySections.Select(section => section.MediaType).Should().Equal("text/plain", "text/html");
        result.Attachments.Should().ContainSingle(attachment => attachment.FileName == "receipt.pdf");
    }

    [Fact]
    public async Task ParseAsyncUsesGooglePartIdForProviderIdentity()
    {
        var reader = CreateReader(new GmailContentLimitsOptions { MaxEmbeddedAttachmentBytes = 4 });
        var root = Mixed(new MessagePart
        {
            PartId = "gmail-part-1",
            MimeType = "application/pdf",
            Filename = "statement.pdf",
            Headers = [Header("Content-Disposition", "attachment; filename=\"statement.pdf\"")],
            Body = new MessagePartBody
            {
                Data = Encode("larger"),
                Size = 6
            }
        });

        var result = await reader.ParseAsync("message-1", root);

        result.Attachments.Should().ContainSingle().Which.ProviderPartId.Should().Be("gmail-part-1");
    }

    private static GmailMessagePartReader CreateReader(GmailContentLimitsOptions? limits = null) =>
        new(
            new FakeMessageClient(),
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

    private static MessagePart Text(string value, string mimeType = "text/plain") =>
        new()
        {
            MimeType = mimeType,
            Headers = [Header("Content-Type", $"{mimeType}; charset=utf-8")],
            Body = new MessagePartBody
            {
                Data = Encode(value),
                Size = Encoding.UTF8.GetByteCount(value)
            }
        };

    private static MessagePart Attachment(string fileName, string mimeType, string value) =>
        new()
        {
            MimeType = mimeType,
            Filename = fileName,
            Headers = [Header("Content-Disposition", $"attachment; filename=\"{fileName}\"")],
            Body = new MessagePartBody
            {
                Data = Encode(value),
                Size = Encoding.UTF8.GetByteCount(value)
            }
        };

    private static MessagePartHeader Header(string name, string value) =>
        new()
        {
            Name = name,
            Value = value
        };

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class FakeMessageClient : IGmailMessageClient
    {
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
            throw new NotSupportedException();
    }
}
