using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public class CreateOutputDirectoryAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenFullNameIsEmpty_LogsErrorAndDoesNotCreateDirectory()
        {
            // Act
            await _service.CreateOutputDirectoryAsync(string.Empty);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Output directory name is null or empty")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenFullNameIsWhitespace_LogsErrorAndDoesNotCreateDirectory()
        {
            // Arrange
            const string whitespaceDir = "   \t\n\r   ";

            // Act
            await _service.CreateOutputDirectoryAsync(whitespaceDir);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Output directory name is null or empty")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenFullNameIsValid_LogsInformationAndCreatesDirectory()
        {
            // Arrange
            const string dirName = @"C:\output";
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(dirName);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(dirName)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenCreateDirectoryThrows_ExceptionPropagates()
        {
            // Arrange
            const string dirName = @"C:\fail";
            var expectedException = new IOException("Failed to create directory");
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<IOException>(() => _service.CreateOutputDirectoryAsync(dirName));
            Assert.AreEqual(expectedException, actualException);

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(dirName)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenUnauthorizedAccessException_ExceptionPropagates()
        {
            // Arrange
            const string protectedDir = @"C:\Windows\System32\Protected";
            var expectedException = new UnauthorizedAccessException("Access denied");
            _fileSystem.Setup(f => f.CreateDirectory(protectedDir)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => _service.CreateOutputDirectoryAsync(protectedDir));
            Assert.AreEqual(expectedException, actualException);

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(protectedDir)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(protectedDir), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenDirectoryNotFoundExceptionThrown_ExceptionPropagates()
        {
            // Arrange
            const string invalidPath = @"X:\NonExistentDrive\Directory";
            var expectedException = new DirectoryNotFoundException("Drive not found");
            _fileSystem.Setup(f => f.CreateDirectory(invalidPath)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(() => _service.CreateOutputDirectoryAsync(invalidPath));
            Assert.AreEqual(expectedException, actualException);

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(invalidPath)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(invalidPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenPathTooLongExceptionThrown_ExceptionPropagates()
        {
            // Arrange
            var longPath = @"C:\" + new string('a', 300); // Very long path
            var expectedException = new PathTooLongException("Path too long");
            _fileSystem.Setup(f => f.CreateDirectory(longPath)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<PathTooLongException>(() => _service.CreateOutputDirectoryAsync(longPath));
            Assert.AreEqual(expectedException, actualException);

            _fileSystem.Verify(f => f.CreateDirectory(longPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenValidRelativePath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string relativePath = @"backup\output";
            _fileSystem.Setup(f => f.CreateDirectory(relativePath)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(relativePath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(relativePath)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(relativePath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenUNCPath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string uncPath = @"\\server\share\backup";
            _fileSystem.Setup(f => f.CreateDirectory(uncPath)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(uncPath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(uncPath)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(uncPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenPathWithSpecialCharacters_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string specialPath = @"C:\backup folder with spaces & symbols!";
            _fileSystem.Setup(f => f.CreateDirectory(specialPath)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(specialPath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(specialPath)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(specialPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenPathWithTrailingSlash_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string pathWithSlash = @"C:\backup\";
            _fileSystem.Setup(f => f.CreateDirectory(pathWithSlash)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(pathWithSlash);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(pathWithSlash)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(pathWithSlash), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenMultipleCallsWithSamePath_CallsFileSystemMultipleTimes()
        {
            // Arrange
            const string dirName = @"C:\output";
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(dirName);
            await _service.CreateOutputDirectoryAsync(dirName);
            await _service.CreateOutputDirectoryAsync(dirName);

            // Assert
            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Exactly(3));

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3));
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenMultipleCallsWithDifferentPaths_CallsFileSystemForEach()
        {
            // Arrange
            const string dir1 = @"C:\output1";
            const string dir2 = @"C:\output2";
            const string dir3 = @"C:\output3";

            _fileSystem.Setup(f => f.CreateDirectory(It.IsAny<string>())).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(dir1);
            await _service.CreateOutputDirectoryAsync(dir2);
            await _service.CreateOutputDirectoryAsync(dir3);

            // Assert
            _fileSystem.Verify(f => f.CreateDirectory(dir1), Times.Once);
            _fileSystem.Verify(f => f.CreateDirectory(dir2), Times.Once);
            _fileSystem.Verify(f => f.CreateDirectory(dir3), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenNestedPath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string nestedPath = @"C:\level1\level2\level3\level4\backup";
            _fileSystem.Setup(f => f.CreateDirectory(nestedPath)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(nestedPath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(nestedPath)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(nestedPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenSingleCharacterPath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string singleChar = "C";
            _fileSystem.Setup(f => f.CreateDirectory(singleChar)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(singleChar);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(singleChar)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(singleChar), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenTaskCancellationExceptionThrown_ExceptionPropagates()
        {
            // Arrange
            const string dirName = @"C:\backup";
            var expectedException = new TaskCanceledException("Operation was cancelled");
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => _service.CreateOutputDirectoryAsync(dirName));
            Assert.AreEqual(expectedException, actualException);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenGenericExceptionThrown_ExceptionPropagates()
        {
            // Arrange
            const string dirName = @"C:\backup";
            var expectedException = new InvalidOperationException("Something went wrong");
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.CreateOutputDirectoryAsync(dirName));
            Assert.AreEqual(expectedException, actualException);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenCurrentDirectoryPath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string currentDir = ".";
            _fileSystem.Setup(f => f.CreateDirectory(currentDir)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(currentDir);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(currentDir)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(currentDir), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenParentDirectoryPath_CreatesDirectorySuccessfully()
        {
            // Arrange
            const string parentDir = "..";
            _fileSystem.Setup(f => f.CreateDirectory(parentDir)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(parentDir);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory") && v.ToString()!.Contains(parentDir)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(parentDir), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenLongValidPath_CreatesDirectorySuccessfully()
        {
            // Arrange - Create a long but valid path (under Windows limit)
            var longPath = @"C:\backup\" + string.Join(@"\", Enumerable.Repeat("folder", 20));
            _fileSystem.Setup(f => f.CreateDirectory(longPath)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(longPath);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(longPath), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenFileSystemServiceIsNull_ThrowsNullReferenceException()
        {
            // Arrange
            var serviceWithNullFileSystem = new BackupTool.Services.BackupService(
                _unitOfWork.Object,
                _hashService.Object,
                null!, // Null file system
                _logger.Object);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => serviceWithNullFileSystem.CreateOutputDirectoryAsync(@"C:\test"));
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenConcurrentCalls_HandlesCorrectly()
        {
            // Arrange
            const string dirName = @"C:\concurrent";
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).Returns(Task.Delay(100)); // Simulate some work

            // Act
            var task1 = _service.CreateOutputDirectoryAsync(dirName);
            var task2 = _service.CreateOutputDirectoryAsync(dirName);
            var task3 = _service.CreateOutputDirectoryAsync(dirName);

            await Task.WhenAll(task1, task2, task3);

            // Assert
            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Exactly(3));

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Creating output directory")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(3));
        }
    }
}
