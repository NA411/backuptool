using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RepositoryTests
{
    [TestClass]
    public class SnapshotFileRepositoryTests
    {
        private BackupDbContext _context = null!;
        private SnapshotFileRepository _repository = null!;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BackupDbContext(options);
            _repository = new SnapshotFileRepository(_context);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
        }

        #region CreateAsync Tests

        [TestMethod]
        public async Task CreateAsync_WhenValidSnapshotFile_SavesAndReturnsSnapshotFile()
        {
            // Arrange
            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash123",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            // Act
            var result = await _repository.CreateAsync(snapshotFile);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.SnapshotId);
            Assert.AreEqual("hash123", result.ContentHash);
            Assert.AreEqual("test.txt", result.RelativePath);
            Assert.AreEqual("test.txt", result.FileName);
            Assert.IsTrue(result.Id > 0); // Should have generated ID

            // Verify it was saved to database
            var savedSnapshotFile = await _context.SnapshotFiles.FindAsync(result.Id);
            Assert.IsNotNull(savedSnapshotFile);
            Assert.AreEqual(result.Id, savedSnapshotFile.Id);
        }

        [TestMethod]
        public async Task CreateAsync_WhenNestedPath_SavesCorrectly()
        {
            // Arrange
            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "nestedHash",
                RelativePath = @"folder\subfolder\deep\file.txt",
                FileName = "file.txt"
            };

            // Act
            var result = await _repository.CreateAsync(snapshotFile);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(@"folder\subfolder\deep\file.txt", result.RelativePath);
            Assert.AreEqual("file.txt", result.FileName);
        }

        [TestMethod]
        public async Task CreateAsync_WhenSpecialCharactersInPath_SavesCorrectly()
        {
            // Arrange
            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "specialHash",
                RelativePath = @"folder with spaces\file & symbols!.txt",
                FileName = "file & symbols!.txt"
            };

            // Act
            var result = await _repository.CreateAsync(snapshotFile);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(@"folder with spaces\file & symbols!.txt", result.RelativePath);
            Assert.AreEqual("file & symbols!.txt", result.FileName);
        }

        [TestMethod]
        public async Task CreateAsync_WhenMultipleFilesForSameSnapshot_SavesAll()
        {
            // Arrange
            var snapshotFile1 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };

            var snapshotFile2 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash2",
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            // Act
            var result1 = await _repository.CreateAsync(snapshotFile1);
            var result2 = await _repository.CreateAsync(snapshotFile2);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreNotEqual(result1.Id, result2.Id);
            Assert.AreEqual(1, result1.SnapshotId);
            Assert.AreEqual(1, result2.SnapshotId);

            var count = await _context.SnapshotFiles.CountAsync(sf => sf.SnapshotId == 1);
            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public async Task CreateAsync_WhenSameRelativePathDifferentSnapshots_SavesSuccessfully()
        {
            // Arrange
            var snapshotFile1 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "same.txt",
                FileName = "same.txt"
            };

            var snapshotFile2 = new SnapshotFile
            {
                SnapshotId = 2,
                ContentHash = "hash2",
                RelativePath = "same.txt", // Same relative path but different snapshot
                FileName = "same.txt"
            };

            // Act
            var result1 = await _repository.CreateAsync(snapshotFile1);
            var result2 = await _repository.CreateAsync(snapshotFile2);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(1, result1.SnapshotId);
            Assert.AreEqual(2, result2.SnapshotId);
        }

        [TestMethod]
        public async Task CreateAsync_WhenNullSnapshotFile_ThrowsNullReferenceException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => _repository.CreateAsync(null!));
        }

        [TestMethod]
        public async Task CreateAsync_WhenEmptyStrings_SavesWithEmptyValues()
        {
            // Arrange
            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "",
                RelativePath = "",
                FileName = ""
            };

            // Act
            var result = await _repository.CreateAsync(snapshotFile);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("", result.ContentHash);
            Assert.AreEqual("", result.RelativePath);
            Assert.AreEqual("", result.FileName);
        }

        #endregion

        #region GetBySnapshotIdAsync Tests

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenSnapshotHasFiles_ReturnsAllFiles()
        {
            // Arrange
            var fileContent1 = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            var fileContent2 = new FileContent { Hash = "hash2", Data = [4, 5, 6], Size = 3 };

            _context.FileContents.AddRange(fileContent1, fileContent2);
            await _context.SaveChangesAsync();

            var snapshotFile1 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };

            var snapshotFile2 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash2",
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            await _repository.CreateAsync(snapshotFile1);
            await _repository.CreateAsync(snapshotFile2);

            // Act
            var result = await _repository.GetBySnapshotIdAsync(1);

            // Assert
            Assert.AreEqual(2, result.Count);

            var file1 = result.FirstOrDefault(f => f.RelativePath == "file1.txt");
            var file2 = result.FirstOrDefault(f => f.RelativePath == "file2.txt");

            Assert.IsNotNull(file1);
            Assert.IsNotNull(file2);
            Assert.AreEqual("hash1", file1.ContentHash);
            Assert.AreEqual("hash2", file2.ContentHash);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenSnapshotHasNoFiles_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetBySnapshotIdAsync(999);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenMultipleSnapshots_ReturnsOnlyMatchingFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshot1File = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "snapshot1.txt",
                FileName = "snapshot1.txt"
            };

            var snapshot2File = new SnapshotFile
            {
                SnapshotId = 2,
                ContentHash = "hash1",
                RelativePath = "snapshot2.txt",
                FileName = "snapshot2.txt"
            };

            await _repository.CreateAsync(snapshot1File);
            await _repository.CreateAsync(snapshot2File);

            // Act
            var result = await _repository.GetBySnapshotIdAsync(1);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("snapshot1.txt", result[0].RelativePath);
            Assert.AreEqual(1, result[0].SnapshotId);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenFilesIncludeContent_LoadsContentCorrectly()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "contentHash",
                Data = [10, 20, 30, 40, 50],
                Size = 5,
                CreatedAt = DateTime.UtcNow
            };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "contentHash",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            await _repository.CreateAsync(snapshotFile);

            // Act
            var result = await _repository.GetBySnapshotIdAsync(1);

            // Assert
            Assert.AreEqual(1, result.Count);
            var file = result[0];
            Assert.AreEqual("contentHash", file.Content.Hash);
            Assert.AreEqual(5, file.Content.Size);
            CollectionAssert.AreEqual(new byte[] { 10, 20, 30, 40, 50 }, file.Content.Data);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenLargeNumberOfFiles_ReturnsAllFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "sharedHash", Data = [1], Size = 1 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            const int fileCount = 100;
            for (int i = 0; i < fileCount; i++)
            {
                var snapshotFile = new SnapshotFile
                {
                    SnapshotId = 1,
                    ContentHash = "sharedHash",
                    RelativePath = $"file{i}.txt",
                    FileName = $"file{i}.txt"
                };
                await _repository.CreateAsync(snapshotFile);
            }

            // Act
            var result = await _repository.GetBySnapshotIdAsync(1);

            // Assert
            Assert.AreEqual(fileCount, result.Count);

            // Verify all files are unique and correctly numbered
            var relativePaths = result.Select(f => f.RelativePath).OrderBy(p => p, new AlphanumericComparer()).ToList();
            for (int i = 0; i < fileCount; i++)
                Assert.AreEqual($"file{i}.txt", relativePaths[i]);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenNegativeSnapshotId_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetBySnapshotIdAsync(-1);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenZeroSnapshotId_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetBySnapshotIdAsync(0);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region DeleteBySnapshotIdAsync Tests

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenSnapshotHasFiles_DeletesAllFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshotFile1 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };

            var snapshotFile2 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            await _repository.CreateAsync(snapshotFile1);
            await _repository.CreateAsync(snapshotFile2);

            // Verify files exist before deletion
            var beforeDelete = await _repository.GetBySnapshotIdAsync(1);
            Assert.AreEqual(2, beforeDelete.Count);

            // Act
            await _repository.DeleteBySnapshotIdAsync(1);

            // Assert
            var afterDelete = await _repository.GetBySnapshotIdAsync(1);
            Assert.AreEqual(0, afterDelete.Count);

            var totalCount = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(0, totalCount);
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenSnapshotHasNoFiles_DoesNothing()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var otherSnapshotFile = new SnapshotFile
            {
                SnapshotId = 2,
                ContentHash = "hash1",
                RelativePath = "other.txt",
                FileName = "other.txt"
            };

            await _repository.CreateAsync(otherSnapshotFile);

            // Act
            await _repository.DeleteBySnapshotIdAsync(1); // Non-existent snapshot

            // Assert
            var remainingFiles = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(1, remainingFiles); // Other snapshot file should remain

            var otherFiles = await _repository.GetBySnapshotIdAsync(2);
            Assert.AreEqual(1, otherFiles.Count);
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenMultipleSnapshots_DeletesOnlySpecifiedSnapshot()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshot1File1 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "s1f1.txt",
                FileName = "s1f1.txt"
            };

            var snapshot1File2 = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "s1f2.txt",
                FileName = "s1f2.txt"
            };

            var snapshot2File = new SnapshotFile
            {
                SnapshotId = 2,
                ContentHash = "hash1",
                RelativePath = "s2f1.txt",
                FileName = "s2f1.txt"
            };

            await _repository.CreateAsync(snapshot1File1);
            await _repository.CreateAsync(snapshot1File2);
            await _repository.CreateAsync(snapshot2File);

            // Act
            await _repository.DeleteBySnapshotIdAsync(1);

            // Assert
            var snapshot1Files = await _repository.GetBySnapshotIdAsync(1);
            Assert.AreEqual(0, snapshot1Files.Count);

            var snapshot2Files = await _repository.GetBySnapshotIdAsync(2);
            Assert.AreEqual(1, snapshot2Files.Count);
            Assert.AreEqual("s2f1.txt", snapshot2Files[0].RelativePath);
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenLargeNumberOfFiles_DeletesAllFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1], Size = 1 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            const int fileCount = 100;
            for (int i = 0; i < fileCount; i++)
            {
                var snapshotFile = new SnapshotFile
                {
                    SnapshotId = 1,
                    ContentHash = "hash1",
                    RelativePath = $"bulk{i}.txt",
                    FileName = $"bulk{i}.txt"
                };
                await _repository.CreateAsync(snapshotFile);
            }

            // Verify files exist
            var beforeDelete = await _repository.GetBySnapshotIdAsync(1);
            Assert.AreEqual(fileCount, beforeDelete.Count);

            // Act
            await _repository.DeleteBySnapshotIdAsync(1);

            // Assert
            var afterDelete = await _repository.GetBySnapshotIdAsync(1);
            Assert.AreEqual(0, afterDelete.Count);

            var totalCount = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(0, totalCount);
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenNegativeSnapshotId_DoesNothing()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            await _repository.CreateAsync(snapshotFile);

            // Act
            await _repository.DeleteBySnapshotIdAsync(-1);

            // Assert
            var remainingFiles = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(1, remainingFiles);
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenZeroSnapshotId_DoesNothing()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            await _repository.CreateAsync(snapshotFile);

            // Act
            await _repository.DeleteBySnapshotIdAsync(0);

            // Assert
            var remainingFiles = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(1, remainingFiles);
        }

        #endregion

        #region Context Disposal Tests

        [TestMethod]
        public async Task CreateAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();
            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.CreateAsync(snapshotFile));
        }

        [TestMethod]
        public async Task GetBySnapshotIdAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.GetBySnapshotIdAsync(1));
        }

        [TestMethod]
        public async Task DeleteBySnapshotIdAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.DeleteBySnapshotIdAsync(1));
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task FullLifeCycle_CreateRetrieveDelete_WorksCorrectly()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "lifecycleHash", Data = [1, 2, 3, 4, 5], Size = 5 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = 99,
                ContentHash = "lifecycleHash",
                RelativePath = "lifecycle.txt",
                FileName = "lifecycle.txt"
            };

            // Act & Assert - Create
            var created = await _repository.CreateAsync(snapshotFile);
            Assert.IsNotNull(created);
            Assert.IsTrue(created.Id > 0);

            // Act & Assert - Retrieve
            var retrieved = await _repository.GetBySnapshotIdAsync(99);
            Assert.AreEqual(1, retrieved.Count);
            Assert.AreEqual("lifecycle.txt", retrieved[0].RelativePath);
            Assert.AreEqual(5, retrieved[0].Content.Size);

            // Act & Assert - Delete
            await _repository.DeleteBySnapshotIdAsync(99);
            var afterDelete = await _repository.GetBySnapshotIdAsync(99);
            Assert.AreEqual(0, afterDelete.Count);
        }

        [TestMethod]
        public async Task ConcurrentOperations_WhenMultipleSnapshots_HandleCorrectly()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "concurrentHash", Data = [1], Size = 1 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            // Create files for multiple snapshots
            var tasks = new List<Task>();
            for (int snapshotId = 1; snapshotId <= 5; snapshotId++)
            {
                for (int fileId = 1; fileId <= 10; fileId++)
                {
                    var snapshotFile = new SnapshotFile
                    {
                        SnapshotId = snapshotId,
                        ContentHash = "concurrentHash",
                        RelativePath = $"s{snapshotId}f{fileId}.txt",
                        FileName = $"s{snapshotId}f{fileId}.txt"
                    };
                    tasks.Add(_repository.CreateAsync(snapshotFile));
                }
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            for (int snapshotId = 1; snapshotId <= 5; snapshotId++)
            {
                var files = await _repository.GetBySnapshotIdAsync(snapshotId);
                Assert.AreEqual(10, files.Count);
            }

            var totalFiles = await _context.SnapshotFiles.CountAsync();
            Assert.AreEqual(50, totalFiles);
        }
        #endregion
    }
    public class AlphanumericComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x is null && y is null)
                return 0;
            if (x is null)
                return -1;
            if (y is null)
                return 1;

            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    // Extract numeric parts
                    string numX = "";
                    string numY = "";

                    while (i < x.Length && char.IsDigit(x[i]))
                        numX += x[i++];

                    while (j < y.Length && char.IsDigit(y[j]))
                        numY += y[j++];

                    // Compare as integers
                    int result = int.Parse(numX).CompareTo(int.Parse(numY));
                    if (result != 0) return result;
                }
                else
                {
                    // Compare as characters
                    int result = x[i].CompareTo(y[j]);
                    if (result != 0) return result;
                    i++;
                    j++;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}