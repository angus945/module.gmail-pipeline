using System.Net;
using FluentAssertions;
using GmailPipeline.Google.Clients;
using Google;
using Google.Apis.Requests;

namespace GmailPipeline.Google.Test.Unit.Clients;

public sealed class GmailApiRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsyncRetriesForbiddenRateLimitReason()
    {
        var delay = new RecordingDelay();
        var policy = new GmailApiRetryPolicy(delay);
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw CreateGoogleException(HttpStatusCode.Forbidden, "userRateLimitExceeded");
                }

                return Task.FromResult("ok");
            },
            "search messages");

        result.Should().Be("ok");
        attempts.Should().Be(2);
        delay.Delays.Should().ContainSingle();
    }

    [Fact]
    public void ClassifierTreatsBackendErrorAsTransient()
    {
        var classification = GoogleErrorClassifier.Classify(CreateGoogleException(HttpStatusCode.Forbidden, "backendError"));

        classification.IsTransient.Should().BeTrue();
        classification.IsRateLimit.Should().BeFalse();
        classification.Reason.Should().Be("backendError");
    }

    private static GoogleApiException CreateGoogleException(HttpStatusCode statusCode, string reason) =>
        new("gmail", reason)
        {
            HttpStatusCode = statusCode,
            Error = new RequestError
            {
                Errors =
                [
                    new SingleError
                    {
                        Reason = reason
                    }
                ]
            }
        };

    private sealed class RecordingDelay : IGmailRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }
}
