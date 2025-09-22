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
        public async Task CreateOutputDirectoryAsync_WhenFullNameIsNull_LogsErrorAndDoesNotCreateDirectory()
        {
            // Act
            await _service.CreateOutputDirectoryAsync(null);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Output directory name is null or empty")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

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
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Output directory name is null or empty")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenFullNameIsValid_LogsInformationAndCreatesDirectory()
        {
            // Arrange
            const string dirName = "C:\\output";
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).Returns(Task.CompletedTask);

            // Act
            await _service.CreateOutputDirectoryAsync(dirName);

            // Assert
            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Creating output directory")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }

        [TestMethod]
        public async Task CreateOutputDirectoryAsync_WhenCreateDirectoryThrows_ExceptionPropagates()
        {
            // Arrange
            const string dirName = "C:\\fail";
            _fileSystem.Setup(f => f.CreateDirectory(dirName)).ThrowsAsync(new System.IO.IOException("Failed"));

            // Act & Assert
            await Assert.ThrowsExceptionAsync<System.IO.IOException>(() => _service.CreateOutputDirectoryAsync(dirName));

            _logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("Creating output directory")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _fileSystem.Verify(f => f.CreateDirectory(dirName), Times.Once);
        }
    }
}
