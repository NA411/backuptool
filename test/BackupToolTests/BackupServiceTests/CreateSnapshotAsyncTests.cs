using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public sealed class CreateSnapshotAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task CreateSnapshotAsync_WhenValidDirectoryWithFiles_ReturnsSnapshotId()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "BackupTest", Guid.NewGuid().ToString());
            Directory.CreateDirectory(sourceDir);
            const int expectedSnapshotId = 42;
            var filePath = Path.Combine(sourceDir, "file1.txt");
            var fileData = new byte[] { 1, 2, 3, 4, 5 };

            try
            {
                _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
                _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([filePath]);
                _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
                _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
                _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash123");
                _fileContentRepository.Setup(x => x.ExistsAsync("hash123")).ReturnsAsync(false);
                _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
                _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
                _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

                // Act
                var result = await _service.CreateSnapshotAsync(sourceDir);

                // Assert
                Assert.AreEqual(expectedSnapshotId, result);
                _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
                _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
                _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Never);
            }
            finally
            {
                if (Directory.Exists(sourceDir))
                    Directory.Delete(sourceDir, true);
            }
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenValidEmptyDirectory_ReturnsSnapshotId()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "EmptyDir", Guid.NewGuid().ToString());
            const int expectedSnapshotId = 1;

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
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
            var sourceDir = Path.Combine(Path.GetTempPath(), "NonExistentDir", Guid.NewGuid().ToString());
            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(false);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Never);

            // Verify error logging
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Source directory not found: {sourceDir}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenSnapshotCreationFails_ReturnsNullAndRollsBack()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());

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
        public async Task CreateSnapshotAsync_WhenFileProcessingFails_ReturnsNullAndRollsBack()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var filePath = Path.Combine(sourceDir, "file1.txt");

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([filePath]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ThrowsAsync(new IOException("File access denied"));
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = 1).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenCommitTransactionFails_ReturnsNullAndRollsBack()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = 1).ReturnsAsync((Snapshot s) => s);
            _unitOfWork.Setup(x => x.CommitTransactionAsync()).ThrowsAsync(new Exception("Transaction failed"));

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenDirectoryWithNestedStructure_ProcessesRecursively()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var subDir = Path.Combine(sourceDir, "SubDir");
            var rootFile = Path.Combine(sourceDir, "root.txt");
            var subFile = Path.Combine(subDir, "sub.txt");
            const int expectedSnapshotId = 1;
            var rootFileData = new byte[] { 1, 2 };
            var subFileData = new byte[] { 3, 4, 5 };

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([rootFile]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([subDir]);
            _fileSystem.Setup(x => x.GetFiles(subDir, It.IsAny<string>())).Returns([subFile]);
            _fileSystem.Setup(x => x.GetDirectories(subDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(rootFile)).ReturnsAsync(rootFileData);
            _fileSystem.Setup(x => x.ReadFileAsync(subFile)).ReturnsAsync(subFileData);
            _hashService.Setup(x => x.CalculateHash(rootFileData)).Returns("rootHash");
            _hashService.Setup(x => x.CalculateHash(subFileData)).Returns("subHash");
            _fileContentRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify both files were processed
            _fileSystem.Verify(x => x.ReadFileAsync(rootFile), Times.Once);
            _fileSystem.Verify(x => x.ReadFileAsync(subFile), Times.Once);
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenLargeNumberOfFiles_ProcessesAllFiles()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            const int expectedSnapshotId = 1;
            var files = new List<string>();

            for (int i = 1; i <= 100; i++) // Create 100 files
                files.Add(Path.Combine(sourceDir,$"file{i}.txt"));

            var fileData = new byte[] { 1, 2, 3 };
            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns(files);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash");
            _fileContentRepository.Setup(x => x.ExistsAsync("hash")).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Exactly(100));

            // Verify progress logging
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Processed 100 files")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenDuplicateFileContent_CreatesContentOnce()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var file1 = Path.Combine(sourceDir, "file1.txt");
            var file2 = Path.Combine(sourceDir, "file2.txt");
            const int expectedSnapshotId = 1;
            var identicalData = new byte[] { 1, 2, 3, 4, 5 };
            const string sharedHash = "sharedHash";

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([file1, file2]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(identicalData);
            _hashService.Setup(x => x.CalculateHash(identicalData)).Returns(sharedHash);

            _fileContentRepository.SetupSequence(x => x.ExistsAsync(sharedHash)).ReturnsAsync(false).ReturnsAsync(true);

            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify content was created only once (de-duplication)
            _fileContentRepository.Verify(x => x.CreateAsync(It.IsAny<FileContent>()), Times.Once);

            // But both snapshot files were created
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenSuccessful_LogsStartAndCompletionWithStats()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var filePath = Path.Combine(sourceDir, "file1.txt");
            const int expectedSnapshotId = 42;
            var fileData = new byte[] { 1, 2, 3, 4, 5 };

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([filePath]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash123");
            _fileContentRepository.Setup(x => x.ExistsAsync("hash123")).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify start logging
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Starting snapshot creation for directory: {sourceDir}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Verify completion logging with stats
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Snapshot {expectedSnapshotId} created successfully") && v.ToString()!.Contains("Files: 1") && v.ToString()!.Contains("Bytes: 5")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenExceptionOccurs_LogsError()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var exception = new InvalidOperationException("Test exception");

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).ThrowsAsync(exception);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);

            // Verify error logging
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Failed to create snapshot for directory: {sourceDir}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenSnapshotCreated_SetsCorrectProperties()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            Snapshot? capturedSnapshot = null;

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s =>
                {
                    capturedSnapshot = s;
                    s.Id = 1;
                }).ReturnsAsync((Snapshot s) => s);

            // Act
            await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNotNull(capturedSnapshot);
            Assert.AreEqual(sourceDir, capturedSnapshot.SourceDirectory);
            Assert.IsTrue(capturedSnapshot.CreatedAt <= DateTime.UtcNow);
            Assert.IsTrue(capturedSnapshot.CreatedAt > DateTime.UtcNow.AddMinutes(-1)); // Should be recent
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenDebugLoggingEnabled_LogsSnapshotCreation()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = 1).ReturnsAsync((Snapshot s) => s);

            // Act
            await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Creating snapshot record for directory: {sourceDir}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenEmptyDirectory_LogsZeroStats()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "EmptyDir", Guid.NewGuid().ToString());
            const int expectedSnapshotId = 1;

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify completion logging shows zero stats
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Snapshot {expectedSnapshotId} created successfully") &&
                                                v.ToString()!.Contains("Files: 0") &&
                                                v.ToString()!.Contains("Bytes: 0")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenMixedFileAndDirectoryStructure_ProcessesCorrectly()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var subDir1 = Path.Combine(sourceDir, "SubDir1");
            var subDir2 = Path.Combine(sourceDir, "SubDir2");
            var rootFile = Path.Combine(sourceDir, "root.txt");
            var subFile1 = Path.Combine(subDir1, "sub1.txt");
            var subFile2 = Path.Combine(subDir2, "sub2.txt");
            const int expectedSnapshotId = 1;
            var fileData = new byte[] { 1, 2, 3 };

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([rootFile]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([subDir1, subDir2]);
            _fileSystem.Setup(x => x.GetFiles(subDir1, It.IsAny<string>())).Returns([subFile1]);
            _fileSystem.Setup(x => x.GetDirectories(subDir1)).Returns([]);
            _fileSystem.Setup(x => x.GetFiles(subDir2, It.IsAny<string>())).Returns([subFile2]);
            _fileSystem.Setup(x => x.GetDirectories(subDir2)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns((byte[] data) => $"hash_{data.Length}");
            _fileContentRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify all 3 files were processed
            _fileSystem.Verify(x => x.ReadFileAsync(rootFile), Times.Once);
            _fileSystem.Verify(x => x.ReadFileAsync(subFile1), Times.Once);
            _fileSystem.Verify(x => x.ReadFileAsync(subFile2), Times.Once);
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Exactly(3));

            // Verify final stats show 3 files
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Files: 3") && v.ToString()!.Contains("Bytes: 9")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenSpecialCharactersInPath_HandlesCorrectly()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "Test Dir with spaces & symbols!", Guid.NewGuid().ToString());
            var filePath = Path.Combine(sourceDir, "file with spaces.txt");
            const int expectedSnapshotId = 1;
            var fileData = new byte[] { 1, 2, 3 };

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([filePath]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns("specialHash");
            _fileContentRepository.Setup(x => x.ExistsAsync("specialHash")).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify the snapshot was created with special characters in source directory
            _unitOfWork.Verify(x => x.Snapshots.CreateAsync(It.Is<Snapshot>(s => s.SourceDirectory == sourceDir)), Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenEmptyFilesExist_ProcessesEmptyFiles()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var emptyFilePath = Path.Combine(sourceDir, "empty.txt");
            const int expectedSnapshotId = 1;
            var emptyFileData = Array.Empty<byte>();

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([emptyFilePath]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(emptyFilePath)).ReturnsAsync(emptyFileData);
            _hashService.Setup(x => x.CalculateHash(emptyFileData)).Returns("emptyHash");
            _fileContentRepository.Setup(x => x.ExistsAsync("emptyHash")).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify empty file was processed with zero bytes
            _fileContentRepository.Verify(x => x.CreateAsync(It.Is<FileContent>(fc =>
                fc.Hash == "emptyHash" &&
                fc.Data.Length == 0 &&
                fc.Size == 0)), Times.Once);

            // Verify final stats show zero bytes
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Files: 1") && v.ToString()!.Contains("Bytes: 0")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenTransactionBeginFails_ReturnsNullWithoutRollback()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _unitOfWork.Setup(x => x.BeginTransactionAsync()).ThrowsAsync(new InvalidOperationException("Cannot begin transaction"));

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once); // Should still attempt rollback
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenVeryLargeFile_ProcessesCorrectly()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var largeFilePath = Path.Combine(sourceDir, "large.bin");
            const int expectedSnapshotId = 1;
            var largeFileData = new byte[10 * 1024 * 1024]; // 10MB file
            Array.Fill(largeFileData, (byte)0xAB);

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([largeFilePath]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _fileSystem.Setup(x => x.ReadFileAsync(largeFilePath)).ReturnsAsync(largeFileData);
            _hashService.Setup(x => x.CalculateHash(largeFileData)).Returns("largeFileHash");
            _fileContentRepository.Setup(x => x.ExistsAsync("largeFileHash")).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = expectedSnapshotId).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.AreEqual(expectedSnapshotId, result);

            // Verify large file was processed with correct size
            _fileContentRepository.Verify(x => x.CreateAsync(It.Is<FileContent>(fc =>
                fc.Hash == "largeFileHash" &&
                fc.Data.Length == 10 * 1024 * 1024 &&
                fc.Size == 10 * 1024 * 1024)), Times.Once);

            // Verify final stats show large byte count
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Files: 1") && v.ToString()!.Contains("Bytes: 10,485,760")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenNullOrEmptyPath_HandlesGracefully()
        {
            // Test with null path
            var resultNull = await _service.CreateSnapshotAsync(null!);
            Assert.IsNull(resultNull);

            // Test with empty path
            var resultEmpty = await _service.CreateSnapshotAsync(string.Empty);
            Assert.IsNull(resultEmpty);

            // Verify no transactions were started for invalid paths
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenProcessDirectoryThrows_RollsBackAndReturnsNull()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Throws(new UnauthorizedAccessException("Access denied"));
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s => s.Id = 1).ReturnsAsync((Snapshot s) => s);

            // Act
            var result = await _service.CreateSnapshotAsync(sourceDir);

            // Assert
            Assert.IsNull(result);
            _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
            _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
        }

        [TestMethod]
        public async Task CreateSnapshotAsync_WhenSnapshotHasUtcTimestamp_CreatesWithCorrectTime()
        {
            // Arrange
            var sourceDir = Path.Combine(Path.GetTempPath(), "TestDir", Guid.NewGuid().ToString());
            var beforeTime = DateTime.UtcNow;
            Snapshot? capturedSnapshot = null;

            _fileSystem.Setup(x => x.DirectoryExists(sourceDir)).Returns(true);
            _fileSystem.Setup(x => x.GetFiles(sourceDir, It.IsAny<string>())).Returns([]);
            _fileSystem.Setup(x => x.GetDirectories(sourceDir)).Returns([]);
            _unitOfWork.Setup(x => x.Snapshots.CreateAsync(It.IsAny<Snapshot>())).Callback<Snapshot>(s =>
                {
                    capturedSnapshot = s;
                    s.Id = 1;
                }).ReturnsAsync((Snapshot s) => s);

            // Act
            await _service.CreateSnapshotAsync(sourceDir);
            var afterTime = DateTime.UtcNow;

            // Assert
            Assert.IsNotNull(capturedSnapshot);
            Assert.IsTrue(capturedSnapshot.CreatedAt >= beforeTime);
            Assert.IsTrue(capturedSnapshot.CreatedAt <= afterTime);
            Assert.AreEqual(DateTimeKind.Utc, capturedSnapshot.CreatedAt.Kind);
        }
    }
}