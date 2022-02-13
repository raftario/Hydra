using HydraHttp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydraHttp.OneDotOne
{
    public enum Status
    {
        Complete,
        Incomplete,
        Finished,
    }
    public readonly record struct Result<T>(Status Status, T? Value = null) where T : struct
    {
        public bool Complete([NotNullWhen(true)] out T? value)
        {
            value = Value;
            return Status == Status.Complete;
        }
        public bool Incomplete => Status == Status.Incomplete;
        public bool Finished => Status == Status.Finished;
    }

    public readonly record struct StartLine(string Method, string Uri, int Version);
    public readonly record struct StatusLine(int Status, string Reason);
    public readonly record struct Header(string Name, string Value);
}
