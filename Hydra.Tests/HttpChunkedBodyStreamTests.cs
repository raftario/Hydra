using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtils;

namespace Hydra.Tests
{
    [TestClass]
    public class HttpChunkedBodyStreamTests
    {
        private readonly MemoryStream stream = new();
        private readonly HttpHeaders headers = new();
        private readonly HttpChunkedBodyStream bodyStream;

        public HttpChunkedBodyStreamTests()
        {
            bodyStream = new(stream, headers);
        }

        [TestMethod]
        public void Decodes()
        {
            string encoded =
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
            string encoded =
                "3\r\n" + "oh \r\n" +
                "3\r\n" + "hi \r\n" +
                "4\r\n" + "mark\r\n" +
                "0\r\n" + "X-Movie: The Room\r\n" +
                "\r\n";

            stream.Write(encoded.AsBytes());
            stream.Position = 0;

            Assert.AreEqual("oh hi mark", bodyStream.AsText());
            Assert.AreEqual("The Room", headers["X-Movie"].ToString());
        }
    }
}
