using BackupTool.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupServiceTests;

[TestClass]
public class ProcessDirectoryAsyncTests : BackupServiceTestsBase
{
    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDirectoryIsEmpty_ReturnsZeroStats()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(0, result.FileCount);
        Assert.AreEqual(0, result.BytesProcessed);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDirectoryContainsSingleFile_ReturnsCorrectStats()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";
        const string filePath = @"C:\TestDir\file1.txt";
        var fileData = new byte[] { 1, 2, 3, 4, 5 };
        const string fileHash = "hash123";

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([filePath]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns(fileHash);
        _fileContentRepository.Setup(x => x.ExistsAsync(fileHash)).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(5, result.BytesProcessed);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDirectoryContainsMultipleFiles_ReturnsCorrectStats()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";
        const string filePath1 = @"C:\TestDir\file1.txt";
        const string filePath2 = @"C:\TestDir\file2.txt";
        var fileData1 = new byte[] { 1, 2, 3 };
        var fileData2 = new byte[] { 4, 5, 6, 7, 8 };

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([filePath1, filePath2]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath1)).ReturnsAsync(fileData1);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath2)).ReturnsAsync(fileData2);
        _hashService.Setup(x => x.CalculateHash(fileData1)).Returns("hash1");
        _hashService.Setup(x => x.CalculateHash(fileData2)).Returns("hash2");
        _fileContentRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(2, result.FileCount);
        Assert.AreEqual(8, result.BytesProcessed); // 3 + 5 bytes
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDirectoryContainsSubdirectories_ProcessesRecursively()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const string subDirPath = @"C:\TestDir\SubDir";
        const int snapshotId = 1;
        const string relativePath = "";
        const string filePath = @"C:\TestDir\SubDir\file1.txt";
        var fileData = new byte[] { 1, 2, 3 };

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([subDirPath]);
        _fileSystem.Setup(x => x.GetFiles(subDirPath, It.IsAny<string>())).Returns([filePath]);
        _fileSystem.Setup(x => x.GetDirectories(subDirPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash1");
        _fileContentRepository.Setup(x => x.ExistsAsync("hash1")).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(3, result.BytesProcessed);
        _fileSystem.Verify(x => x.GetFiles(directoryPath, It.IsAny<string>()), Times.Once);
        _fileSystem.Verify(x => x.GetFiles(subDirPath, It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDirectoryContainsMixedContent_ProcessesAllContent()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const string subDirPath = @"C:\TestDir\SubDir";
        const string rootFile = @"C:\TestDir\root.txt";
        const string subFile = @"C:\TestDir\SubDir\sub.txt";
        const int snapshotId = 1;
        const string relativePath = "";
        var rootFileData = new byte[] { 1, 2 };
        var subFileData = new byte[] { 3, 4, 5 };

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([rootFile]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([subDirPath]);
        _fileSystem.Setup(x => x.GetFiles(subDirPath, It.IsAny<string>())).Returns([subFile]);
        _fileSystem.Setup(x => x.GetDirectories(subDirPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(rootFile)).ReturnsAsync(rootFileData);
        _fileSystem.Setup(x => x.ReadFileAsync(subFile)).ReturnsAsync(subFileData);
        _hashService.Setup(x => x.CalculateHash(rootFileData)).Returns("rootHash");
        _hashService.Setup(x => x.CalculateHash(subFileData)).Returns("subHash");
        _fileContentRepository.Setup(x => x.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(2, result.FileCount);
        Assert.AreEqual(5, result.BytesProcessed); // 2 + 3 bytes
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenFileProcessingFails_ThrowsException()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";
        const string filePath1 = @"C:\TestDir\file1.txt";
        const string filePath2 = @"C:\TestDir\file2.txt";
        var expectedException = new IOException("File access denied");

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([filePath1, filePath2]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath1)).ThrowsAsync(expectedException);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath2)).ReturnsAsync([1, 2, 3]);

        // Act & Assert
        var actualException = await Assert.ThrowsExceptionAsync<IOException>(() =>
            _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath));

        Assert.AreEqual(expectedException, actualException);

        // Verify the second file was never processed
        _fileSystem.Verify(x => x.ReadFileAsync(filePath2), Times.Never);
        _fileContentRepository.Verify(x => x.CreateAsync(It.IsAny<FileContent>()), Times.Never);
        _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Never);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenSubdirectoryProcessingFails_ContinuesWithOtherDirectories()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const string subDir1 = @"C:\TestDir\SubDir1";
        const string subDir2 = @"C:\TestDir\SubDir2";
        const string file2 = @"C:\TestDir\SubDir2\file2.txt";
        const int snapshotId = 1;
        const string relativePath = "";
        var fileData = new byte[] { 1, 2, 3 };

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([subDir1, subDir2]);
        _fileSystem.Setup(x => x.GetFiles(subDir1, It.IsAny<string>())).Throws(new UnauthorizedAccessException("Access denied"));
        _fileSystem.Setup(x => x.GetFiles(subDir2, It.IsAny<string>())).Returns([file2]);
        _fileSystem.Setup(x => x.GetDirectories(subDir2)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(file2)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash2");
        _fileContentRepository.Setup(x => x.ExistsAsync("hash2")).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount); // Only SubDir2 processed successfully
        Assert.AreEqual(3, result.BytesProcessed);

        // Verify warning is logged for failed subdirectory
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Failed to process subdirectory: {subDir1}")),
                It.IsAny<UnauthorizedAccessException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenProcessing100Files_LogsProgress()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";
        var files = new List<string>();

        // Create 100 files
        for (int i = 1; i <= 100; i++)
        {
            files.Add($@"C:\TestDir\file{i}.txt");
        }

        var fileData = new byte[] { 1, 2, 3 };
        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns(files);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(It.IsAny<string>())).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash");
        _fileContentRepository.Setup(x => x.ExistsAsync("hash")).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(100, result.FileCount);
        Assert.AreEqual(300, result.BytesProcessed); // 100 files * 3 bytes each

        // Verify progress logging occurs at file 100
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
    public async Task ProcessDirectoryAsync_WhenRelativePathProvided_PassesToFileProcessing()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "parent";
        const string filePath = @"C:\TestDir\file1.txt";
        var fileData = new byte[] { 1, 2, 3 };

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([filePath]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns("hash1");
        _fileContentRepository.Setup(x => x.ExistsAsync("hash1")).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(3, result.BytesProcessed);

        // Verify the SnapshotFile was created with correct relative path
        _snapshotFileRepository.Verify(x => x.CreateAsync(
            It.Is<SnapshotFile>(sf => sf.RelativePath == @"parent\file1.txt")),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenDeepDirectoryStructure_ProcessesRecursively()
    {
        // Arrange
        const string rootDir = @"C:\TestDir";
        const string subDir1 = @"C:\TestDir\Level1";
        const string subDir2 = @"C:\TestDir\Level1\Level2";
        const string deepFile = @"C:\TestDir\Level1\Level2\deep.txt";
        const int snapshotId = 1;
        const string relativePath = "";
        var fileData = new byte[] { 1, 2, 3, 4 };

        _fileSystem.Setup(x => x.GetFiles(rootDir, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(rootDir)).Returns([subDir1]);
        _fileSystem.Setup(x => x.GetFiles(subDir1, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(subDir1)).Returns([subDir2]);
        _fileSystem.Setup(x => x.GetFiles(subDir2, It.IsAny<string>())).Returns([deepFile]);
        _fileSystem.Setup(x => x.GetDirectories(subDir2)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(deepFile)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns("deepHash");
        _fileContentRepository.Setup(x => x.ExistsAsync("deepHash")).ReturnsAsync(false);
        _fileContentRepository.Setup(x => x.CreateAsync(It.IsAny<FileContent>())).ReturnsAsync((FileContent fc) => fc);
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(rootDir, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(4, result.BytesProcessed);

        // Verify the deep file was processed with correct relative path
        _snapshotFileRepository.Verify(x => x.CreateAsync(
            It.Is<SnapshotFile>(sf => sf.RelativePath == @"Level1\Level2\deep.txt")),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenLoggingEnabled_LogsDirectoryProcessing()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "test";

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);

        // Act
        await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Processing directory: {directoryPath}") && v.ToString()!.Contains("relative: test")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ProcessDirectoryAsync_WhenFileContentAlreadyExists_SkipsContentCreation()
    {
        // Arrange
        const string directoryPath = @"C:\TestDir";
        const int snapshotId = 1;
        const string relativePath = "";
        const string filePath = @"C:\TestDir\file1.txt";
        var fileData = new byte[] { 1, 2, 3 };
        const string existingHash = "existingHash";

        _fileSystem.Setup(x => x.GetFiles(directoryPath, It.IsAny<string>())).Returns([filePath]);
        _fileSystem.Setup(x => x.GetDirectories(directoryPath)).Returns([]);
        _fileSystem.Setup(x => x.ReadFileAsync(filePath)).ReturnsAsync(fileData);
        _hashService.Setup(x => x.CalculateHash(fileData)).Returns(existingHash);
        _fileContentRepository.Setup(x => x.ExistsAsync(existingHash)).ReturnsAsync(true); // Content already exists
        _snapshotFileRepository.Setup(x => x.CreateAsync(It.IsAny<SnapshotFile>())).ReturnsAsync((SnapshotFile sf) => sf);

        // Act
        var result = await _service.ProcessDirectoryAsync(directoryPath, snapshotId, relativePath);

        // Assert
        Assert.AreEqual(1, result.FileCount);
        Assert.AreEqual(3, result.BytesProcessed);

        // Verify FileContent was NOT created since it already exists
        _fileContentRepository.Verify(x => x.CreateAsync(It.IsAny<FileContent>()), Times.Never);

        // But SnapshotFile should still be created
        _snapshotFileRepository.Verify(x => x.CreateAsync(It.IsAny<SnapshotFile>()), Times.Once);
    }
}
