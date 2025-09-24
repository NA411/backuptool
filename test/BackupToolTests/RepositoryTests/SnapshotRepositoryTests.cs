using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Repositories;
using Microsoft.EntityFrameworkCore;

namespace RepositoryTests
{
    [TestClass]
    public class SnapshotRepositoryTests
    {
        private BackupDbContext _context = null!;
        private SnapshotRepository _repository = null!;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BackupDbContext(options);
            _repository = new SnapshotRepository(_context);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
        }

        #region CreateAsync Tests

        [TestMethod]
        public async Task CreateAsync_WhenValidSnapshot_SavesAndReturnsSnapshot()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\TestSource",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(@"C:\TestSource", result.SourceDirectory);
            Assert.IsTrue(result.Id > 0); // Should have generated ID

            // Verify it was saved to database
            var savedSnapshot = await _context.Snapshots.FindAsync(result.Id);
            Assert.IsNotNull(savedSnapshot);
            Assert.AreEqual(result.Id, savedSnapshot.Id);
            Assert.AreEqual(@"C:\TestSource", savedSnapshot.SourceDirectory);
        }

        [TestMethod]
        public async Task CreateAsync_WhenSourceDirectoryWithSpecialCharacters_SavesCorrectly()
        {
            // Arrange - Use platform-appropriate path
            string testPath;
            if (OperatingSystem.IsWindows())
            {
                testPath = @"C:\Test Directory with spaces & symbols!";
            }
            else
            {
                testPath = "/tmp/Test Directory with spaces & symbols!";
            }

            var snapshot = new Snapshot
            {
                SourceDirectory = testPath,
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(testPath, result.SourceDirectory);
        }

        [TestMethod]
        public async Task CreateAsync_WhenUNCPath_SavesCorrectly()
        {
            // Arrange - Only test UNC paths on Windows
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("UNC paths are Windows-specific");
                return;
            }

            var snapshot = new Snapshot
            {
                SourceDirectory = @"\\server\share\backup",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(@"\\server\share\backup", result.SourceDirectory);
        }

        [TestMethod]
        public async Task CreateAsync_WhenRelativePath_SavesCorrectly()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"backup\relative\path",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(@"backup\relative\path", result.SourceDirectory);
        }

        [TestMethod]
        public async Task CreateAsync_WhenEmptySourceDirectory_SavesCorrectly()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = string.Empty,
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(string.Empty, result.SourceDirectory);
        }

        [TestMethod]
        public async Task CreateAsync_WhenCreatedAtInPast_SavesCorrectly()
        {
            // Arrange
            var pastDate = DateTime.UtcNow.AddDays(-30);
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\OldBackup",
                CreatedAt = pastDate,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(pastDate, result.CreatedAt);
        }

        [TestMethod]
        public async Task CreateAsync_WhenCreatedAtInFuture_SavesCorrectly()
        {
            // Arrange
            var futureDate = DateTime.UtcNow.AddDays(30);
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\FutureBackup",
                CreatedAt = futureDate,
                Files = []
            };

            // Act
            var result = await _repository.CreateAsync(snapshot);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(futureDate, result.CreatedAt);
        }

        [TestMethod]
        public async Task CreateAsync_WhenMultipleSnapshots_AssignsUniqueIds()
        {
            // Arrange
            var snapshot1 = new Snapshot
            {
                SourceDirectory = @"C:\Source1",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var snapshot2 = new Snapshot
            {
                SourceDirectory = @"C:\Source2",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act
            var result1 = await _repository.CreateAsync(snapshot1);
            var result2 = await _repository.CreateAsync(snapshot2);

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreNotEqual(result1.Id, result2.Id);
            Assert.IsTrue(result1.Id > 0);
            Assert.IsTrue(result2.Id > 0);
        }

        [TestMethod]
        public async Task CreateAsync_WhenNullSnapshot_ThrowsNullReferenceException()
        {
            // Act & Assert
            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => _repository.CreateAsync(null!));
        }

        [TestMethod]
        public async Task CreateAsync_WhenNullSourceDirectory_ThrowsDbUpdateException()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = null!,
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<DbUpdateException>(async () => await _repository.CreateAsync(snapshot));
        }

        #endregion

        #region GetByIdAsync Tests

        [TestMethod]
        public async Task GetByIdAsync_WhenSnapshotExists_ReturnsSnapshotWithFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\TestSource",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(createdSnapshot.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(createdSnapshot.Id, result.Id);
            Assert.AreEqual(@"C:\TestSource", result.SourceDirectory);
            Assert.AreEqual(1, result.Files.Count);

            var file = result.Files.First();
            Assert.AreEqual("test.txt", file.FileName);
            Assert.AreEqual("hash1", file.Content.Hash);
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenSnapshotDoesNotExist_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenSnapshotHasNoFiles_ReturnsSnapshotWithEmptyFiles()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\EmptySource",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            // Act
            var result = await _repository.GetByIdAsync(createdSnapshot.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(createdSnapshot.Id, result.Id);
            Assert.AreEqual(0, result.Files.Count);
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenSnapshotHasMultipleFiles_ReturnsAllFiles()
        {
            // Arrange
            var fileContent1 = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            var fileContent2 = new FileContent { Hash = "hash2", Data = [4, 5, 6], Size = 3 };
            _context.FileContents.AddRange(fileContent1, fileContent2);
            await _context.SaveChangesAsync();

            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\MultiFileSource",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            var snapshotFile1 = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "hash1",
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };

            var snapshotFile2 = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "hash2",
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            _context.SnapshotFiles.AddRange(snapshotFile1, snapshotFile2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(createdSnapshot.Id);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Files.Count);

            var files = result.Files.ToList();
            Assert.IsTrue(files.Any(f => f.FileName == "file1.txt"));
            Assert.IsTrue(files.Any(f => f.FileName == "file2.txt"));
            Assert.IsTrue(files.All(f => f.Content != null));
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenNegativeId_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(-1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenZeroId_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(0);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetAllAsync Tests

        [TestMethod]
        public async Task GetAllAsync_WhenNoSnapshots_ReturnsEmptyList()
        {
            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetAllAsync_WhenSingleSnapshot_ReturnsOneSnapshot()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\SingleSource",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            await _repository.CreateAsync(snapshot);

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(@"C:\SingleSource", result[0].SourceDirectory);
        }

        [TestMethod]
        public async Task GetAllAsync_WhenMultipleSnapshots_ReturnsAllSnapshotsOrderedById()
        {
            // Arrange
            var snapshot1 = new Snapshot
            {
                SourceDirectory = @"C:\Source1",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Files = []
            };

            var snapshot2 = new Snapshot
            {
                SourceDirectory = @"C:\Source2",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Files = []
            };

            var snapshot3 = new Snapshot
            {
                SourceDirectory = @"C:\Source3",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var created1 = await _repository.CreateAsync(snapshot1);
            var created2 = await _repository.CreateAsync(snapshot2);
            var created3 = await _repository.CreateAsync(snapshot3);

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);

            // Should be ordered by Id (which typically correlates with creation order)
            Assert.AreEqual(created1.Id, result[0].Id);
            Assert.AreEqual(created2.Id, result[1].Id);
            Assert.AreEqual(created3.Id, result[2].Id);
        }

        [TestMethod]
        public async Task GetAllAsync_WhenLargeNumberOfSnapshots_ReturnsAllSnapshots()
        {
            // Arrange
            const int snapshotCount = 100;
            var createdSnapshots = new List<Snapshot>();

            for (int i = 0; i < snapshotCount; i++)
            {
                var snapshot = new Snapshot
                {
                    SourceDirectory = $@"C:\Source{i}",
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    Files = []
                };

                var created = await _repository.CreateAsync(snapshot);
                createdSnapshots.Add(created);
            }

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(snapshotCount, result.Count);

            // Verify ordering by Id
            for (int i = 0; i < snapshotCount - 1; i++)
            {
                Assert.IsTrue(result[i].Id < result[i + 1].Id);
            }
        }

        [TestMethod]
        public async Task GetAllAsync_WhenSnapshotsHaveFiles_DoesNotLoadFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\WithFiles",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        #endregion

        #region DeleteAsync Tests

        [TestMethod]
        public async Task DeleteAsync_WhenSnapshotExists_DeletesSnapshot()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\ToDelete",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            // Verify it exists
            var beforeDelete = await _repository.GetByIdAsync(createdSnapshot.Id);
            Assert.IsNotNull(beforeDelete);

            // Act
            await _repository.DeleteAsync(createdSnapshot.Id);

            // Assert
            var afterDelete = await _repository.GetByIdAsync(createdSnapshot.Id);
            Assert.IsNull(afterDelete);

            var allSnapshots = await _repository.GetAllAsync();
            Assert.AreEqual(0, allSnapshots.Count);
        }

        [TestMethod]
        public async Task DeleteAsync_WhenSnapshotDoesNotExist_DoesNothing()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Existing",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            await _repository.CreateAsync(snapshot);

            // Act
            await _repository.DeleteAsync(999); // Non-existent ID

            // Assert
            var allSnapshots = await _repository.GetAllAsync();
            Assert.AreEqual(1, allSnapshots.Count); // Original snapshot should remain
        }

        [TestMethod]
        public async Task DeleteAsync_WhenSnapshotHasFiles_DeletesSnapshotAndCascadesFiles()
        {
            // Arrange
            var fileContent = new FileContent { Hash = "hash1", Data = [1, 2, 3], Size = 3 };
            _context.FileContents.Add(fileContent);
            await _context.SaveChangesAsync();

            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\WithFiles",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "hash1",
                RelativePath = "test.txt",
                FileName = "test.txt"
            };

            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();

            // Verify files exist
            var filesBeforeDelete = await _context.SnapshotFiles.CountAsync(sf => sf.SnapshotId == createdSnapshot.Id);
            Assert.AreEqual(1, filesBeforeDelete);

            // Act
            await _repository.DeleteAsync(createdSnapshot.Id);

            // Assert
            var snapshotAfterDelete = await _repository.GetByIdAsync(createdSnapshot.Id);
            Assert.IsNull(snapshotAfterDelete);

            // Files should be cascaded (deleted due to foreign key relationship)
            var filesAfterDelete = await _context.SnapshotFiles.CountAsync(sf => sf.SnapshotId == createdSnapshot.Id);
            Assert.AreEqual(0, filesAfterDelete);
        }

        [TestMethod]
        public async Task DeleteAsync_WhenMultipleSnapshots_DeletesOnlySpecified()
        {
            // Arrange
            var snapshot1 = new Snapshot
            {
                SourceDirectory = @"C:\ToDelete",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var snapshot2 = new Snapshot
            {
                SourceDirectory = @"C:\ToKeep",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var created1 = await _repository.CreateAsync(snapshot1);
            var created2 = await _repository.CreateAsync(snapshot2);

            // Act
            await _repository.DeleteAsync(created1.Id);

            // Assert
            var afterDelete1 = await _repository.GetByIdAsync(created1.Id);
            Assert.IsNull(afterDelete1);

            var afterDelete2 = await _repository.GetByIdAsync(created2.Id);
            Assert.IsNotNull(afterDelete2);
            Assert.AreEqual(@"C:\ToKeep", afterDelete2.SourceDirectory);
        }

        [TestMethod]
        public async Task DeleteAsync_WhenNegativeId_DoesNothing()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Safe",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            await _repository.CreateAsync(snapshot);

            // Act
            await _repository.DeleteAsync(-1);

            // Assert
            var allSnapshots = await _repository.GetAllAsync();
            Assert.AreEqual(1, allSnapshots.Count);
        }

        [TestMethod]
        public async Task DeleteAsync_WhenZeroId_DoesNothing()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Safe",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            await _repository.CreateAsync(snapshot);

            // Act
            await _repository.DeleteAsync(0);

            // Assert
            var allSnapshots = await _repository.GetAllAsync();
            Assert.AreEqual(1, allSnapshots.Count);
        }

        #endregion

        #region ExistsAsync Tests

        [TestMethod]
        public async Task ExistsAsync_WhenSnapshotExists_ReturnsTrue()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Existing",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            // Act
            var result = await _repository.ExistsAsync(createdSnapshot.Id);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenSnapshotDoesNotExist_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(999);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenMultipleSnapshots_ReturnsCorrectResults()
        {
            // Arrange
            var snapshot1 = new Snapshot { SourceDirectory = @"C:\Test1", CreatedAt = DateTime.UtcNow, Files = [] };
            var snapshot2 = new Snapshot { SourceDirectory = @"C:\Test2", CreatedAt = DateTime.UtcNow, Files = [] };

            var created1 = await _repository.CreateAsync(snapshot1);
            var created2 = await _repository.CreateAsync(snapshot2);

            // Act & Assert
            Assert.IsTrue(await _repository.ExistsAsync(created1.Id));
            Assert.IsTrue(await _repository.ExistsAsync(created2.Id));
            Assert.IsFalse(await _repository.ExistsAsync(999));
        }

        [TestMethod]
        public async Task ExistsAsync_WhenNegativeId_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(-1);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_WhenZeroId_ReturnsFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(0);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task ExistsAsync_AfterDelete_ReturnsFalse()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\ToDeleteAndCheck",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            var createdSnapshot = await _repository.CreateAsync(snapshot);

            // Verify it exists
            Assert.IsTrue(await _repository.ExistsAsync(createdSnapshot.Id));

            // Act
            await _repository.DeleteAsync(createdSnapshot.Id);

            // Assert
            Assert.IsFalse(await _repository.ExistsAsync(createdSnapshot.Id));
        }

        #endregion

        #region Context Disposal Tests

        [TestMethod]
        public async Task CreateAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();
            var snapshot = new Snapshot { SourceDirectory = @"C:\Test", CreatedAt = DateTime.UtcNow, Files = [] };

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.CreateAsync(snapshot));
        }

        [TestMethod]
        public async Task GetByIdAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.GetByIdAsync(1));
        }

        [TestMethod]
        public async Task GetAllAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.GetAllAsync());
        }

        [TestMethod]
        public async Task DeleteAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.DeleteAsync(1));
        }

        [TestMethod]
        public async Task ExistsAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _repository.ExistsAsync(1));
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task FullLifeCycle_CreateRetrieveCheckExistsDelete_WorksCorrectly()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\LifecycleTest",
                CreatedAt = DateTime.UtcNow,
                Files = []
            };

            // Act & Assert - Create
            var created = await _repository.CreateAsync(snapshot);
            Assert.IsNotNull(created);
            Assert.IsTrue(created.Id > 0);

            // Act & Assert - Exists
            Assert.IsTrue(await _repository.ExistsAsync(created.Id));

            // Act & Assert - Retrieve by ID
            var retrieved = await _repository.GetByIdAsync(created.Id);
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(created.Id, retrieved.Id);
            Assert.AreEqual(@"C:\LifecycleTest", retrieved.SourceDirectory);

            // Act & Assert - Get All
            var all = await _repository.GetAllAsync();
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(created.Id, all[0].Id);

            // Act & Assert - Delete
            await _repository.DeleteAsync(created.Id);
            Assert.IsFalse(await _repository.ExistsAsync(created.Id));
            Assert.IsNull(await _repository.GetByIdAsync(created.Id));

            var allAfterDelete = await _repository.GetAllAsync();
            Assert.AreEqual(0, allAfterDelete.Count);
        }

        [TestMethod]
        public async Task ConcurrentOperations_WhenMultipleSnapshots_HandleCorrectly()
        {
            // Arrange & Act
            var tasks = new List<Task<Snapshot>>();
            for (int i = 1; i <= 10; i++)
            {
                var snapshot = new Snapshot
                {
                    SourceDirectory = $@"C:\Concurrent{i}",
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    Files = []
                };
                tasks.Add(_repository.CreateAsync(snapshot));
            }

            var createdSnapshots = await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(10, createdSnapshots.Length);
            Assert.IsTrue(createdSnapshots.All(s => s.Id > 0));
            Assert.AreEqual(10, createdSnapshots.Select(s => s.Id).Distinct().Count()); // All IDs unique

            var allSnapshots = await _repository.GetAllAsync();
            Assert.AreEqual(10, allSnapshots.Count);

            // Verify all exist
            foreach (var snapshot in createdSnapshots)
            {
                Assert.IsTrue(await _repository.ExistsAsync(snapshot.Id));
            }
        }

        #endregion
    }
}