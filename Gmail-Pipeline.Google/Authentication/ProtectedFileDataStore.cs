using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Util.Store;

namespace GmailPipeline.Google.Authentication;

public sealed class ProtectedFileDataStore : IDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directoryPath;

    public ProtectedFileDataStore(string directoryPath)
    {
        _directoryPath = directoryPath;
        Directory.CreateDirectory(_directoryPath);
    }

    public Task ClearAsync()
    {
        if (!Directory.Exists(_directoryPath))
        {
            return Task.CompletedTask;
        }

        foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.bin"))
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return default!;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        var path = GetPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(path, protectedBytes).ConfigureAwait(false);
    }

    private string GetPath(string key)
    {
        var safeName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return Path.Combine(_directoryPath, $"{safeName}.bin");
    }
}
