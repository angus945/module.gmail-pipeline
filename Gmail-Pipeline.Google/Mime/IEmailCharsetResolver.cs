using System.Text;

namespace GmailPipeline.Google.Mime;

public interface IEmailCharsetResolver
{
    Encoding Resolve(string? charset, string resource);
}
