using FluentAssertions;
using GmailPipeline.Core.Contract.Parsing;

namespace GmailPipeline.Core.Test.Parsing;

public sealed class EmailParseResultTests
{
    [Fact]
    public void SucceededExposesValueAndWarningsOnly()
    {
        var result = EmailParseResult<string>.Succeeded("value", ["warning"]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value");
        result.Warnings.Should().Equal("warning");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FailedExposesErrorsOnly()
    {
        var error = new EmailParseError("Code", "Message");

        var result = EmailParseResult<string>.Failed(error);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Errors.Should().Equal(error);
        result.Warnings.Should().BeEmpty();
    }
}
