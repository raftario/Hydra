using Microsoft.Extensions.Primitives;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Hydra
{
    /// <summary>
    /// A read only version of <see cref="HttpHeaders"/>
    /// </summary>
    public class ReadOnlyHttpHeaders : IReadOnlyDictionary<string, StringValues>
    {
        internal readonly HttpHeaders inner = new();

        public ReadOnlyHttpHeaders() { }
        protected ReadOnlyHttpHeaders(ReadOnlyHttpHeaders other)
        {
            inner = other.inner;
        }

        public StringValues this[string name] => inner[name];

        public IEnumerable<string> Keys => inner.Keys;
        public IEnumerable<StringValues> Values => inner.Values;

        public int Count => inner.Count;

        public bool ContainsKey(string name) => inner.ContainsKey(name);

        public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();

        public bool TryGetValue(string name, [MaybeNullWhen(false)] out StringValues values) => inner.TryGetValue(name, out values);
    }
}
