using System.Buffers;
using System.IO;
using System.Text;

namespace HydraHttp.OneDotOne.Tests
{
    internal static class Util
    {
        internal static byte[] AsBytes(this string s, Encoding? encoding = null) =>
            (encoding ?? Encoding.ASCII).GetBytes(s);
        internal static ReadOnlySequence<byte> AsReadonlySequence(this string s, Encoding? encoding = null) =>
            new ReadOnlySequence<byte>(s.AsBytes(encoding));
        internal static Stream AsStream(this string s, Encoding? encoding = null) =>
            new MemoryStream(s.AsBytes(encoding));
        internal static string AsText(this Stream stream, Encoding? encoding = null) =>
            new StreamReader(stream, encoding ?? Encoding.ASCII).ReadToEnd();
    }
}
