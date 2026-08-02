using FluentAssertions;
using GmailPipeline.Core.Models;

namespace GmailPipeline.Core.Test.Models;

public sealed class EmailHeaderCollectionTests
{
    [Fact]
    public void GetValuesPreservesDuplicateHeaders()
    {
        var headers = new EmailHeaderCollection(
        [
            new KeyValuePair<string, string>("Received", "first"),
            new KeyValuePair<string, string>("received", "second"),
            new KeyValuePair<string, string>("Subject", "Statement")
        ]);

        headers.GetValues("RECEIVED").Should().Equal("first", "second");
        headers.GetFirstOrDefault("received").Should().Be("first");
        headers["subject"].Should().Be("Statement");
        headers.Count.Should().Be(2);
    }
}
