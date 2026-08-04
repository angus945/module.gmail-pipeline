using System.Text;

namespace GmailPipeline.Google.Infrastructure.Mime;

public interface IEmailCharsetResolver
{
    Encoding Resolve(string? charset, string resource);
}
