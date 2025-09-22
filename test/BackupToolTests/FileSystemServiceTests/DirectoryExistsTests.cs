namespace FileSystemServiceTests
{
    [TestClass]
    public class DirectoryExistsTests
    {
        [TestMethod]
        public void DirectoryExists_WhenDirectoryExists_ReturnsTrue()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var exists = fileSystemService.DirectoryExists(dirPath);

                // Assert
                Assert.IsTrue(exists);
            }
            finally
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath);
            }
        }

        [TestMethod]
        public void DirectoryExists_WhenDirectoryDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.DirectoryExists(dirPath);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void DirectoryExists_WhenPathIsNull_ReturnsFalse()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.DirectoryExists(null!);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void DirectoryExists_WhenPathIsEmpty_ReturnsFalse()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.DirectoryExists(string.Empty);

            // Assert
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public void DirectoryExists_WhenPathIsAFile_ReturnsFalse()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var exists = fileSystemService.DirectoryExists(filePath);

                // Assert
                Assert.IsFalse(exists);
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        [TestMethod]
        public void DirectoryExists_WhenPathIsInvalid_ReturnsFalse()
        {
            // Arrange
            const string invalidPath = "?:\\invalid\\dir";
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act
            var exists = fileSystemService.DirectoryExists(invalidPath);

            // Assert
            Assert.IsFalse(exists);
        }
    }
}
