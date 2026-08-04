using System.Net.Mail;
using GmailPipeline.Core.Contract.Models;

namespace GmailPipeline.Google.Infrastructure.Mapping;

public static class GmailAddressParser
{
    public static EmailAddress? ParseSingle(string? value) =>
        ParseMany(value).FirstOrDefault();

    public static IReadOnlyList<EmailAddress> ParseMany(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SplitAddressList(value)
            .Select(ParseAddress)
            .Where(address => address is not null)
            .Select(address => address!)
            .ToArray();
    }

    private static EmailAddress? ParseAddress(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return new EmailAddress(address.Address, string.IsNullOrWhiteSpace(address.DisplayName) ? null : address.DisplayName);
        }
        catch (FormatException)
        {
            return new EmailAddress(value.Trim());
        }
    }

    private static IEnumerable<string> SplitAddressList(string value)
    {
        var start = 0;
        var inQuotes = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (value[index] == ',' && !inQuotes)
            {
                yield return value[start..index].Trim();
                start = index + 1;
            }
        }

        yield return value[start..].Trim();
    }
}
