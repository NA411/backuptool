using BackupTool.Services;
using System.Text;

namespace HashServiceTests
{
    [TestClass]
    public class Sha265HashServiceTests
    {
        private Sha256HashService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new Sha256HashService();
        }

        [TestMethod]
        public void CalculateHash_WhenGivenKnownData_ReturnsExpectedHash()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("hello world");
            // SHA256 hash for "hello world" in lowercase hex
            const string expected = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9";

            // Act
            var result = _service.CalculateHash(data);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CalculateHash_WhenGivenEmptyArray_ReturnsExpectedHash()
        {
            // Arrange
            var data = Array.Empty<byte>();
            // SHA256 hash for empty byte array in lowercase hex
            const string expected = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            // Act
            var result = _service.CalculateHash(data);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CalculateHash_WhenGivenNull_ThrowsArgumentNullException() => Assert.ThrowsException<ArgumentNullException>(() => _service.CalculateHash(null!)); // Act & Assert

        [TestMethod]
        public void CalculateHash_WhenGivenDifferentData_ReturnsDifferentHash()
        {
            // Arrange
            var data1 = Encoding.UTF8.GetBytes("foo");
            var data2 = Encoding.UTF8.GetBytes("bar");

            // Act
            var hash1 = _service.CalculateHash(data1);
            var hash2 = _service.CalculateHash(data2);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }
    }
}
