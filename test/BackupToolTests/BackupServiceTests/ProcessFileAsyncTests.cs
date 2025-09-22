using BackupTool.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupServiceTests
{

    [TestClass]
    public class ProcessFileAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task ProcessFileAsync_WhenFileIsNew_CreatesFileContentAndSnapshotFile()
        {
            // Arrange
            const string filePath = @"C:\TestDir\file1.txt";
            const int snapshotId = 1;
            const string relativePath = "testDir";
            var fileData = new byte[] { 1, 2, 3, 4, 5 };
            const string fileHash = "hash123";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(fileHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(fileHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(5, result); // File size

            // Verify FileContent was created
            _fileContentRepository.Verify(x => x.CreateAsync(It.Is<FileContent>(fc =>
                fc.Hash == fileHash &&
                fc.Data.SequenceEqual(fileData) &&
                fc.Size == 5)), Times.Once);

            // Verify SnapshotFile was created
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.SnapshotId == snapshotId &&
                sf.ContentHash == fileHash &&
                sf.RelativePath == @"testDir\file1.txt" &&
                sf.FileName == "file1.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenFileContentExists_SkipsContentCreation()
        {
            // Arrange
            const string filePath = @"C:\TestDir\existing.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2, 3 };
            const string existingHash = "existingHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(existingHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(existingHash)).ReturnsAsync(true);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(3, result); // File size

            // Verify FileContent was NOT created since it already exists
            _fileContentRepository.Verify(x => x.CreateAsync(It.IsAny<FileContent>()), Times.Never);

            // But SnapshotFile should still be created
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.SnapshotId == snapshotId &&
                sf.ContentHash == existingHash &&
                sf.RelativePath == "existing.txt" &&
                sf.FileName == "existing.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenRelativePathIsEmpty_UsesOnlyFileName()
        {
            // Arrange
            const string filePath = @"C:\TestDir\root.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2 };
            const string fileHash = "hash456";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(fileHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(fileHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(2, result);

            // Verify SnapshotFile has correct relative path (just filename when relativePath is empty)
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.RelativePath == "root.txt" &&
                sf.FileName == "root.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenRelativePathProvided_CombinesPathAndFileName()
        {
            // Arrange
            const string filePath = @"C:\TestDir\SubDir\nested.txt";
            const int snapshotId = 1;
            const string relativePath = @"parent\child";
            var fileData = new byte[] { 1, 2, 3, 4 };
            const string fileHash = "nestedHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(fileHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(fileHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(4, result);

            // Verify SnapshotFile has correct combined relative path
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.RelativePath == @"parent\child\nested.txt" &&
                sf.FileName == "nested.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenFileIsEmpty_HandlesEmptyFile()
        {
            // Arrange
            const string filePath = @"C:\TestDir\empty.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = Array.Empty<byte>();
            const string emptyFileHash = "emptyHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(emptyFileHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(emptyFileHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(0, result); // Empty file size

            // Verify FileContent was created with zero size
            _fileContentRepository.Verify(x => x.CreateAsync(It.Is<FileContent>(fc =>
                fc.Hash == emptyFileHash &&
                fc.Data.Length == 0 &&
                fc.Size == 0)), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenLargeFile_HandlesLargeFile()
        {
            // Arrange
            const string filePath = @"C:\TestDir\large.bin";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[1024 * 1024]; // 1MB file
            for (int i = 0; i < fileData.Length; i++)
                fileData[i] = (byte)(i % 256);
            const string largeFileHash = "largeHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(largeFileHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(largeFileHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(1024 * 1024, result); // 1MB

            // Verify FileContent was created with correct size
            _fileContentRepository.Verify(x => x.CreateAsync(It.Is<FileContent>(fc =>
                fc.Hash == largeFileHash &&
                fc.Data.Length == 1024 * 1024 &&
                fc.Size == 1024 * 1024)), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenFileWithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            const string filePath = @"C:\TestDir\file with spaces & symbols!.txt";
            const int snapshotId = 1;
            const string relativePath = "special chars";
            var fileData = new byte[] { 0xFF, 0xFE, 0x00, 0x01 };
            const string specialHash = "specialHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(specialHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(specialHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(4, result);

            // Verify SnapshotFile handles special characters correctly
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.RelativePath == @"special chars\file with spaces & symbols!.txt" &&
                sf.FileName == "file with spaces & symbols!.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenDeepNestedPath_HandlesCorrectly()
        {
            // Arrange
            const string filePath = @"C:\Root\Level1\Level2\Level3\deep.txt";
            const int snapshotId = 1;
            const string relativePath = @"backup\level1\level2";
            var fileData = new byte[] { 1, 2, 3 };
            const string deepHash = "deepHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(deepHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(deepHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(3, result);

            // Verify deep nested path is handled correctly
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.RelativePath == @"backup\level1\level2\deep.txt" &&
                sf.FileName == "deep.txt")), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenFileContentCreationSucceeds_LogsDebugMessage()
        {
            // Arrange
            const string filePath = @"C:\TestDir\new.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2, 3 };
            const string newHash = "newHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(newHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(newHash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Storing new file content: {filePath}") && v.ToString()!.Contains($"hash: {newHash}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenFileContentExists_LogsTraceMessage()
        {
            // Arrange
            const string filePath = @"C:\TestDir\existing.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2, 3 };
            const string existingHash = "existingHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(existingHash);
            _fileContentRepository.Setup(x => x.ExistsAsync(existingHash)).ReturnsAsync(true);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Trace,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"File content already exists: {filePath}") && v.ToString()!.Contains($"hash: {existingHash}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenCalled_LogsTraceMessage()
        {
            // Arrange
            const string filePath = @"C:\TestDir\trace.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1 };
            const string hash = "traceHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(hash);
            _fileContentRepository.Setup(x => x.ExistsAsync(hash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Trace,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Processing file: {filePath}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenSnapshotIdIsZero_ProcessesNormally()
        {
            // Arrange
            const string filePath = @"C:\TestDir\zero.txt";
            const int snapshotId = 0;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2 };
            const string hash = "zeroHash";

            _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(hash);
            _fileContentRepository.Setup(x => x.ExistsAsync(hash)).ReturnsAsync(false);
            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // Act
            var result = await _service.ProcessFileAsync(filePath, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(2, result);

            // Verify SnapshotFile was created with snapshotId = 0
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.Is<SnapshotFile>(sf =>
                sf.SnapshotId == 0)), Times.Once);
        }

        [TestMethod]
        public async Task ProcessFileAsync_WhenMultipleFilesWithSameContent_CreatesContentOnce()
        {
            // Arrange
            const string filePath1 = @"C:\TestDir\file1.txt";
            const string filePath2 = @"C:\TestDir\file2.txt";
            const int snapshotId = 1;
            const string relativePath = "";
            var fileData = new byte[] { 1, 2, 3 };
            const string sharedHash = "sharedHash";

            _fileSystem.Setup(x => x.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(fileData);
            _hashService.Setup(x => x.CalculateHash(fileData)).Returns(sharedHash);
            _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

            // First call - content doesn't exist
            _fileContentRepository.SetupSequence(x => x.ExistsAsync(sharedHash))
                .ReturnsAsync(false)  // First file - content doesn't exist
                .ReturnsAsync(true);  // Second file - content now exists

            _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);

            // Act
            var result1 = await _service.ProcessFileAsync(filePath1, snapshotId, relativePath);
            var result2 = await _service.ProcessFileAsync(filePath2, snapshotId, relativePath);

            // Assert
            Assert.AreEqual(3, result1);
            Assert.AreEqual(3, result2);

            // Verify FileContent was created only once (for first file)
            _fileContentRepository.Verify(x => x.CreateAsync(It.IsAny<FileContent>()), Times.Once);

            // But both SnapshotFiles were created
            _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Exactly(2));
        }
    }
}