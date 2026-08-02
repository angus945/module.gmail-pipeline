using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using GmailPipeline.Core.Exceptions;
using Google.Apis.Util.Store;
using System.Collections.Concurrent;

namespace GmailPipeline.Google.Authentication;

public sealed class ProtectedFileDataStore : IDataStore
{
    private const int SchemaVersion = 2;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directoryPath;
    private readonly string _keyNamespace;

    public ProtectedFileDataStore(string directoryPath, string keyNamespace)
    {
        _directoryPath = directoryPath;
        _keyNamespace = keyNamespace;
        Directory.CreateDirectory(_directoryPath);
    }

    public async Task ClearAsync()
    {
        if (!Directory.Exists(_directoryPath))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(_directoryPath, "*.bin"))
            {
                var gate = GetGate(file);
                await gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    File.Delete(file);
                }
                finally
                {
                    gate.Release();
                }
            }
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            throw new EmailCredentialStoreException("Failed to clear Gmail token store.", exception);
        }
    }

    public async Task DeleteAsync<T>(string key)
    {
        var path = GetPath(key);
        var gate = GetGate(path);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            throw new EmailCredentialStoreException("Failed to delete Gmail token.", exception);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var path = GetPath(key);
        var gate = GetGate(path);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return default!;
            }

            var envelopeBytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            var envelope = JsonSerializer.Deserialize<TokenEnvelope>(envelopeBytes, JsonOptions)
                ?? throw new JsonException("Token envelope was empty.");
            if (envelope.SchemaVersion != SchemaVersion || !string.Equals(envelope.KeyNamespace, _keyNamespace, StringComparison.Ordinal))
            {
                return default!;
            }

            var protectedBytes = Convert.FromBase64String(envelope.ProtectedPayload);
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Windows DPAPI token protection is only available on Windows.");
            }

            var bytes = Unprotect(protectedBytes);
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            throw new EmailCredentialStoreException(
                "Failed to read Gmail token. Delete the token store entry and authorize again if the token is corrupt.",
                exception);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        var path = GetPath(key);
        var gate = GetGate(path);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Windows DPAPI token protection is only available on Windows.");
            }

            var protectedBytes = Protect(bytes);
            var envelope = new TokenEnvelope(
                SchemaVersion,
                _keyNamespace,
                Convert.ToBase64String(protectedBytes));
            var envelopeBytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(tempPath, envelopeBytes).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception exception) when (IsStoreException(exception))
        {
            throw new EmailCredentialStoreException("Failed to store Gmail token.", exception);
        }
        finally
        {
            gate.Release();
        }
    }

    private string GetPath(string key)
    {
        var scopedKey = $"{_keyNamespace}\n{key}";
        var safeName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scopedKey))).ToLowerInvariant();
        return Path.Combine(_directoryPath, $"{safeName}.bin");
    }

    private static SemaphoreSlim GetGate(string path) =>
        Gates.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

    private static bool IsStoreException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or CryptographicException
            or JsonException
            or FormatException
            or PlatformNotSupportedException;

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] bytes) =>
        ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] protectedBytes) =>
        ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

    private sealed record TokenEnvelope(
        int SchemaVersion,
        string KeyNamespace,
        string ProtectedPayload);
}
