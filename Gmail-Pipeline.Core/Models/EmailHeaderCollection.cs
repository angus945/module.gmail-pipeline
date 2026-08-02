namespace GmailPipeline.Core.Models;

public sealed class EmailHeaderCollection : IReadOnlyDictionary<string, string>
{
    private readonly IReadOnlyList<KeyValuePair<string, string>> _headers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _valuesByName;

    public EmailHeaderCollection(IEnumerable<KeyValuePair<string, string>> headers)
    {
        _headers = headers.ToArray();
        _valuesByName = _headers
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(header => header.Value).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public string this[string key] => GetFirstOrDefault(key) ?? throw new KeyNotFoundException($"Header '{key}' was not found.");

    public IEnumerable<string> Keys => _valuesByName.Keys;

    public IEnumerable<string> Values => _valuesByName.Values.Select(values => values[0]);

    public int Count => _valuesByName.Count;

    public bool ContainsKey(string key) => _valuesByName.ContainsKey(key);

    public IReadOnlyList<string> GetValues(string name) =>
        _valuesByName.TryGetValue(name, out var values) ? values : [];

    public string? GetFirstOrDefault(string name) =>
        GetValues(name).FirstOrDefault();

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
        _valuesByName.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value[0])).GetEnumerator();

    public bool TryGetValue(string key, out string value)
    {
        value = GetFirstOrDefault(key)!;
        return value is not null;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
