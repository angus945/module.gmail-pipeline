namespace GmailPipeline.Core.Models;

public sealed class EmailHeaderCollection : IReadOnlyDictionary<string, string>
{
    private readonly IReadOnlyDictionary<string, string> _headers;

    public EmailHeaderCollection(IEnumerable<KeyValuePair<string, string>> headers)
    {
        _headers = headers
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    public string this[string key] => _headers[key];

    public IEnumerable<string> Keys => _headers.Keys;

    public IEnumerable<string> Values => _headers.Values;

    public int Count => _headers.Count;

    public bool ContainsKey(string key) => _headers.ContainsKey(key);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _headers.GetEnumerator();

    public bool TryGetValue(string key, out string value) => _headers.TryGetValue(key, out value!);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
