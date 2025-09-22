using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Repositories;
using Microsoft.EntityFrameworkCore;

namespace RepositoryTests
{
    [TestClass]
    public class UnitOfWorkTests
    {
        private BackupDbContext _context = null!;
        private UnitOfWork _unitOfWork = null!;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BackupDbContext(options);
            _unitOfWork = new UnitOfWork(_context);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _unitOfWork.Dispose();
            _context.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WhenValidContext_InitializesRepositories()
        {
            // Assert
            Assert.IsNotNull(_unitOfWork.Snapshots);
            Assert.IsNotNull(_unitOfWork.FileContents);
            Assert.IsNotNull(_unitOfWork.SnapshotFiles);
            Assert.IsInstanceOfType<SnapshotRepository>(_unitOfWork.Snapshots);
            Assert.IsInstanceOfType<FileContentRepository>(_unitOfWork.FileContents);
            Assert.IsInstanceOfType<SnapshotFileRepository>(_unitOfWork.SnapshotFiles);
        }

        [TestMethod]
        public void Constructor_WhenNullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new UnitOfWork(null!));
        }

        [TestMethod]
        public void Constructor_WhenMultipleInstances_CreatesIndependentRepositories()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context1 = new BackupDbContext(options);
            using var context2 = new BackupDbContext(options);
            using var unitOfWork1 = new UnitOfWork(context1);
            using var unitOfWork2 = new UnitOfWork(context2);

            // Assert
            Assert.AreNotSame(unitOfWork1.Snapshots, unitOfWork2.Snapshots);
            Assert.AreNotSame(unitOfWork1.FileContents, unitOfWork2.FileContents);
            Assert.AreNotSame(unitOfWork1.SnapshotFiles, unitOfWork2.SnapshotFiles);
        }

        #endregion

        #region Repository Property Tests

        [TestMethod]
        public void Snapshots_WhenAccessed_ReturnsSameInstance()
        {
            // Act
            var snapshots1 = _unitOfWork.Snapshots;
            var snapshots2 = _unitOfWork.Snapshots;

            // Assert
            Assert.AreSame(snapshots1, snapshots2);
        }

        [TestMethod]
        public void FileContents_WhenAccessed_ReturnsSameInstance()
        {
            // Act
            var fileContents1 = _unitOfWork.FileContents;
            var fileContents2 = _unitOfWork.FileContents;

            // Assert
            Assert.AreSame(fileContents1, fileContents2);
        }

        [TestMethod]
        public void SnapshotFiles_WhenAccessed_ReturnsSameInstance()
        {
            // Act
            var snapshotFiles1 = _unitOfWork.SnapshotFiles;
            var snapshotFiles2 = _unitOfWork.SnapshotFiles;

            // Assert
            Assert.AreSame(snapshotFiles1, snapshotFiles2);
        }

        #endregion

        #region SaveChangesAsync Tests

        [TestMethod]
        public async Task SaveChangesAsync_WhenNoChanges_ReturnsZero()
        {
            // Act
            var result = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenSingleEntityAdded_ReturnsOne()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "testHash",
                Data = [1, 2, 3],
                Size = 3
            };

            await _unitOfWork.FileContents.CreateAsync(fileContent);

            // Act
            var result = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenMultipleEntitiesAdded_ReturnsCorrectCount()
        {
            // Arrange
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Test",
                CreatedAt = DateTime.UtcNow
            };

            var fileContent = new FileContent
            {
                Hash = "hash123",
                Data = [1, 2, 3],
                Size = 3
            };

            await _unitOfWork.Snapshots.CreateAsync(snapshot);
            await _unitOfWork.FileContents.CreateAsync(fileContent);

            // Act
            var result = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenEntityUpdated_ReturnsOne()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "originalHash",
                Data = [1, 2, 3],
                Size = 3
            };

            var created = await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            // Modify the entity
            created.Size = 5;
            _context.FileContents.Update(created);

            // Act
            var result = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenEntityDeleted_ReturnsOne()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "deleteHash",
                Data = [1, 2, 3],
                Size = 3
            };

            var created = await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            // Delete the entity
            _context.FileContents.Remove(created);

            // Act
            var result = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenCalledMultipleTimes_WorksCorrectly()
        {
            // Arrange & Act
            var result1 = await _unitOfWork.SaveChangesAsync();

            var fileContent = new FileContent { Hash = "hash1", Data = [1], Size = 1 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            var result2 = await _unitOfWork.SaveChangesAsync();

            var result3 = await _unitOfWork.SaveChangesAsync(); // No changes

            // Assert
            Assert.AreEqual(0, result1);
            Assert.AreEqual(1, result2);
            Assert.AreEqual(0, result3);
        }

        [TestMethod]
        public async Task SaveChangesAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _unitOfWork.SaveChangesAsync());
        }

        #endregion

        #region Transaction Tests

        [TestMethod]
        public async Task BeginTransactionAsync_WhenCalled_StartsTransaction()
        {
            // Act
            await _unitOfWork.BeginTransactionAsync();

            // Assert - No exception means transaction started successfully
            // We can verify by checking that subsequent operations work within transaction
            var fileContent = new FileContent { Hash = "transactionTest", Data = [1], Size = 1 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            var result = await _unitOfWork.SaveChangesAsync();
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task BeginTransactionAsync_WhenCalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.BeginTransactionAsync(); // Second call should handle gracefully
        }

        [TestMethod]
        public async Task CommitTransactionAsync_WhenTransactionStarted_CommitsChanges()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();

            var fileContent = new FileContent { Hash = "commitTest", Data = [1, 2], Size = 2 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            // Act
            await _unitOfWork.CommitTransactionAsync();

            // Assert - Verify data is persisted
            var exists = await _unitOfWork.FileContents.ExistsAsync("commitTest");
            Assert.IsTrue(exists);
        }

        [TestMethod]
        public async Task CommitTransactionAsync_WhenNoTransactionStarted_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await _unitOfWork.CommitTransactionAsync();
        }

        [TestMethod]
        public async Task RollbackTransactionAsync_WhenTransactionStarted_RollsBackChanges()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();

            var fileContent = new FileContent { Hash = "rollbackTest", Data = [1, 2, 3], Size = 3 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            // Act
            await _unitOfWork.RollbackTransactionAsync();

            // Assert - Verify data is not persisted
            var exists = await _unitOfWork.FileContents.ExistsAsync("rollbackTest");
            Assert.IsFalse(exists);
        }

        [TestMethod]
        public async Task RollbackTransactionAsync_WhenNoTransactionStarted_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            await _unitOfWork.RollbackTransactionAsync();
        }

        [TestMethod]
        public async Task TransactionLifecycle_WhenBeginCommitSequence_WorksCorrectly()
        {
            // Arrange & Act
            await _unitOfWork.BeginTransactionAsync();

            var fileContent = new FileContent { Hash = "lifecycleTest", Data = [5, 6, 7], Size = 3 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            // Assert
            var retrieved = await _unitOfWork.FileContents.GetByHashAsync("lifecycleTest");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("lifecycleTest", retrieved.Hash);
        }

        [TestMethod]
        public async Task TransactionLifecycle_WhenBeginRollbackSequence_WorksCorrectly()
        {
            // Arrange & Act
            await _unitOfWork.BeginTransactionAsync();

            var fileContent = new FileContent { Hash = "rollbackLifecycle", Data = [8, 9, 10], Size = 3 };
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.RollbackTransactionAsync();

            // Assert
            var retrieved = await _unitOfWork.FileContents.GetByHashAsync("rollbackLifecycle");
            Assert.IsNull(retrieved);
        }

        [TestMethod]
        public async Task BeginTransactionAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _unitOfWork.BeginTransactionAsync());
        }

        [TestMethod]
        public async Task CommitTransactionAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _unitOfWork.CommitTransactionAsync());
        }

        [TestMethod]
        public async Task RollbackTransactionAsync_WhenContextDisposed_ThrowsObjectDisposedException()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();
            _context.Dispose();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
                _unitOfWork.RollbackTransactionAsync());
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_WhenCalled_DisposesResources()
        {
            // Act
            _unitOfWork.Dispose();

            // Assert - Subsequent operations should throw ObjectDisposedException
            Assert.ThrowsException<ObjectDisposedException>(() => _context.SaveChanges());
        }

        [TestMethod]
        public void Dispose_WhenCalledMultipleTimes_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _unitOfWork.Dispose();
            _unitOfWork.Dispose();
            _unitOfWork.Dispose();
        }

        [TestMethod]
        public async Task Dispose_WhenTransactionActive_DisposesTransaction()
        {
            // Arrange
            await _unitOfWork.BeginTransactionAsync();

            // Act
            _unitOfWork.Dispose();

            // Assert - No exception means transaction was properly disposed
            // Context should also be disposed
            Assert.ThrowsException<ObjectDisposedException>(() => _context.SaveChanges());
        }

        [TestMethod]
        public void Dispose_WhenRepositoriesAccessed_StillWorks()
        {
            // Arrange
            var snapshots = _unitOfWork.Snapshots;
            var fileContents = _unitOfWork.FileContents;
            var snapshotFiles = _unitOfWork.SnapshotFiles;

            // Act
            _unitOfWork.Dispose();

            // Assert - Repository references should still exist but context is disposed
            Assert.IsNotNull(snapshots);
            Assert.IsNotNull(fileContents);
            Assert.IsNotNull(snapshotFiles);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task IntegrationTest_WhenCompleteWorkflow_WorksCorrectly()
        {
            // Arrange & Act - Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            // Create entities
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\Integration",
                CreatedAt = DateTime.UtcNow
            };

            var fileContent = new FileContent
            {
                Hash = "integrationHash",
                Data = [1, 2, 3, 4, 5],
                Size = 5
            };

            var createdSnapshot = await _unitOfWork.Snapshots.CreateAsync(snapshot);
            var createdFileContent = await _unitOfWork.FileContents.CreateAsync(fileContent);

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "integrationHash",
                RelativePath = "integration.txt",
                FileName = "integration.txt"
            };

            var createdSnapshotFile = await _unitOfWork.SnapshotFiles.CreateAsync(snapshotFile);

            // Save changes
            var saveResult = await _unitOfWork.SaveChangesAsync();

            // Commit transaction
            await _unitOfWork.CommitTransactionAsync();

            // Assert
            Assert.AreEqual(3, saveResult); // 3 entities saved

            // Verify all entities exist
            var retrievedSnapshot = await _unitOfWork.Snapshots.GetByIdAsync(createdSnapshot.Id);
            Assert.IsNotNull(retrievedSnapshot);
            Assert.AreEqual(@"C:\Integration", retrievedSnapshot.SourceDirectory);

            var retrievedFileContent = await _unitOfWork.FileContents.GetByHashAsync("integrationHash");
            Assert.IsNotNull(retrievedFileContent);
            Assert.AreEqual(5, retrievedFileContent.Size);

            var retrievedSnapshotFiles = await _unitOfWork.SnapshotFiles.GetBySnapshotIdAsync(createdSnapshot.Id);
            Assert.AreEqual(1, retrievedSnapshotFiles.Count);
            Assert.AreEqual("integration.txt", retrievedSnapshotFiles[0].FileName);
        }

        [TestMethod]
        public async Task IntegrationTest_WhenTransactionRolledBack_NoDataPersisted()
        {
            // Arrange & Act - Begin transaction
            await _unitOfWork.BeginTransactionAsync();

            // Create entities
            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\RollbackTest",
                CreatedAt = DateTime.UtcNow
            };

            var fileContent = new FileContent
            {
                Hash = "rollbackIntegrationHash",
                Data = [10, 20, 30],
                Size = 3
            };

            await _unitOfWork.Snapshots.CreateAsync(snapshot);
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            await _unitOfWork.SaveChangesAsync();

            // Rollback transaction
            await _unitOfWork.RollbackTransactionAsync();

            // Assert - No data should be persisted
            var allSnapshots = await _unitOfWork.Snapshots.GetAllAsync();
            Assert.AreEqual(0, allSnapshots.Count);

            var fileContentExists = await _unitOfWork.FileContents.ExistsAsync("rollbackIntegrationHash");
            Assert.IsFalse(fileContentExists);
        }

        [TestMethod]
        public async Task IntegrationTest_WhenConcurrentOperations_HandlesCorrectly()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act - Create multiple entities concurrently
            for (int i = 0; i < 10; i++)
            {
                int index = i; // Capture loop variable
                tasks.Add(Task.Run(async () =>
                {
                    var fileContent = new FileContent
                    {
                        Hash = $"concurrent{index}",
                        Data = [(byte)index],
                        Size = 1
                    };
                    await _unitOfWork.FileContents.CreateAsync(fileContent);
                }));
            }

            await Task.WhenAll(tasks);
            var saveResult = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(10, saveResult);

            // Verify all entities exist
            for (int i = 0; i < 10; i++)
            {
                var exists = await _unitOfWork.FileContents.ExistsAsync($"concurrent{i}");
                Assert.IsTrue(exists);
            }
        }

        [TestMethod]
        public async Task IntegrationTest_WhenRepositoriesShareContext_WorkCorrectly()
        {
            // Arrange
            var fileContent = new FileContent
            {
                Hash = "sharedContextHash",
                Data = [100, 200],
                Size = 2
            };

            var snapshot = new Snapshot
            {
                SourceDirectory = @"C:\SharedContext",
                CreatedAt = DateTime.UtcNow
            };

            // Act - Use different repositories but same context
            await _unitOfWork.FileContents.CreateAsync(fileContent);
            var createdSnapshot = await _unitOfWork.Snapshots.CreateAsync(snapshot);

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = createdSnapshot.Id,
                ContentHash = "sharedContextHash",
                RelativePath = "shared.txt",
                FileName = "shared.txt"
            };

            await _unitOfWork.SnapshotFiles.CreateAsync(snapshotFile);
            var saveResult = await _unitOfWork.SaveChangesAsync();

            // Assert
            Assert.AreEqual(3, saveResult);

            // Verify relationships work correctly
            var retrievedSnapshot = await _unitOfWork.Snapshots.GetByIdAsync(createdSnapshot.Id);
            Assert.IsNotNull(retrievedSnapshot);
            Assert.AreEqual(1, retrievedSnapshot.Files.Count);
            Assert.AreEqual("sharedContextHash", retrievedSnapshot.Files.First().ContentHash);
        }

        #endregion
    }
}