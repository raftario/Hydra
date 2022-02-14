using Microsoft.Extensions.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace HydraHttp
{
    /// <summary>
    /// A collection of case-insensitive names associated with a list of values
    /// </summary>
    public class HttpHeaders : IDictionary<string, StringValues>
    {
        private readonly Dictionary<string, StringValues> dictionary = new(StringComparer.OrdinalIgnoreCase);
        private ICollection<KeyValuePair<string, StringValues>> Collection => dictionary;

        public StringValues this[string name] { get => dictionary[name]; set => dictionary[name] = value; }

        public ICollection<string> Keys => dictionary.Keys;
        public ICollection<StringValues> Values => dictionary.Values;

        public int Count => dictionary.Count;

        public bool IsReadOnly => false;

        public void Add(string name, StringValues values)
        {
            if (dictionary.TryGetValue(name, out var existing)) dictionary[name] = StringValues.Concat(existing, values);
            else dictionary.Add(name, values);
        }
        public void Add(KeyValuePair<string, StringValues> header) => Add(header.Key, header.Value);

        public void Clear() => dictionary.Clear();

        public bool Contains(KeyValuePair<string, StringValues> header) =>
            dictionary.TryGetValue(header.Key, out var existing) && (existing == header.Value || existing.ToString().Contains(header.Value));
        public bool ContainsKey(string name) => dictionary.ContainsKey(name);

        public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex) => Collection.CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => dictionary.GetEnumerator();

        public bool Remove(string name) => dictionary.Remove(name);
        public bool Remove(KeyValuePair<string, StringValues> header)
        {
            if (!dictionary.TryGetValue(header.Key, out var existing)) return false;
            if (existing == header.Value) return dictionary.Remove(header.Key);
            dictionary[header.Key] = existing.Except(header.Value).ToArray();
            return true;
        }

        public bool TryGetValue(string name, [MaybeNullWhen(false)] out StringValues values) => dictionary.TryGetValue(name, out values);
    }
}
