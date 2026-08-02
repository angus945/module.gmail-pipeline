using FluentAssertions;
using GmailPipeline.Core.Exceptions;
using GmailPipeline.Google.Authentication;

namespace GmailPipeline.Google.Test.Unit.Authentication;

public sealed class ProtectedFileDataStoreTests
{
    [Fact]
    public async Task StoreAndGetRoundTripsWithinNamespaceOnly()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new ProtectedFileDataStore(directory, "namespace-a");
        var otherScopeStore = new ProtectedFileDataStore(directory, "namespace-b");

        await store.StoreAsync("token", new TestToken("secret"));

        var token = await store.GetAsync<TestToken>("token");
        var otherScopeToken = await otherScopeStore.GetAsync<TestToken>("token");

        token.Should().Be(new TestToken("secret"));
        otherScopeToken.Should().BeNull();
        Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsyncReportsCorruptTokenAsCredentialStoreException()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new ProtectedFileDataStore(directory, "namespace-a");
        await store.StoreAsync("token", new TestToken("secret"));
        var tokenPath = Directory.EnumerateFiles(directory, "*.bin").Single();
        await File.WriteAllTextAsync(tokenPath, "not-json");

        var act = async () => await store.GetAsync<TestToken>("token");

        await act.Should().ThrowAsync<EmailCredentialStoreException>();
    }

    private sealed record TestToken(string Value);
}
