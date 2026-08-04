using System.Buffers;
using GmailPipeline.Core.Contract.Exceptions;

namespace GmailPipeline.Google.Infrastructure.Mime;

public static class Base64UrlDecoder
{
    public static byte[] Decode(
        string value,
        string resource = "Base64URL content",
        long? maxDecodedBytes = null)
    {
        if (value.Length == 0)
        {
            return [];
        }

        var padding = (4 - value.Length % 4) % 4;
        var charCount = value.Length + padding;
        var maxByteCount = charCount / 4 * 3;
        if (maxDecodedBytes is not null && maxByteCount > maxDecodedBytes.Value + 2)
        {
            throw new EmailResourceLimitException(resource, maxByteCount, maxDecodedBytes.Value);
        }

        var chars = ArrayPool<char>.Shared.Rent(charCount);
        var bytes = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            for (var index = 0; index < value.Length; index++)
            {
                chars[index] = value[index] switch
                {
                    '-' => '+',
                    '_' => '/',
                    var character => character
                };
            }

            for (var index = value.Length; index < charCount; index++)
            {
                chars[index] = '=';
            }

            if (!Convert.TryFromBase64Chars(
                    chars.AsSpan(0, charCount),
                    bytes,
                    out var bytesWritten))
            {
                throw new FormatException("Invalid Base64URL content.");
            }

            if (maxDecodedBytes is not null && bytesWritten > maxDecodedBytes.Value)
            {
                throw new EmailResourceLimitException(resource, bytesWritten, maxDecodedBytes.Value);
            }

            return bytes.AsSpan(0, bytesWritten).ToArray();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars, clearArray: true);
            ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
        }
    }

    public static string DecodeUtf8(
        string value,
        string resource = "Base64URL text content",
        long? maxDecodedBytes = null) =>
        System.Text.Encoding.UTF8.GetString(Decode(value, resource, maxDecodedBytes));
}
