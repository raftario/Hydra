using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;

namespace HydraHttp
{
    public class HttpHeaders : Dictionary<string, StringValues>
    {
        internal HttpHeaders() : base(StringComparer.OrdinalIgnoreCase) { }
    }
}