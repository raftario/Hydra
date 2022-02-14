using System.Buffers;
using System.IO;
using System.Text;

namespace TestUtils
{
    public static class Utils
    {
        public static byte[] AsBytes(this string s, Encoding? encoding = null) =>
            (encoding ?? Encoding.ASCII).GetBytes(s);
        public static ReadOnlySequence<byte> AsReadonlySequence(this string s, Encoding? encoding = null) =>
            new(s.AsBytes(encoding));
        public static Stream AsStream(this string s, Encoding? encoding = null) =>
            new MemoryStream(s.AsBytes(encoding));
        public static string AsText(this Stream stream, Encoding? encoding = null) =>
            new StreamReader(stream, encoding ?? Encoding.ASCII).ReadToEnd();
    }
}
