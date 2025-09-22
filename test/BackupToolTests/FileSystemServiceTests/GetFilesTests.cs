namespace FileSystemServiceTests
{
    [TestClass]
    public class GetFilesTests
    {
        [TestMethod]
        public void GetFiles_WhenDirectoryContainsFiles_ReturnsAllFiles()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var file1 = Path.Combine(dirPath, "file1.txt");
            var file2 = Path.Combine(dirPath, "file2.txt");
            File.WriteAllText(file1, "A");
            File.WriteAllText(file2, "B");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var files = fileSystemService.GetFiles(dirPath).ToList();

                // Assert
                Assert.AreEqual(2, files.Count);
                CollectionAssert.AreEquivalent(new[] { file1, file2 }, files);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }

        [TestMethod]
        public void GetFiles_WhenDirectoryIsEmpty_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var files = fileSystemService.GetFiles(dirPath).ToList();

                // Assert
                Assert.AreEqual(0, files.Count);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }

        [TestMethod]
        public void GetFiles_WhenUsingSearchPattern_ReturnsMatchingFiles()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var file1 = Path.Combine(dirPath, "a.txt");
            var file2 = Path.Combine(dirPath, "b.log");
            File.WriteAllText(file1, "A");
            File.WriteAllText(file2, "B");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var txtFiles = fileSystemService.GetFiles(dirPath, "*.txt").ToList();

                // Assert
                Assert.AreEqual(1, txtFiles.Count);
                Assert.AreEqual(file1, txtFiles[0]);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }

        [TestMethod]
        public void GetFiles_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<DirectoryNotFoundException>(() => fileSystemService.GetFiles(dirPath).ToList());
        }

        [TestMethod]
        public void GetFiles_WhenPathIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => fileSystemService.GetFiles(null!).ToList());
        }

        [TestMethod]
        public void GetFiles_WhenPathIsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => fileSystemService.GetFiles(string.Empty).ToList());
        }

        [TestMethod]
        public void GetFiles_WhenNoFilesMatchPattern_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var file1 = Path.Combine(dirPath, "a.txt");
            File.WriteAllText(file1, "A");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var logFiles = fileSystemService.GetFiles(dirPath, "*.log").ToList();

                // Assert
                Assert.AreEqual(0, logFiles.Count);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }
    }
}
