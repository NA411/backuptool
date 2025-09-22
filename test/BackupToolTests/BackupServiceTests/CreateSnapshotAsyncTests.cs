using Moq;
using BackupTool.Entities;

namespace BackupServiceTests
{
    [TestClass]
    public sealed class CreateSnapshotAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task CreateSnapshotAsync_WhenValidDirectory_ReturnsSnapshotId()
        {
            // Arrange
            const string sourceDir = @"C:\TestDir";
            const int expectedSnapshotId = 42;

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(It.IsAny<string>())).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Never);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenDirectoryDoesNotExist_ReturnsNull()
        {
            // Arrange
            const string sourceDir = @"C:\NonExistentDir";

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(false);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Never);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenExceptionDuringCreation_ReturnsNullAndRollsBack()
        {
            // Arrange
            const string sourceDir = @"C:\TestDir";

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);

            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenTransactionFails_ReturnsNullAndRollsBack()
        {
            // Arrange
            const string sourceDir = @"C:\TestDir";

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);

            _unitOfWork.Setup(x => x.CommitTransactionAsync()).ThrowsAsync(new Exception("Transaction failed"));

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        }
    }
}