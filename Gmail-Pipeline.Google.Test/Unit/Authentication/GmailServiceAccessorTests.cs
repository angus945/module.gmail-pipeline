using FluentAssertions;
using GmailPipeline.Google.Authentication;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;

namespace GmailPipeline.Google.Test.Unit.Authentication;

public sealed class GmailServiceAccessorTests
{
    [Fact]
    public async Task GetAsyncInitializesServiceOnceAcrossConcurrentCallers()
    {
        var factory = new CountingFactory();
        using var accessor = new GmailServiceAccessor(factory);

        var services = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => accessor.GetAsync()));

        factory.CreateCount.Should().Be(1);
        services.Distinct().Should().ContainSingle();
    }

    private sealed class CountingFactory : IGmailServiceFactory
    {
        public int CreateCount { get; private set; }

        public async Task<GmailService> CreateAsync(CancellationToken cancellationToken = default)
        {
            CreateCount++;
            await Task.Delay(10, cancellationToken);
            return new GmailService(new BaseClientService.Initializer
            {
                ApplicationName = "test"
            });
        }
    }
}
