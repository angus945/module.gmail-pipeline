using System.Text;
using GmailPipeline.Core.Contract.Exceptions;

namespace GmailPipeline.Google.Infrastructure.Mime;

public sealed class DefaultEmailCharsetResolver : IEmailCharsetResolver
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static DefaultEmailCharsetResolver()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Encoding Resolve(string? charset, string resource)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return StrictUtf8;
        }

        try
        {
            return Encoding.GetEncoding(
                charset.Trim().Trim('"'),
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
        catch (ArgumentException exception)
        {
            throw new EmailContentFormatException($"Unsupported charset '{charset}' for {resource}.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new EmailContentFormatException($"Unsupported charset '{charset}' for {resource}.", exception);
        }
    }
}
