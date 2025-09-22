using BackupTool.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public class RestoreSnapshotAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenSnapshotHasFiles_RestoresAllFiles()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent1 = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            var fileContent2 = new FileContent { Hash = "hash2", Data = [4, 5, 6, 7], Size = 4 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "file1.txt", Content = fileContent1 },
                new() { Id = 2, SnapshotId = snapshotId, RelativePath = @"subdir\file2.txt", Content = fileContent2 }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns(Task.CompletedTask);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\file1.txt", fileContent1.Data), Times.Once);
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\subdir\file2.txt", fileContent2.Data), Times.Once);
            _fileSystem.Verify(x => x.CreateDirectory(@"C:\Restore\subdir"), Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenSnapshotHasNoFiles_LogsErrorAndReturns()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var emptyFileList = new List<SnapshotFile>();

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(emptyFileList);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"No files found for Snapshot: {snapshotId}, stopping")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenFileInNestedDirectory_CreatesDirectoryStructure()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = @"level1\level2\level3\deep.txt", Content = fileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns(Task.CompletedTask);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.CreateDirectory(@"C:\Restore\level1\level2\level3"), Times.Once);
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\level1\level2\level3\deep.txt", fileContent.Data), Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenSingleFileRestoreFails_ContinuesWithOtherFiles()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent1 = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            var fileContent2 = new FileContent { Hash = "hash2", Data = [4, 5, 6], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "fail.txt", Content = fileContent1 },
                new() { Id = 2, SnapshotId = snapshotId, RelativePath = "success.txt", Content = fileContent2 }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(@"C:\Restore\fail.txt", fileContent1.Data))
                .ThrowsAsync(new UnauthorizedAccessException("Access denied"));
            _fileSystem.Setup(x => x.WriteFileAsync(@"C:\Restore\success.txt", fileContent2.Data))
                .Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\fail.txt", fileContent1.Data), Times.Once);
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\success.txt", fileContent2.Data), Times.Once);

            // Verify warning is logged for failed file
            _logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to restore file: fail.txt")),
                    It.IsAny<UnauthorizedAccessException>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenDirectoryCreationFails_FailsFileRestore()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = @"readonly\file.txt", Content = fileContent }
            };

            var expectedException = new UnauthorizedAccessException("Cannot create directory");
            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.CreateDirectory(@"C:\Restore\readonly")).ThrowsAsync(expectedException);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            // Verify the file write was never attempted since directory creation failed
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\readonly\file.txt", fileContent.Data), Times.Never);

            // Verify warning is logged for failed file restoration
            _logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to restore file: readonly\\file.txt")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenRestoring50Files_LogsProgress()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var snapshotFiles = new List<SnapshotFile>();

            // Create 50 files
            for (int i = 1; i <= 50; i++)
            {
                var fileContent = new FileContent { Hash = $"hash{i}", Data = [1, 2, 3], Size = 3 };
                snapshotFiles.Add(new SnapshotFile
                {
                    Id = i,
                    SnapshotId = snapshotId,
                    RelativePath = $"file{i}.txt",
                    Content = fileContent
                });
            }

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(50));

            // Verify progress logging occurs at file 50
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Restored 50/50 files")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenSuccessful_LogsStartAndCompletionMessages()
        {
            // Arrange
            const int snapshotId = 42;
            const string outputDirectory = @"C:\MyRestore";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3, 4, 5], Size = 5 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "test.txt", Content = fileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Starting restore of snapshot {snapshotId} to {outputDirectory}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Restore completed. Snapshot: {snapshotId}, Files: 1, Bytes: 5")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenRepositoryThrows_LogsErrorAndDoesNotCrash()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var exception = new InvalidOperationException("Database connection failed");

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ThrowsAsync(exception);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Failed to restore snapshot {snapshotId} to {outputDirectory}")),
                    exception,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenLargeNumberOfFiles_HandlesCorrectly()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var snapshotFiles = new List<SnapshotFile>();

            // Create 1000 files
            for (int i = 1; i <= 1000; i++)
            {
                var fileContent = new FileContent { Hash = $"hash{i}", Data = new byte[100], Size = 100 };
                snapshotFiles.Add(new SnapshotFile
                {
                    Id = i,
                    SnapshotId = snapshotId,
                    RelativePath = $"batch{i / 100}/file{i}.txt",
                    Content = fileContent
                });
            }

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns(Task.CompletedTask);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Exactly(1000));

            // Verify completion log shows correct totals
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Files: 1000") && v.ToString()!.Contains("Bytes: 100,000")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenFilesHaveSpecialCharacters_RestoresCorrectly()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = @"special chars & symbols!\file with spaces.txt", Content = fileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>())).Returns(Task.CompletedTask);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.CreateDirectory(@"C:\Restore\special chars & symbols!"), Times.Once);
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\special chars & symbols!\file with spaces.txt", fileContent.Data), Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenEmptyFiles_RestoresEmptyFiles()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var emptyFileContent = new FileContent { Hash = "emptyHash", Data = [], Size = 0 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "empty.txt", Content = emptyFileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\empty.txt", Array.Empty<byte>()), Times.Once);

            // Verify completion log shows zero bytes
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
        public async Task RestoreSnapshotAsync_WhenOutputDirectoryHasTrailingSlash_HandlesCorrectly()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore\";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "test.txt", Content = fileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            // Path.Combine should handle the trailing slash correctly
            _fileSystem.Verify(x => x.WriteFileAsync(@"C:\Restore\test.txt", fileContent.Data), Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenTraceLoggingEnabled_LogsFileDetails()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };

            var snapshotFiles = new List<SnapshotFile>
            {
                new() { Id = 1, SnapshotId = snapshotId, RelativePath = "trace.txt", Content = fileContent }
            };

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Trace,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Restored file: C:\\Restore\\trace.txt")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task RestoreSnapshotAsync_WhenInitialFileCountLogShown_ShowsCorrectCount()
        {
            // Arrange
            const int snapshotId = 1;
            const string outputDirectory = @"C:\Restore";
            var snapshotFiles = new List<SnapshotFile>();

            // Create 3 files
            for (int i = 1; i <= 3; i++)
            {
                var fileContent = new FileContent { Hash = $"hash{i}", Data = [1, 2, 3], Size = 3 };
                snapshotFiles.Add(new SnapshotFile
                {
                    Id = i,
                    SnapshotId = snapshotId,
                    RelativePath = $"file{i}.txt",
                    Content = fileContent
                });
            }

            _snapshotFileRepository.Setup(x => x.GetBySnapshotIdAsync(snapshotId)).ReturnsAsync(snapshotFiles);
            _fileSystem.Setup(x => x.WriteFileAsync(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(Task.CompletedTask);

            // Act
            await _service.RestoreSnapshotAsync(snapshotId, outputDirectory);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Restoring 3 files from snapshot {snapshotId}")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}