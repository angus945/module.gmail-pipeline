using System.Net;
using FluentAssertions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Google.Exceptions;
using Google;
using Google.Apis.Requests;

namespace GmailPipeline.Google.Test.Unit.Exceptions;

public sealed class GoogleExceptionMapperTests
{
    [Fact]
    public void MapReturnsRateLimitExceptionForForbiddenRateLimitReason()
    {
        var exception = new GoogleApiException("gmail", "rate limit")
        {
            HttpStatusCode = HttpStatusCode.Forbidden,
            Error = new RequestError
            {
                Errors =
                [
                    new SingleError
                    {
                        Reason = "rateLimitExceeded"
                    }
                ]
            }
        };

        var mapped = GoogleExceptionMapper.Map(exception, "search messages")
            .Should()
            .BeOfType<EmailRateLimitException>()
            .Subject;

        mapped.Operation.Should().Be("search messages");
        mapped.Reason.Should().Be("rateLimitExceeded");
    }

    [Fact]
    public void CanMapIncludesCredentialAndContentFormatInfrastructureExceptions()
    {
        GoogleExceptionMapper.CanMap(new FileNotFoundException()).Should().BeTrue();
        GoogleExceptionMapper.CanMap(new System.Text.Json.JsonException()).Should().BeTrue();
        GoogleExceptionMapper.CanMap(new FormatException()).Should().BeTrue();
    }
}
