using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtils;

namespace Hydra.Tests
{
    [TestClass]
    public class HttpSizedBodyStreamTests
    {
        [TestMethod]
        public void Limits()
        {
            var stream = new MemoryStream();
            stream.Write("0123456789".AsBytes());
            stream.Position = 0;

            var bodyStream = new HttpSizedBodyStream(stream, 5);
            Assert.AreEqual("01234", bodyStream.AsText());
        }
    }
}
