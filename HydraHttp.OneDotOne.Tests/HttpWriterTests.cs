using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using TestUtils;

namespace HydraHttp.OneDotOne.Tests
{
    [TestClass]
    public class HttpWriterTests
    {
        private MemoryStream stream;
        private HttpWriter writer;

        public HttpWriterTests()
        {
            stream = new();
            writer = new(PipeWriter.Create(stream));
        }

        [TestMethod]
        public async Task Write()
        {
            var body = "{ \"name\": \"Hydra\" }";
            writer.WriteStatusLine(new(200, "OK"));
            writer.WriteHeader(new("Content-Type", "application/json; charset=utf-8"));
            writer.WriteHeader(new("Content-Length", body.Length.ToString()));
            await writer.Send(body.AsStream());

            var expected =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "\r\n" + body;

            stream.Position = 0;
            Assert.AreEqual(expected, stream.AsText(Encoding.UTF8));
        }
    }
}
