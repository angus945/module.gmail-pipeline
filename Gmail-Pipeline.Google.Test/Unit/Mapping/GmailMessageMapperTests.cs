using FluentAssertions;
using GmailPipeline.Google.Mapping;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1.Data;

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
            LabelIds = ["INBOX"],
            Payload = new MessagePart
            {
                Headers =
                [
                    new MessagePartHeader { Name = "From", Value = "Bank <bank@example.test>" },
                    new MessagePartHeader { Name = "To", Value = "me@example.test" },
                    new MessagePartHeader { Name = "Subject", Value = "Statement" },
                    new MessagePartHeader { Name = "Date", Value = "Sat, 01 Aug 2026 08:00:00 +0000" },
                    new MessagePartHeader { Name = "X-Duplicate", Value = "one" },
                    new MessagePartHeader { Name = "X-Duplicate", Value = "two" }
                ]
            }
        };
        var parsedMime = new GmailMimeParseResult(
            "Statement",
            null,
            [],
            [new GmailPipeline.Core.Models.EmailBodySection
            {
                MediaType = "text/plain",
                Content = "Statement",
                PartPath = "0.0"
            }]);

        var mapped = new GmailMessageMapper().Map(message, parsedMime);

        mapped.Id.Should().Be("message-1");
        mapped.ThreadId.Should().Be("thread-1");
        mapped.From!.Address.Should().Be("bank@example.test");
        mapped.From.DisplayName.Should().Be("Bank");
        mapped.To.Should().ContainSingle(address => address.Address == "me@example.test");
        mapped.Subject.Should().Be("Statement");
        mapped.TextBody.Should().Be("Statement");
        mapped.BodySections.Should().ContainSingle(section => section.PartPath == "0.0");
        mapped.Headers["subject"].Should().Be("Statement");
        mapped.Headers.GetValues("x-duplicate").Should().Equal("one", "two");
        mapped.LabelIds.Should().Equal("INBOX");
    }
}
