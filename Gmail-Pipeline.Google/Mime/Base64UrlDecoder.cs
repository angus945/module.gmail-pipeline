namespace GmailPipeline.Google.Mime;

public static class Base64UrlDecoder
{
    public static byte[] Decode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Convert.FromBase64String(normalized);
    }

    public static string DecodeUtf8(string value) =>
        System.Text.Encoding.UTF8.GetString(Decode(value));
}
