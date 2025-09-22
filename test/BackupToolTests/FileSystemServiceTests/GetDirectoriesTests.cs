using System.Threading.Tasks;

namespace FileSystemServiceTests
{
    [TestClass]
    public class GetDirectoriesTests
    {
        [TestMethod]
        public void GetDirectories_WhenDirectoryContainsSubdirectories_ReturnsAllDirectories()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var subDir1 = Path.Combine(dirPath, "sub1");
            var subDir2 = Path.Combine(dirPath, "sub2");
            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var dirs = fileSystemService.GetDirectories(dirPath).ToList();

                // Assert
                Assert.AreEqual(2, dirs.Count);
                CollectionAssert.AreEquivalent(new[] { subDir1, subDir2 }, dirs);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }

        [TestMethod]
        public void GetDirectories_WhenDirectoryIsEmpty_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var dirs = fileSystemService.GetDirectories(dirPath).ToList();

                // Assert
                Assert.AreEqual(0, dirs.Count);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }

        [TestMethod]
        public void GetDirectories_WhenDirectoryDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<DirectoryNotFoundException>(() => fileSystemService.GetDirectories(dirPath).ToList());
        }

        [TestMethod]
        public void GetDirectories_WhenPathIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => fileSystemService.GetDirectories(null!).ToList());
        }

        [TestMethod]
        public void GetDirectories_WhenPathIsEmpty_ThrowsArgumentException()
        {
            // Arrange
            var fileSystemService = new BackupTool.Services.FileSystemService();

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => fileSystemService.GetDirectories(string.Empty).ToList());
        }

        [TestMethod]
        public void GetDirectories_WhenDirectoryContainsFilesOnly_ReturnsEmptyEnumerable()
        {
            // Arrange
            var dirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dirPath);
            var file1 = Path.Combine(dirPath, "file1.txt");
            File.WriteAllText(file1, "A");
            var fileSystemService = new BackupTool.Services.FileSystemService();
            try
            {
                // Act
                var dirs = fileSystemService.GetDirectories(dirPath).ToList();

                // Assert
                Assert.AreEqual(0, dirs.Count);
            }
            finally
            {
                Directory.Delete(dirPath, true);
            }
        }
    }
}
