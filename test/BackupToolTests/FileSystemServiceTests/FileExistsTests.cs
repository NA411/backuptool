namespace FileSystemServiceTests
{
    [TestClass]
    public class FileExistsTests
    {
        [TestMethod]
        public void FileExists_WhenFileExists_ReturnsTrue()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var exists = fileSystemService.FileExists(filePath);

                // Assert
                Assert.IsTrue(exists);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public void FileExists_WhenFileDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tmp");
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.FileExists(filePath);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void FileExists_WhenPathIsNull_ReturnsFalse()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.FileExists(null!);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void FileExists_WhenPathIsEmpty_ReturnsFalse()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.FileExists(string.Empty);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void FileExists_WhenPathIsDirectory_ReturnsFalse()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var exists = fileSystemService.FileExists(dirPath);

                // Assert
                Assert.IsFalse(exists);
            }
            finally
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath);
            }
        }

        [TestMethod]
        public void FileExists_WhenPathIsInvalid_ReturnsFalse()
        {
            // Arrange
            const string invalidPath = "?:\\invalid\\file.txt";
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.FileExists(invalidPath);

            // Assert
            Assert.IsFalse(exists);
        }
    }
}
