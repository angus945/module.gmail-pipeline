using FluentAssertions;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1.Data;
using MimeKit;
using System.Text;

namespace GmailPipeline.Google.Test.Unit.Mapping;

public sealed class GmailMessageMapperTests
{
    [Fact]
    public void MapConvertsGoogleDtoToCoreEmailMessage()
    {
        var message = new Message
        {
            Id = "message-1",
            ThreadId = "thread-1",
            InternalDate = 1785571200000,
            LabelIds = ["INBOX"]
        };
        var mimeMessage = LoadMime("""
            From: Bank <bank@example.test>
            To: me@example.test
            Subject: Statement
            Date: Sat, 01 Aug 2026 08:00:00 +0000
            X-Duplicate: one
            X-Duplicate: two
            Content-Type: text/plain; charset=utf-8

            Statement
            """);

        var mapped = new GmailMessageMapper(new GmailMimeParser()).Map(message, mimeMessage);

        mapped.Id.Should().Be("message-1");
        mapped.ThreadId.Should().Be("thread-1");
        mapped.From!.Address.Should().Be("bank@example.test");
        mapped.From.DisplayName.Should().Be("Bank");
        mapped.To.Should().ContainSingle(address => address.Address == "me@example.test");
        mapped.Subject.Should().Be("Statement");
        mapped.TextBody.Should().Be("Statement");
        mapped.Headers["subject"].Should().Be("Statement");
        mapped.Headers.GetValues("x-duplicate").Should().Equal("one", "two");
        mapped.LabelIds.Should().Equal("INBOX");
    }

    private static MimeMessage LoadMime(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.ReplaceLineEndings("\r\n"));
        using var stream = new MemoryStream(bytes);
        return MimeMessage.Load(stream);
    }
}
