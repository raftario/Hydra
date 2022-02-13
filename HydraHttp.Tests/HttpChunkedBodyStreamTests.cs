using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtils;

namespace HydraHttp.Tests
{
    [TestClass]
    public class HttpChunkedBodyStreamTests
    {
        private MemoryStream stream;
        private HttpChunkedBodyStream bodyStream;

        public HttpChunkedBodyStreamTests()
        {
            stream = new();
            bodyStream = new(stream);
        }

        [TestMethod]
        public void Decodes()
        {
            var encoded =
                "3\r\n" + "oh \r\n" +
                "3\r\n" + "hi \r\n" +
                "4\r\n" + "mark\r\n" +
                "0\r\n" + "\r\n";

            stream.Write(encoded.AsBytes());
            stream.Position = 0;

            Assert.AreEqual("oh hi mark", bodyStream.AsText());
        }

        [TestMethod]
        public void DecodesHeaders()
        {
            var encoded =
                "3\r\n" + "oh \r\n" +
                "3\r\n" + "hi \r\n" +
                "4\r\n" + "mark\r\n" +
                "0\r\n" + "X-Movie: The Room\r\n" +
                "\r\n";

            stream.Write(encoded.AsBytes());
            stream.Position = 0;

            Assert.AreEqual("oh hi mark", bodyStream.AsText());
            Assert.AreEqual("The Room", bodyStream.Headers["X-Movie"].ToString());
        }
    }
}
