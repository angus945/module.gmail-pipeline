using FluentAssertions;
using GmailPipeline.Core.Contract.Exceptions;
using GmailPipeline.Google.Infrastructure.Authentication;

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
        Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsyncOnlyClearsCurrentNamespace()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var storeA = new ProtectedFileDataStore(directory, "namespace-a");
        var storeB = new ProtectedFileDataStore(directory, "namespace-b");

        await storeA.StoreAsync("token", new TestToken("a"));
        await storeB.StoreAsync("token", new TestToken("b"));

        await storeA.ClearAsync();

        (await storeA.GetAsync<TestToken>("token")).Should().BeNull();
        (await storeB.GetAsync<TestToken>("token")).Should().Be(new TestToken("b"));
    }

    [Fact]
    public async Task SameTextKeyForDifferentTypesDoesNotCollide()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new ProtectedFileDataStore(directory, "namespace-a");

        await store.StoreAsync("same", new TestToken("token"));
        await store.StoreAsync("same", new AlternateToken("alternate"));

        (await store.GetAsync<TestToken>("same")).Should().Be(new TestToken("token"));
        (await store.GetAsync<AlternateToken>("same")).Should().Be(new AlternateToken("alternate"));
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
        var tokenPath = Directory.EnumerateFiles(directory, "*.bin", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(tokenPath, "not-json");

        var act = async () => await store.GetAsync<TestToken>("token");

        await act.Should().ThrowAsync<EmailCredentialStoreException>();
    }

    private sealed record TestToken(string Value);

    private sealed record AlternateToken(string Value);
}
