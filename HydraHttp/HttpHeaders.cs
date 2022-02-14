using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace HydraHttp
{
    /// <summary>
    /// A collection of case-insensitive names associated with a list of values
    /// </summary>
    public class HttpHeaders : Dictionary<string, StringValues>
    {
        internal HttpHeaders() : base(StringComparer.OrdinalIgnoreCase) { }
    }
}