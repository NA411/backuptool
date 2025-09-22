using System.Text;

namespace FileSystemServiceTests
{
    [TestClass]
    public class ReadFileAsyncTests
    {
        [TestMethod]
        public async Task ReadFileAsync_WhenFileExists_ReturnsFileContent()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var expectedContent = Encoding.UTF8.GetBytes("Test content");
            await File.WriteAllBytesAsync(filePath, expectedContent);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var result = await fileSystemService.ReadFileAsync(filePath);
                // Assert
                CollectionAssert.AreEqual(expectedContent, result);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task ReadFileAsync_WhenFileDoesNotExist_ThrowsFileNotFoundException()
        {
            // Arrange
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tmp");
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () => await fileSystemService.ReadFileAsync(filePath));
        }

        [TestMethod]
        public async Task ReadFileAsync_WhenFilePathIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await fileSystemService.ReadFileAsync(null!));
        }

        [TestMethod]
        public async Task ReadFileAsync_WhenFilePathIsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await fileSystemService.ReadFileAsync(string.Empty));
        }

        [TestMethod]
        public async Task ReadFileAsync_WhenFileIsEmpty_ReturnsEmptyByteArray()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            await File.WriteAllBytesAsync(filePath, []);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var result = await fileSystemService.ReadFileAsync(filePath);
                // Assert
                Assert.AreEqual(0, result.Length);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task ReadFileAsync_WhenFileIsLocked_ThrowsIOException()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            await File.WriteAllBytesAsync(filePath, Encoding.UTF8.GetBytes("Locked file"));
            var fileSystemService = new BackupTool.Services.FileSystemService();

            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // Act & Assert
                await Assert.ThrowsExceptionAsync<IOException>(async () => await fileSystemService.ReadFileAsync(filePath));
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
