using System.Buffers;
using System.Text;

namespace HydraHttp.Core.Tests
{
    internal static class Util
    {
        internal static byte[] AsBytes(this string s, Encoding? encoding = null) =>
            (encoding ?? Encoding.ASCII).GetBytes(s);
        internal static ReadOnlySequence<byte> AsReadonlySequence(this string s, Encoding? encoding = null) =>
            new ReadOnlySequence<byte>(s.AsBytes(encoding));
    }
}
