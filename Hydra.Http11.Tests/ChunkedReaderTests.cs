using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using TestUtils;

namespace Hydra.Http11.Tests
{
    [TestClass]
    public class ChunkedReaderTests
    {
        private readonly MemoryStream stream = new();
        private readonly ChunkedReader reader;

        public ChunkedReaderTests()
        {
            reader = new(PipeReader.Create(stream));
        }

        [TestMethod]
        [DataRow("1312\r\n", 0x1312, "", false)]
        [DataRow("1312\n", 0x1312, "", false)]
        [DataRow("\r\n1312\r\n", 0x1312, "", true)]
        [DataRow("\n1312\r\n", 0x1312, "", true)]
        [DataRow("1312;server=Hydra\r\n", 0x1312, "", false)]
        [DataRow("1312;server=Hydra\n", 0x1312, "", false)]
        [DataRow("1312\r\nHydra", 0x1312, "Hydra", false)]
        [DataRow("1312;server=Hydra\r\nHydra", 0x1312, "Hydra", false)]
        public async Task ReadChunkSize_Complete(string chunkSize, int expectedSize, string expectedBody, bool prefixNewline)
        {
            stream.Write(chunkSize.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadChunkSize(prefixNewline);
            Assert.IsTrue(result.Complete(out int? value));

            Assert.AreEqual(expectedSize, value!.Value);
            Assert.AreEqual(expectedBody, reader.Reader.AsStream().AsText());
        }

        [TestMethod]
        [DataRow("1312")]
        [DataRow("1312\r")]
        [DataRow("1312;server=Hydra")]
        [DataRow("1312;server=Hydra\r")]
        public async Task ReadChunkSize_Incomplete(string chunkSize)
        {
            stream.Write(chunkSize.AsBytes());
            stream.Position = 0;

            var result = await reader.ReadChunkSize();
            Assert.IsTrue(result.Incomplete);
        }

        [TestMethod]
        [DataRow("ffg\r\n")]
        [DataRow("ffg;server=Hydra\r\n")]
        public void ReadChunkSize_InvalidHexNumber(string chunkSize)
        {
            stream.Write(chunkSize.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidHexNumberException>(() => reader.ReadChunkSize().AsTask());
        }

        [TestMethod]
        [DataRow("1312;ser\ver=Hydra\r\n")]
        [DataRow("1312;server= Hydra\r\n")]
        public void ReadChunkSize_InvalidChunkExtension(string chunkSize)
        {
            stream.Write(chunkSize.AsBytes());
            stream.Position = 0;

            Assert.ThrowsExceptionAsync<InvalidChunkExtensionException>(() => reader.ReadChunkSize().AsTask());
        }
    }
}
