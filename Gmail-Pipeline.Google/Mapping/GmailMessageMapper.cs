using GmailPipeline.Core.Models;
using GmailPipeline.Google.Mime;
using Google.Apis.Gmail.v1.Data;
using MimeKit;

namespace GmailPipeline.Google.Mapping;

public sealed class GmailMessageMapper : IGmailMessageMapper
{
    private readonly GmailMimeParser _mimeParser;

    public GmailMessageMapper(GmailMimeParser mimeParser)
    {
        _mimeParser = mimeParser;
    }

    public EmailMessage Map(Message message, MimeMessage mimeMessage)
    {
        var headers = new EmailHeaderCollection(mimeMessage.Headers
            .Where(header => !string.IsNullOrWhiteSpace(header.Field))
            .Select(header => new KeyValuePair<string, string>(header.Field, header.Value ?? string.Empty)));

        var parsedMime = _mimeParser.Parse(mimeMessage);
        var receivedAt = message.InternalDate is null
            ? (DateTimeOffset?)null
            : DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(message.InternalDate, System.Globalization.CultureInfo.InvariantCulture));

        return new EmailMessage
        {
            Id = message.Id,
            ThreadId = message.ThreadId ?? string.Empty,
            Subject = mimeMessage.Subject,
            From = ToEmailAddress(mimeMessage.From.Mailboxes.FirstOrDefault()),
            To = ToEmailAddresses(mimeMessage.To),
            Cc = ToEmailAddresses(mimeMessage.Cc),
            Bcc = ToEmailAddresses(mimeMessage.Bcc),
            SentAt = mimeMessage.Date == default ? null : mimeMessage.Date,
            ReceivedAt = receivedAt,
            TextBody = parsedMime.TextBody,
            HtmlBody = parsedMime.HtmlBody,
            Attachments = parsedMime.Attachments,
            Headers = headers,
            LabelIds = message.LabelIds?.ToArray() ?? []
        };
    }

    private static IReadOnlyList<EmailAddress> ToEmailAddresses(InternetAddressList addresses) =>
        addresses.Mailboxes.Select(mailbox => ToEmailAddress(mailbox)!).ToArray();

    private static EmailAddress? ToEmailAddress(MailboxAddress? address) =>
        address is null
            ? null
            : new EmailAddress(
                address.Address,
                string.IsNullOrWhiteSpace(address.Name) ? null : address.Name);
}
