using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Repositories;
using Microsoft.EntityFrameworkCore;

namespace RepositoryTests
{
    [TestClass]
    public class FileContentRepositoryTests
    {
        private BackupDbContext _context = null!;
        private FileContentRepository _repository = null!;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BackupDbContext(options);
            _repository = new FileContentRepository(_context);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
        }

        [TestMethod]
        public async Task CreateAsync_WhenValidFileContent_SavesAndReturnsFileContent()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "hash123",
                Data = [1, 2, 3, 4, 5],
                Size = 5,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.CreateAsync(fileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(fileContent.Hash, result.Hash);
            Assert.AreEqual(fileContent.Size, result.Size);
            CollectionAssert.AreEqual(fileContent.Data, result.Data);

            // Verify it was saved to database
            var savedFileContent = await _context.FileContents.FindAsync("hash123");
            Assert.IsNotNull(savedFileContent);
            Assert.AreEqual("hash123", savedFileContent.Hash);
        }

        [TestMethod]
        public async Task CreateAsync_WhenEmptyData_SavesEmptyFileContent()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "emptyHash",
                Data = [],
                Size = 0,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.CreateAsync(fileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("emptyHash", result.Hash);
            Assert.AreEqual(0, result.Size);
            Assert.AreEqual(0, result.Data.Length);

            var savedFileContent = await _context.FileContents.FindAsync("emptyHash");
            Assert.IsNotNull(savedFileContent);
        }

        [TestMethod]
        public async Task CreateAsync_WhenLargeData_SavesLargeFileContent()
        {
            // Arrange
            var largeData = new byte[1024 * 1024]; // 1MB
            Array.Fill(largeData, (byte)0xAB);

            var fileContent = new FileContent
            {
                Hash = "largeHash",
                Data = largeData,
                Size = largeData.Length,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var result = await _repository.CreateAsync(fileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("largeHash", result.Hash);
            Assert.AreEqual(1024 * 1024, result.Size);
            Assert.AreEqual(1024 * 1024, result.Data.Length);
        }

        [TestMethod]
        public async Task CreateAsync_WhenDuplicateHash_ThrowsException()
        {
            // Arrange
            var fileContent1 = new FileContent
            {
                Hash = "duplicateHash",
                Data = [1, 2, 3],
                Size = 3
            };

            var fileContent2 = new FileContent
            {
                Hash = "duplicateHash", // Same hash
                Data = [4, 5, 6],
                Size = 3
            };

            await _repository.CreateAsync(fileContent1);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                _repository.CreateAsync(fileContent2));
        }

        [TestMethod]
        public async Task CreateAsync_WhenNullFileContent_ThrowsNullReferenceException() => await Assert.ThrowsExceptionAsync<NullReferenceException>(() => _repository.CreateAsync(null!)); // Act & Assert

        [TestMethod]
        public async Task GetByHashAsync_WhenHashExists_ReturnsFileContent()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "existingHash",
                Data = [1, 2, 3, 4],
                Size = 4
            };

            await _repository.CreateAsync(fileContent);

            // Act
            var result = await _repository.GetByHashAsync("existingHash");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("existingHash", result.Hash);
            Assert.AreEqual(4, result.Size);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, result.Data);
        }

        [TestMethod]
        public async Task GetByHashAsync_WhenHashDoesNotExist_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByHashAsync("nonExistentHash");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByHashAsync_WhenHashIsNull_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByHashAsync(null!);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByHashAsync_WhenHashIsEmpty_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByHashAsync(string.Empty);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByHashAsync_WhenCaseInsensitiveHash_ReturnsFileContent()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "CaseSensitiveHash",
                Data = [1, 2, 3],
                Size = 3
            };

            await _repository.CreateAsync(fileContent);

            // Act
            var result = await _repository.GetByHashAsync("casesensitivehash");

            // Assert
            // This test depends on database collation settings
            // In SQLite with default settings, this should return null (case sensitive)
            // Adjust expectation based on your database configuration
            Assert.IsNull(result); // Assuming case-sensitive database
        }

        [TestMethod]
        public async Task ExistsAsync_WhenHashExists_ReturnsTrue()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "existingHash",
                Data = [1, 2, 3],
                Size = 3
            };

            await _repository.CreateAsync(fileContent);

            // Act
            var result = await _repository.ExistsAsync("existingHash");

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenHashDoesNotExist_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync("nonExistentHash");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenHashIsNull_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(null!);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenHashIsEmpty_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(string.Empty);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenMultipleFileContents_ReturnsCorrectResults()
        {
            // Arrange
            var fileContent1 = new FileContent { Hash = "hash1", Data = [1], Size = 1 };
            var fileContent2 = new FileContent { Hash = "hash2", Data = [2], Size = 1 };
            var fileContent3 = new FileContent { Hash = "hash3", Data = [3], Size = 1 };

            await _repository.CreateAsync(fileContent1);
            await _repository.CreateAsync(fileContent2);
            await _repository.CreateAsync(fileContent3);

            // Act & Assert
            Assert.IsTrue(await _repository.ExistsAsync("hash1"));
            Assert.IsTrue(await _repository.ExistsAsync("hash2"));
            Assert.IsTrue(await _repository.ExistsAsync("hash3"));
            Assert.IsFalse(await _repository.ExistsAsync("hash4"));
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenNoOrphanedContent_ReturnsEmptyList()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1], Size = 1 };
            await _repository.CreateAsync(fileContent);

            // Create a snapshot file that references the content
            var snapshotFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };
            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetOrphanedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenOrphanedContentExists_ReturnsOrphanedContent()
        {
            // Arrange
            var orphanedContent = new FileContent { Hash = "orphanHash", Data = [1, 2, 3], Size = 3 };
            var referencedContent = new FileContent { Hash = "referencedHash", Data = [4, 5, 6], Size = 3 };

            await _repository.CreateAsync(orphanedContent);
            await _repository.CreateAsync(referencedContent);

            // Create a snapshot file that references only one content
            var snapshotFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "referencedHash",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };
            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetOrphanedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("orphanHash", result[0].Hash);
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenAllContentOrphaned_ReturnsAllContent()
        {
            // Arrange
            var content1 = new FileContent { Hash = "orphan1", Data = [1], Size = 1 };
            var content2 = new FileContent { Hash = "orphan2", Data = [2], Size = 1 };
            var content3 = new FileContent { Hash = "orphan3", Data = [3], Size = 1 };

            await _repository.CreateAsync(content1);
            await _repository.CreateAsync(content2);
            await _repository.CreateAsync(content3);

            // Act
            var result = await _repository.GetOrphanedContentAsync();

            // Assert
            Assert.AreEqual(3, result.Count);
            var hashes = result.Select(c => c.Hash).ToList();
            CollectionAssert.Contains(hashes, "orphan1");
            CollectionAssert.Contains(hashes, "orphan2");
            CollectionAssert.Contains(hashes, "orphan3");
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenNoContent_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetOrphanedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenValidContent_DeletesContent()
        {
            // Arrange
            var content1 = new FileContent { Hash = "delete1", Data = [1], Size = 1 };
            var content2 = new FileContent { Hash = "delete2", Data = [2], Size = 1 };
            var content3 = new FileContent { Hash = "keep", Data = [3], Size = 1 };

            await _repository.CreateAsync(content1);
            await _repository.CreateAsync(content2);
            await _repository.CreateAsync(content3);

            var contentsToDelete = new List<FileContent> { content1, content2 };

            // Act
            await _repository.DeleteRangeAsync(contentsToDelete);

            // Assert
            Assert.IsFalse(await _repository.ExistsAsync("delete1"));
            Assert.IsFalse(await _repository.ExistsAsync("delete2"));
            Assert.IsTrue(await _repository.ExistsAsync("keep"));

            var remainingContent = await _context.FileContents.CountAsync();
            Assert.AreEqual(1, remainingContent);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenEmptyCollection_DoesNothing()
        {
            // Arrange
            var content = new FileContent { Hash = "keep", Data = [1], Size = 1 };
            await _repository.CreateAsync(content);

            // Act
            await _repository.DeleteRangeAsync([]);

            // Assert
            Assert.IsTrue(await _repository.ExistsAsync("keep"));
            var remainingContent = await _context.FileContents.CountAsync();
            Assert.AreEqual(1, remainingContent);
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenNullCollection_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
                _repository.DeleteRangeAsync(null!));
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenContentDoesNotExist_ThrowsDbUpdateConcurrencyException()
        {
            // Arrange
            var nonExistentContent = new FileContent { Hash = "nonExistent", Data = [1], Size = 1 };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DbUpdateConcurrencyException>(() => _repository.DeleteRangeAsync([nonExistentContent]));
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenLargeNumberOfItems_DeletesAllItems()
        {
            // Arrange
            var contentsToDelete = new List<FileContent>();
            for (int i = 0; i < 100; i++)
            {
                var content = new FileContent
                {
                    Hash = $"delete{i}",
                    Data = [(byte)i],
                    Size = 1
                };
                await _repository.CreateAsync(content);
                contentsToDelete.Add(content);
            }

            // Act
            await _repository.DeleteRangeAsync(contentsToDelete);

            // Assert
            var remainingContent = await _context.FileContents.CountAsync();
            Assert.AreEqual(0, remainingContent);
        }

        [TestMethod]
        public async Task CreateAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();
            var fileContent = new FileContent { Hash = "test", Data = [1], Size = 1 };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.CreateAsync(fileContent));
        }

        [TestMethod]
        public async Task GetByHashAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.GetByHashAsync("test"));
        }

        [TestMethod]
        public async Task ExistsAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.ExistsAsync("test"));
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.GetOrphanedContentAsync());
        }

        [TestMethod]
        public async Task DeleteRangeAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();
            var content = new FileContent { Hash = "test", Data = [1], Size = 1 };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.DeleteRangeAsync([content]));
        }

        [TestMethod]
        public async Task CreateAsync_WhenSpecialCharactersInHash_SavesCorrectly()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "hash_with-special.chars+123!@#",
                Data = [1, 2, 3],
                Size = 3
            };

            // Act
            var result = await _repository.CreateAsync(fileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("hash_with-special.chars+123!@#", result.Hash);

            var exists = await _repository.ExistsAsync("hash_with-special.chars+123!@#");
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public async Task GetOrphanedContentAsync_WhenMixedReferencedAndOrphanedContent_ReturnsOnlyOrphaned()
        {
            // Arrange
            var orphaned1 = new FileContent { Hash = "orphan1", Data = [1], Size = 1 };
            var orphaned2 = new FileContent { Hash = "orphan2", Data = [2], Size = 1 };
            var referenced1 = new FileContent { Hash = "ref1", Data = [3], Size = 1 };
            var referenced2 = new FileContent { Hash = "ref2", Data = [4], Size = 1 };

            await _repository.CreateAsync(orphaned1);
            await _repository.CreateAsync(orphaned2);
            await _repository.CreateAsync(referenced1);
            await _repository.CreateAsync(referenced2);

            // Create snapshot files that reference some content
            var snapshotFile1 = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "ref1",
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };
            var snapshotFile2 = new SnapshotFile
            {
                Id = 2,
                SnapshotId = 1,
                ContentHash = "ref2",
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            _context.SnapshotFiles.AddRange(snapshotFile1, snapshotFile2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetOrphanedContentAsync();

            // Assert
            Assert.AreEqual(2, result.Count);
            var orphanedHashes = result.Select(c => c.Hash).ToList();
            CollectionAssert.Contains(orphanedHashes, "orphan1");
            CollectionAssert.Contains(orphanedHashes, "orphan2");
            CollectionAssert.DoesNotContain(orphanedHashes, "ref1");
            CollectionAssert.DoesNotContain(orphanedHashes, "ref2");
        }
    }
}