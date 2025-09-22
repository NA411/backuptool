namespace FileSystemServiceTests
{
    [TestClass]
    public class CreateDirectoryTests
    {
        [TestMethod]
        public async Task CreateDirectory_WhenDirectoryDoesNotExist_CreatesDirectory()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                await fileSystemService.CreateDirectory(dirPath);

                // Assert
                Assert.IsTrue(Directory.Exists(dirPath));
            }
            finally
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath);
            }
        }

        [TestMethod]
        public async Task CreateDirectory_WhenDirectoryAlreadyExists_DoesNotThrow()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act & Assert
                await fileSystemService.CreateDirectory(dirPath);
                Assert.IsTrue(Directory.Exists(dirPath));
            }
            finally
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath);
            }
        }

        [TestMethod]
        public async Task CreateDirectory_WhenPathIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await fileSystemService.CreateDirectory(null!));
        }

        [TestMethod]
        public async Task CreateDirectory_WhenPathIsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await fileSystemService.CreateDirectory(string.Empty));
        }

        [TestMethod]
        public async Task CreateDirectory_WhenPathIsAFile_ThrowsIOException()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act & Assert
                await Assert.ThrowsExceptionAsync<IOException>(async () => await fileSystemService.CreateDirectory(filePath));
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public async Task CreateDirectory_WhenPathIsInvalid_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();
            const string invalidPath = "?:\\invalid\\path";

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () => await fileSystemService.CreateDirectory(invalidPath));
        }
    }
}
