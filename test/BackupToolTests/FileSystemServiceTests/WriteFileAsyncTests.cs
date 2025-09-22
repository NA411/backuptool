using System.Text;

namespace FileSystemServiceTests
{
    [TestClass]
    public class WriteFileAsyncTests
    {
        [TestMethod]
        public async Task WriteFileAsync_WhenFilePathIsValid_WritesDataToFile()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Hello, BackupTool!");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                await fileSystemService.WriteFileAsync(filePath, data);

                // Assert
                var writtenData = await File.ReadAllBytesAsync(filePath);
                CollectionAssert.AreEqual(data, writtenData);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenFileDoesNotExist_CreatesFile()
        {
            // Arrange
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tmp");
            var data = Encoding.UTF8.GetBytes("New file content");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                await fileSystemService.WriteFileAsync(filePath, data);

                // Assert
                Assert.IsTrue(File.Exists(filePath));
                var writtenData = await File.ReadAllBytesAsync(filePath);
                CollectionAssert.AreEqual(data, writtenData);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenDataIsEmpty_WritesEmptyFile()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var data = Array.Empty<byte>();
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                await fileSystemService.WriteFileAsync(filePath, data);

                // Assert
                var writtenData = await File.ReadAllBytesAsync(filePath);
                Assert.AreEqual(0, writtenData.Length);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenFilePathIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();
            var data = Encoding.UTF8.GetBytes("Null path");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await fileSystemService.WriteFileAsync(null!, data));
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenFilePathIsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();
            var data = Encoding.UTF8.GetBytes("Empty path");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await fileSystemService.WriteFileAsync(string.Empty, data));
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenDataIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act & Assert
                await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await fileSystemService.WriteFileAsync(filePath, null!));
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task WriteFileAsync_WhenFileIsLocked_ThrowsIOException()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var data = Encoding.UTF8.GetBytes("Locked file");
            var fileSystemService = new BackupTool.Services.FileSystemService();

            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Act & Assert
                await Assert.ThrowsExceptionAsync<IOException>(async () => await fileSystemService.WriteFileAsync(filePath, data));
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
