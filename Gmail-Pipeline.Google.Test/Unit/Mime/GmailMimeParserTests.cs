using FluentAssertions;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Test.Unit.Mime;

public sealed class GmailMimeParserTests
{
    [Fact]
    public void ParseExtractsBodiesAndStableAttachmentIds()
    {
        var message = new Message
        {
            Payload = new MessagePart
            {
                MimeType = "multipart/mixed",
                Parts =
                [
                    new MessagePart
                    {
                        MimeType = "multipart/alternative",
                        Parts =
                        [
                            new MessagePart
                            {
                                MimeType = "text/plain",
                                Body = new MessagePartBody { Data = "SGVsbG8" }
                            },
                            new MessagePart
                            {
                                MimeType = "text/html",
                                Body = new MessagePartBody { Data = "PGI-SGVsbG88L2I-" }
                            }
                        ]
                    },
                    new MessagePart
                    {
                        Filename = "statement.zip",
                        MimeType = "application/zip",
                        Body = new MessagePartBody
                        {
                            AttachmentId = "gmail-changing-id",
                            Size = 123
                        }
                    }
                ]
            }
        };

        var result = new GmailMimeParser().Parse(message);

        result.TextBody.Should().Be("Hello");
        result.HtmlBody.Should().Be("<b>Hello</b>");
        result.Attachments.Should().ContainSingle();
        result.Attachments[0].Id.Should().Be("0.1");
        result.Attachments[0].ProviderAttachmentId.Should().Be("gmail-changing-id");
        result.Attachments[0].FileName.Should().Be("statement.zip");
    }

    [Fact]
    public void ParseIgnoresProviderAttachmentIdsWithoutFileNames()
    {
        var message = new Message
        {
            Payload = new MessagePart
            {
                Parts =
                [
                    new MessagePart
                    {
                        MimeType = "text/html",
                        Body = new MessagePartBody
                        {
                            AttachmentId = "body-1",
                            Size = 6127
                        }
                    }
                ]
            }
        };

        new GmailMimeParser().Parse(message).Attachments.Should().BeEmpty();
    }

    [Fact]
    public void Base64UrlDecoderHandlesMissingPaddingAndUrlAlphabet()
    {
        Base64UrlDecoder.DecodeUtf8("SGVsbG8td29ybGQ").Should().Be("Hello-world");
    }
}
