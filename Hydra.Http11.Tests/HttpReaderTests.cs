using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using TestUtils;

namespace Hydra.Http11.Tests
{
    [TestClass]
    public class HttpReaderTests
    {
        private readonly MemoryStream stream = new();
        private readonly HttpReader reader;

        public HttpReaderTests()
        {
            reader = new(PipeReader.Create(stream));
        }

        [TestMethod]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r\n", "POST", "/api/songs/1?locale=en", HttpVersion.Http11)]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.0\r\n", "POST", "/api/songs/1?locale=en", HttpVersion.Http10)]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\n", "POST", "/api/songs/1?locale=en", HttpVersion.Http11)]
        [DataRow("\r\nPOST /api/songs/1?locale=en HTTP/1.1\r\n", "POST", "/api/songs/1?locale=en", HttpVersion.Http11)]
        [DataRow("\nPOST /api/songs/1?locale=en HTTP/1.1\r\n", "POST", "/api/songs/1?locale=en", HttpVersion.Http11)]
        public async Task ReadStartLine_Complete(string startLine, string expectedMethod, string expectedUri, HttpVersion expectedVersion)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadStartLine();
            Assert.IsTrue(result.Complete(out var value));

            Assert.AreEqual(expectedMethod, value!.Value.Method);
            Assert.AreEqual(expectedUri, value!.Value.Uri);
            Assert.AreEqual(expectedVersion, value!.Value.Version);
        }

        [TestMethod]
        [DataRow("POS")]
        [DataRow("POST /api/songs/1?locale=e")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r")]
        public async Task ReadStartLine_Incomplete(string startLine)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadStartLine();
            Assert.IsTrue(result.Incomplete);
        }

        [TestMethod]
        [DataRow("POS\t /api/songs/1?locale=en HTTP/1.1\r\n")]
        public void ReadStartLine_InvalidMethod(string startLine)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidTokenException>(() => reader.ReadStartLine().AsTask());
        }

        [TestMethod]
        [DataRow("POST /api/so\ngs/1?locale=en HTTP/1.1\r\n")]
        public void ReadStartLine_InvalidUri(string startLine)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidUriException>(() => reader.ReadStartLine().AsTask());
        }

        [TestMethod]
        [DataRow("POST /api/songs/1?locale=en HTTp/1.1\r\n")]
        public void ReadStartLine_InvalidVersion(string startLine)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidVersionException>(() => reader.ReadStartLine().AsTask());
        }

        [TestMethod]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.2\r\n")]
        [DataRow("POST /api/songs/1?locale=en HTTP/2.0\r\n")]
        public void ReadStartLine_UnsupportedVersion(string startLine)
        {
            stream.Write(startLine.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<UnsupportedVersionException>(() => reader.ReadStartLine().AsTask());
        }

        [TestMethod]
        [DataRow("Content-Type: application/json; charset=utf-8\r\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type:\tapplication/json; charset=utf-8\r\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type:\t \t\t  application/json; charset=utf-8\r\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type: application/json; charset=utf-8\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type: application/json; charset=utf-8 \r\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type: application/json; charset=utf-8\t\r\n", "Content-Type", "application/json; charset=utf-8")]
        [DataRow("Content-Type: application/json; charset=utf-8\t \t\t  \r\n", "Content-Type", "application/json; charset=utf-8")]
        public async Task ReadHeader_Complete(string header, string expectedName, string expectedValue)
        {
            stream.Write(header.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadHeader();
            Assert.IsTrue(result.Complete(out var value));

            Assert.AreEqual(expectedName, value!.Value.Name);
            Assert.AreEqual(expectedValue, value!.Value.Value);
        }

        [TestMethod]
        [DataRow("\r\n")]
        [DataRow("\n")]
        public async Task ReadHeader_Finished(string header)
        {
            stream.Write(header.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadHeader();
            Assert.IsTrue(result.Finished);
        }

        [TestMethod]
        [DataRow("Content-Type")]
        [DataRow("Content-Type: application/json; charset: utf-8")]
        [DataRow("Content-Type: application/json; charset: utf-8\r")]
        [DataRow("Content-Type: application/json; charset: utf-8 ")]
        [DataRow("Content-Type: application/json; charset: utf-8\t")]
        public async Task ReadHeader_Incomplete(string header)
        {
            stream.Write(header.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadHeader();
            Assert.IsTrue(result.Incomplete);
        }

        [TestMethod]
        [DataRow("Con\tent-Type: application/json; charset=utf-8\r\n")]
        public void ReadHeader_InvalidName(string header)
        {
            stream.Write(header.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidHeaderNameException>(() => reader.ReadStartLine().AsTask());
        }

        private class RequestReader
        {
            private readonly HttpReader reader;

            internal StartLine? startLine = null;
            internal readonly Dictionary<string, string> headers = new();
            internal Stream? body = null;

            internal RequestReader(HttpReader reader)
            {
                this.reader = reader;
            }

            internal async Task<bool> Read()
            {
                var startLineResult = await reader.ReadStartLine();
                if (!startLineResult.Complete(out startLine)) return false;

                while (true)
                {
                    var headerResult = await reader.ReadHeader();
                    if (headerResult.Complete(out var header)) headers.Add(header.Value.Name, header.Value.Value);
                    else if (headerResult.Incomplete) return false;
                    else if (headerResult.Finished) break;
                }

                body = reader.Stream;
                return true;
            }
        }

        [TestMethod]
        public async Task Read_Complete()
        {
            string body = "{ \"name\": \"Hydra\" }";
            string request =
                "POST /api/songs/1?locale=en HTTP/1.1\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "\r\n" + body;

            var reader = new RequestReader(this.reader);

            stream.Write(request.AsBytes());
            stream.Position = 0;

            Assert.IsTrue(await reader.Read());

            Assert.AreEqual("POST", reader.startLine!.Value.Method);
            Assert.AreEqual("/api/songs/1?locale=en", reader.startLine!.Value.Uri);
            Assert.AreEqual(HttpVersion.Http11, reader.startLine!.Value.Version);

            Assert.AreEqual("application/json; charset=utf-8", reader.headers["Content-Type"]);
            Assert.AreEqual($"{body.Length}", reader.headers["Content-Length"]);

            Assert.AreEqual(body, reader.body!.AsText(Encoding.UTF8));
        }

        [TestMethod]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r\nContent-Type: application/json; charset=utf-8\r\n")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r\nContent-Type: application/json; charset=utf-8")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r\n")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1")]
        public async Task Read_Incomplete(string request)
        {
            var reader = new RequestReader(this.reader);

            stream.Write(request.AsBytes());
            stream.Position = 0;

            Assert.IsFalse(await reader.Read());
        }

        [TestMethod]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\rContent-Type: application/json; charset=utf-8")]
        [DataRow("POST /api/songs/1?locale=en HTTP/1.1\r\nContent-Type: application/json; charset=utf-8\r\r\n")]
        public void Read_InvalidNewline(string request)
        {
            var reader = new RequestReader(this.reader);

            stream.Write(request.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidNewlineException>(() => reader.Read());
        }
    }
}
