using BackupTool.Entities;
using BackupTool.Services;
using BackupToolTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public class CheckForCorruptedContentAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHasNullContent_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            snapshot.Files.Add(TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, "hash123", string.Empty, string.Empty));

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshot.Files.FirstOrDefault(), result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHasEmptyContentHash_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, string.Empty, string.Empty, string.Empty);
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshot.Files.FirstOrDefault(), result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHashDoesNotMatch_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, "storedHash123", string.Empty, string.Empty);
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(snapshotFile.Content.Data)).Returns("differentHash456");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshot.Files.FirstOrDefault(), result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileIsValid_ReturnsEmptyList()
        {
            // Arrange
            const string validHash = "validHash123";
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, validHash, string.Empty, string.Empty);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(snapshotFile.Content.Data)).Returns(validHash);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenSnapshotHasNoFiles_ReturnsEmptyList()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            snapshot.Files = null!;

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenNoSnapshots_ReturnsEmptyList()
        {
            // Arrange
            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }
    }
}
