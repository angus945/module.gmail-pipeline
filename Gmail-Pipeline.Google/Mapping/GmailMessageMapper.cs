using GmailPipeline.Core.Models;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1.Data;

namespace GmailPipeline.Google.Mapping;

public sealed class GmailMessageMapper : IGmailMessageMapper
{
    private readonly GmailMimeParser _mimeParser;

    public GmailMessageMapper(GmailMimeParser mimeParser)
    {
        _mimeParser = mimeParser;
    }

    public EmailMessage Map(Message message)
    {
        var headers = new EmailHeaderCollection((message.Payload?.Headers ?? [])
            .Where(header => !string.IsNullOrWhiteSpace(header.Name))
            .Select(header => new KeyValuePair<string, string>(header.Name, header.Value ?? string.Empty)));

        var parsedMime = _mimeParser.Parse(message);
        var receivedAt = message.InternalDate is null
            ? (DateTimeOffset?)null
            : DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(message.InternalDate, System.Globalization.CultureInfo.InvariantCulture));
        headers.TryGetValue("Date", out var dateHeader);

        return new EmailMessage
        {
            Id = message.Id,
            ThreadId = message.ThreadId ?? string.Empty,
            Subject = GetHeader(headers, "Subject"),
            From = GmailAddressParser.ParseSingle(GetHeader(headers, "From")),
            To = GmailAddressParser.ParseMany(GetHeader(headers, "To")),
            Cc = GmailAddressParser.ParseMany(GetHeader(headers, "Cc")),
            Bcc = GmailAddressParser.ParseMany(GetHeader(headers, "Bcc")),
            SentAt = GmailDateParser.ParseHeaderDate(dateHeader),
            ReceivedAt = receivedAt,
            TextBody = parsedMime.TextBody,
            HtmlBody = parsedMime.HtmlBody,
            Attachments = parsedMime.Attachments,
            Headers = headers,
            LabelIds = message.LabelIds?.ToArray() ?? []
        };
    }

    private static string? GetHeader(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : null;
}
