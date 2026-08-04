namespace GmailPipeline.Google.Infrastructure.Mapping;

public static class GmailDateParser
{
    public static DateTimeOffset? ParseHeaderDate(string? value) =>
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
