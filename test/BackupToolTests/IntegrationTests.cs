using BackupTool.Contexts;
using BackupTool.Services;
using BackupTool.Repositories;
using BackupTool.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Text;
using BackupToolTests;

namespace IntegrationTests
{
    [TestClass]
    public class IntegrationTests
    {
        private BackupDbContext _context = null!;
        private IUnitOfWork _unitOfWork = null!;
        private IBackupService _backupService = null!;
        private IHashService _hashService = null!;
        private IFileSystemService _fileSystemService = null!;
        private Mock<ILogger<BackupService>> _logger = null!;
        private string _testRootDirectory = null!;
        private string _sourceDirectory = null!;
        private string _restoreDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create test directories
            _testRootDirectory = Path.Combine(Path.GetTempPath(), "BackupToolIntegrationTests", Guid.NewGuid().ToString());
            _sourceDirectory = Path.Combine(_testRootDirectory, "Source");
            _restoreDirectory = Path.Combine(_testRootDirectory, "Restore");

            Directory.CreateDirectory(_sourceDirectory);
            Directory.CreateDirectory(_restoreDirectory);

            // Setup database
            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BackupDbContext(options);

            // Setup repositories and services
            var snapshotRepository = new SnapshotRepository(_context);
            var fileContentRepository = new FileContentRepository(_context);
            var snapshotFileRepository = new SnapshotFileRepository(_context);
            _unitOfWork = new UnitOfWork(_context);

            _hashService = new Sha256HashService();
            _fileSystemService = new FileSystemService();
            _logger = new Mock<ILogger<BackupService>>();

            _backupService = new BackupService(_unitOfWork, _hashService, _fileSystemService, _logger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Dispose();
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, true);
            }
        }

        #region All Files Restored Tests

        [TestMethod]
        public async Task Integration_WhenSnapshotWithMultipleFiles_AllFilesAreRestored()
        {
            // Arrange - Create multiple files with different content
            var files = new Dictionary<string, byte[]>
            {
                ["file1.txt"] = Encoding.UTF8.GetBytes("This is file 1 content"),
                ["file2.bin"] = [0x01, 0x02, 0x03, 0x04, 0x05],
                ["document.pdf"] = Encoding.UTF8.GetBytes("PDF content simulation"),
                ["image.jpg"] = TestHelpers.GenerateRandomBytes(1024), // Simulate binary image
                ["config.json"] = Encoding.UTF8.GetBytes("{\"setting1\": \"value1\", \"setting2\": 42}")
            };

            foreach (var file in files)
            {
                await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, file.Key), file.Value);
            }

            // Act - Create snapshot and restore
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert - Verify all files are restored
            foreach (var file in files)
            {
                var restoredFilePath = Path.Combine(_restoreDirectory, file.Key);
                Assert.IsTrue(File.Exists(restoredFilePath), $"File {file.Key} was not restored");

                var restoredContent = await File.ReadAllBytesAsync(restoredFilePath);
                CollectionAssert.AreEqual(file.Value, restoredContent, $"File {file.Key} content doesn't match");
            }
        }

        [TestMethod]
        public async Task Integration_WhenSnapshotWithNestedDirectories_AllFilesAreRestoredWithCorrectStructure()
        {
            // Arrange - Create nested directory structure
            var nestedFiles = new Dictionary<string, byte[]>
            {
                ["root.txt"] = Encoding.UTF8.GetBytes("Root level file"),
                [@"folder1\file1.txt"] = Encoding.UTF8.GetBytes("File in folder1"),
                [@"folder1\subfolder\deep.txt"] = Encoding.UTF8.GetBytes("Deep nested file"),
                [@"folder2\file2.bin"] = [0xAA, 0xBB, 0xCC, 0xDD],
                [@"folder2\subfolder\another.txt"] = Encoding.UTF8.GetBytes("Another nested file"),
                [@"empty_folder\will_exist.txt"] = Encoding.UTF8.GetBytes("File in otherwise empty folder")
            };

            foreach (var file in nestedFiles)
            {
                var fullPath = Path.Combine(_sourceDirectory, file.Key);
                var directory = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(fullPath, file.Value);
            }

            // Act - Create snapshot and restore
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert - Verify all files and directory structure
            foreach (var file in nestedFiles)
            {
                var restoredFilePath = Path.Combine(_restoreDirectory, file.Key);
                Assert.IsTrue(File.Exists(restoredFilePath), $"File {file.Key} was not restored");

                var restoredContent = await File.ReadAllBytesAsync(restoredFilePath);
                CollectionAssert.AreEqual(file.Value, restoredContent, $"File {file.Key} content doesn't match");
            }
        }

        #endregion

        #region Bit-for-Bit Identity Tests

        [TestMethod]
        public async Task Integration_WhenRestoringFiles_ContentIsBitForBitIdentical()
        {
            // Arrange - Create files with various binary patterns
            var testFiles = new Dictionary<string, byte[]>
            {
                ["zeros.bin"] = new byte[1000], // All zeros
                ["ones.bin"] = Enumerable.Repeat((byte)0xFF, 500).ToArray(), // All ones
                ["random.bin"] = TestHelpers.GenerateRandomBytes(2048), // Random data
                ["pattern.bin"] = TestHelpers.GeneratePatternBytes(1024), // Repeating pattern
                ["unicode.txt"] = Encoding.UTF8.GetBytes("Hello 世界 🌍 Мир"), // Unicode text
                ["mixed.dat"] = TestHelpers.GenerateMixedContent(512) // Mixed binary/text
            };

            foreach (var file in testFiles)
            {
                await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, file.Key), file.Value);
            }

            // Act - Create snapshot and restore
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert - Verify bit-for-bit identity using hash comparison
            foreach (var file in testFiles)
            {
                var originalPath = Path.Combine(_sourceDirectory, file.Key);
                var restoredPath = Path.Combine(_restoreDirectory, file.Key);

                var originalHash = _hashService.CalculateHash(await File.ReadAllBytesAsync(originalPath));
                var restoredHash = _hashService.CalculateHash(await File.ReadAllBytesAsync(restoredPath));

                Assert.AreEqual(originalHash, restoredHash, $"Hash mismatch for file {file.Key}");

                // Also verify byte-by-byte
                var originalBytes = await File.ReadAllBytesAsync(originalPath);
                var restoredBytes = await File.ReadAllBytesAsync(restoredPath);
                CollectionAssert.AreEqual(originalBytes, restoredBytes, $"Byte-by-byte mismatch for file {file.Key}");
            }
        }

        #endregion

        #region Pruning and Shared Data Tests

        [TestMethod]
        public async Task Integration_WhenPruningSnapshotWithSharedData_OtherSnapshotRemainsRestorable()
        {
            // Arrange - Create initial files
            var sharedContent = Encoding.UTF8.GetBytes("This content is shared between snapshots");
            var uniqueContent1 = Encoding.UTF8.GetBytes("Unique to snapshot 1");
            var uniqueContent2 = Encoding.UTF8.GetBytes("Unique to snapshot 2");

            // Create first snapshot
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "shared.txt"), sharedContent);
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique1.txt"), uniqueContent1);

            var snapshot1Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshot1Id);

            // Modify directory for second snapshot (keep shared file, change unique file)
            File.Delete(Path.Combine(_sourceDirectory, "unique1.txt"));
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique2.txt"), uniqueContent2);

            var snapshot2Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshot2Id);

            // Act - Prune the first snapshot
            await _backupService.PruneSnapshotAsync(snapshot1Id.Value);

            // Assert - Second snapshot should still be fully restorable
            var restoreDir2 = Path.Combine(_restoreDirectory, "Snapshot2");
            Directory.CreateDirectory(restoreDir2);

            await _backupService.RestoreSnapshotAsync(snapshot2Id.Value, restoreDir2);

            // Verify shared content is still available
            var restoredSharedPath = Path.Combine(restoreDir2, "shared.txt");
            Assert.IsTrue(File.Exists(restoredSharedPath), "Shared file not restored after pruning");
            var restoredSharedContent = await File.ReadAllBytesAsync(restoredSharedPath);
            CollectionAssert.AreEqual(sharedContent, restoredSharedContent, "Shared content corrupted");

            // Verify unique content for snapshot 2
            var restoredUnique2Path = Path.Combine(restoreDir2, "unique2.txt");
            Assert.IsTrue(File.Exists(restoredUnique2Path), "Unique file for snapshot 2 not restored");
            var restoredUnique2Content = await File.ReadAllBytesAsync(restoredUnique2Path);
            CollectionAssert.AreEqual(uniqueContent2, restoredUnique2Content, "Unique content 2 corrupted");

            // Verify snapshot 1's unique file is not restored (correct behavior)
            var shouldNotExistPath = Path.Combine(restoreDir2, "unique1.txt");
            Assert.IsFalse(File.Exists(shouldNotExistPath), "Snapshot 1's unique file incorrectly restored");
        }

        [TestMethod]
        public async Task Integration_WhenMultipleSnapshotsShareData_PruningDoesNotAffectOthers()
        {
            // Arrange - Create three snapshots with overlapping content
            var commonFile = Encoding.UTF8.GetBytes("Common content across all snapshots");
            var sharedFile = Encoding.UTF8.GetBytes("Shared between snapshot 1 and 3");

            // Snapshot 1: common.txt, shared.txt, unique1.txt
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "common.txt"), commonFile);
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "shared.txt"), sharedFile);
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique1.txt"), Encoding.UTF8.GetBytes("Unique 1"));
            var snapshot1Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);

            // Snapshot 2: common.txt, unique2.txt
            File.Delete(Path.Combine(_sourceDirectory, "shared.txt"));
            File.Delete(Path.Combine(_sourceDirectory, "unique1.txt"));
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique2.txt"), Encoding.UTF8.GetBytes("Unique 2"));
            var snapshot2Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);

            // Snapshot 3: common.txt, shared.txt, unique3.txt
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "shared.txt"), sharedFile);
            File.Delete(Path.Combine(_sourceDirectory, "unique2.txt"));
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique3.txt"), Encoding.UTF8.GetBytes("Unique 3"));
            var snapshot3Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);

            // Act - Prune snapshot 1 (which shares data with snapshot 3)
            await _backupService.PruneSnapshotAsync(snapshot1Id.Value);

            // Assert - Both remaining snapshots should be fully restorable

            // Test snapshot 2 restoration
            var restoreDir2 = Path.Combine(_restoreDirectory, "Snapshot2");
            Directory.CreateDirectory(restoreDir2);
            await _backupService.RestoreSnapshotAsync(snapshot2Id.Value, restoreDir2);

            Assert.IsTrue(File.Exists(Path.Combine(restoreDir2, "common.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir2, "unique2.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(restoreDir2, "shared.txt")));

            // Test snapshot 3 restoration
            var restoreDir3 = Path.Combine(_restoreDirectory, "Snapshot3");
            Directory.CreateDirectory(restoreDir3);
            await _backupService.RestoreSnapshotAsync(snapshot3Id.Value, restoreDir3);

            Assert.IsTrue(File.Exists(Path.Combine(restoreDir3, "common.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir3, "shared.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir3, "unique3.txt")));

            // Verify content integrity
            var restoredCommon = await File.ReadAllBytesAsync(Path.Combine(restoreDir3, "common.txt"));
            var restoredShared = await File.ReadAllBytesAsync(Path.Combine(restoreDir3, "shared.txt"));
            CollectionAssert.AreEqual(commonFile, restoredCommon);
            CollectionAssert.AreEqual(sharedFile, restoredShared);
        }

        #endregion

        #region Binary Content Tests

        [TestMethod]
        public async Task Integration_WhenHandlingArbitraryBinaryContent_ProcessesCorrectly()
        {
            // Arrange - Create files with various binary content types
            var binaryFiles = new Dictionary<string, byte[]>
            {
                ["executable.exe"] = TestHelpers.GenerateExecutableLikeContent(2048),
                ["image.png"] = TestHelpers.GenerateImageLikeContent(4096),
                ["compressed.zip"] = TestHelpers.GenerateCompressedLikeContent(1024),
                ["null_bytes.dat"] = new byte[500], // All null bytes
                ["high_entropy.bin"] = TestHelpers.GenerateHighEntropyContent(1000),
                ["control_chars.txt"] = TestHelpers.GenerateControlCharacterContent(256),
                ["large_binary.dat"] = TestHelpers.GenerateRandomBytes(10 * 1024) // 10KB
            };

            foreach (var file in binaryFiles)
            {
                await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, file.Key), file.Value);
            }

            // Act - Create snapshot and restore
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert - Verify all binary content is preserved exactly
            foreach (var file in binaryFiles)
            {
                var originalPath = Path.Combine(_sourceDirectory, file.Key);
                var restoredPath = Path.Combine(_restoreDirectory, file.Key);

                Assert.IsTrue(File.Exists(restoredPath), $"Binary file {file.Key} not restored");

                var originalBytes = await File.ReadAllBytesAsync(originalPath);
                var restoredBytes = await File.ReadAllBytesAsync(restoredPath);

                Assert.AreEqual(originalBytes.Length, restoredBytes.Length, $"Size mismatch for {file.Key}");
                CollectionAssert.AreEqual(originalBytes, restoredBytes, $"Content mismatch for {file.Key}");

                // Additional hash verification for critical binary integrity
                var originalHash = _hashService.CalculateHash(originalBytes);
                var restoredHash = _hashService.CalculateHash(restoredBytes);
                Assert.AreEqual(originalHash, restoredHash, $"Hash mismatch for {file.Key}");
            }
        }

        #endregion

        #region Path Handling Tests

        [TestMethod]
        public async Task Integration_WhenHandlingRelativePaths_ProcessesCorrectly()
        {
            // Arrange - Create files in nested structure with various path scenarios
            var currentDir = Environment.CurrentDirectory;
            try
            {
                // Change to source directory to test relative path handling
                Environment.CurrentDirectory = _sourceDirectory;

                var relativeFiles = new Dictionary<string, byte[]>
                {
                    ["file.txt"] = Encoding.UTF8.GetBytes("Root file"),
                    [@"sub\file.txt"] = Encoding.UTF8.GetBytes("Sub file"),
                    [@"sub\deep\file.txt"] = Encoding.UTF8.GetBytes("Deep file"),
                    [@"sub\deep\very\deep\file.txt"] = Encoding.UTF8.GetBytes("Very deep file"),
                    [@"another\path\file.txt"] = Encoding.UTF8.GetBytes("Another path file")
                };

                foreach (var file in relativeFiles)
                {
                    var fullPath = Path.Combine(_sourceDirectory, file.Key);
                    var directory = Path.GetDirectoryName(fullPath)!;
                    Directory.CreateDirectory(directory);
                    await File.WriteAllBytesAsync(fullPath, file.Value);
                }

                // Act - Use relative path for snapshot (from current directory perspective)
                var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, _sourceDirectory);
                var snapshotId = await _backupService.CreateSnapshotAsync(relativePath);
                Assert.IsNotNull(snapshotId);

                await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

                // Assert - Verify all files restored with correct structure
                foreach (var file in relativeFiles)
                {
                    var restoredPath = Path.Combine(_restoreDirectory, file.Key);
                    Assert.IsTrue(File.Exists(restoredPath), $"File {file.Key} not restored from relative path");

                    var restoredContent = await File.ReadAllBytesAsync(restoredPath);
                    CollectionAssert.AreEqual(file.Value, restoredContent, $"Content mismatch for {file.Key}");
                }
            }
            finally
            {
                Environment.CurrentDirectory = currentDir;
            }
        }

        [TestMethod]
        public async Task Integration_WhenHandlingAbsolutePaths_ProcessesCorrectly()
        {
            // Arrange - Create files with absolute path handling
            var absoluteFiles = new Dictionary<string, byte[]>
            {
                ["absolute_test.txt"] = Encoding.UTF8.GetBytes("Absolute path test"),
                [@"folder\absolute_nested.txt"] = Encoding.UTF8.GetBytes("Nested with absolute path"),
                [@"special chars\file with spaces.txt"] = Encoding.UTF8.GetBytes("File with special characters"),
                [@"unicode_path\файл.txt"] = Encoding.UTF8.GetBytes("Unicode filename test")
            };

            foreach (var file in absoluteFiles)
            {
                var fullPath = Path.Combine(_sourceDirectory, file.Key);
                var directory = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(fullPath, file.Value);
            }

            // Act - Use absolute path for snapshot
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            // Use absolute path for restore as well
            var absoluteRestorePath = Path.GetFullPath(_restoreDirectory);
            await _backupService.RestoreSnapshotAsync(snapshotId.Value, absoluteRestorePath);

            // Assert - Verify all files restored correctly with absolute paths
            foreach (var file in absoluteFiles)
            {
                var restoredPath = Path.Combine(absoluteRestorePath, file.Key);
                Assert.IsTrue(File.Exists(restoredPath), $"File {file.Key} not restored with absolute path");

                var restoredContent = await File.ReadAllBytesAsync(restoredPath);
                CollectionAssert.AreEqual(file.Value, restoredContent, $"Content mismatch for {file.Key}");
            }
        }

        #endregion

        #region Deduplication Tests

        [TestMethod]
        public async Task Integration_WhenSnapshotingTwiceWithoutChanges_OnlyStoresMetadata()
        {
            // Arrange - Create initial files
            var files = new Dictionary<string, byte[]>
            {
                ["doc1.txt"] = Encoding.UTF8.GetBytes("Document 1 content"),
                ["doc2.txt"] = Encoding.UTF8.GetBytes("Document 2 content"),
                ["binary.dat"] = TestHelpers.GenerateRandomBytes(1024)
            };

            foreach (var file in files)
            {
                await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, file.Key), file.Value);
            }

            // Act - Create first snapshot
            var snapshot1Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshot1Id);

            // Get initial content count
            var initialContentCount = await _context.FileContents.CountAsync();
            var initialSnapshotFileCount = await _context.SnapshotFiles.CountAsync();

            // Create second snapshot without changes
            var snapshot2Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshot2Id);

            // Assert - Verify deduplication
            var finalContentCount = await _context.FileContents.CountAsync();
            var finalSnapshotFileCount = await _context.SnapshotFiles.CountAsync();

            // Content count should be the same (no duplicate content stored)
            Assert.AreEqual(initialContentCount, finalContentCount,
                "Duplicate file content was stored instead of being deduplicated");

            // Snapshot file count should double (metadata for each snapshot)
            Assert.AreEqual(initialSnapshotFileCount * 2, finalSnapshotFileCount,
                "Snapshot file metadata was not created for second snapshot");

            // Verify both snapshots are independently restorable
            var restoreDir1 = Path.Combine(_restoreDirectory, "Snapshot1");
            var restoreDir2 = Path.Combine(_restoreDirectory, "Snapshot2");
            Directory.CreateDirectory(restoreDir1);
            Directory.CreateDirectory(restoreDir2);

            await _backupService.RestoreSnapshotAsync(snapshot1Id.Value, restoreDir1);
            await _backupService.RestoreSnapshotAsync(snapshot2Id.Value, restoreDir2);

            // Verify both restorations are identical
            foreach (var file in files)
            {
                var restored1Path = Path.Combine(restoreDir1, file.Key);
                var restored2Path = Path.Combine(restoreDir2, file.Key);

                Assert.IsTrue(File.Exists(restored1Path));
                Assert.IsTrue(File.Exists(restored2Path));

                var content1 = await File.ReadAllBytesAsync(restored1Path);
                var content2 = await File.ReadAllBytesAsync(restored2Path);

                CollectionAssert.AreEqual(content1, content2, $"Restored content differs for {file.Key}");
                CollectionAssert.AreEqual(file.Value, content1, $"Original content differs for {file.Key}");
            }
        }

        [TestMethod]
        public async Task Integration_WhenMultipleSnapshotsWithIdenticalFiles_DeduplicatesAcrossSnapshots()
        {
            // Arrange - Create identical content in different snapshot sessions
            var sharedContent = Encoding.UTF8.GetBytes("This content appears in multiple snapshots");
            var uniqueContent1 = Encoding.UTF8.GetBytes("Unique to snapshot 1");
            var uniqueContent2 = Encoding.UTF8.GetBytes("Unique to snapshot 2");

            // First snapshot
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "shared.txt"), sharedContent);
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique1.txt"), uniqueContent1);
            var snapshot1Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);

            // Clear directory and create second snapshot with some shared content
            Directory.Delete(_sourceDirectory, true);
            Directory.CreateDirectory(_sourceDirectory);
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "shared.txt"), sharedContent); // Same content
            await File.WriteAllBytesAsync(Path.Combine(_sourceDirectory, "unique2.txt"), uniqueContent2);
            var snapshot2Id = await _backupService.CreateSnapshotAsync(_sourceDirectory);

            // Assert - Verify deduplication occurred
            var sharedHash = _hashService.CalculateHash(sharedContent);
            var contentWithSharedHash = await _context.FileContents.Where(fc => fc.Hash == sharedHash).ToListAsync();

            Assert.AreEqual(1, contentWithSharedHash.Count,
                "Shared content was not deduplicated across snapshots");

            // Verify both snapshots reference the same content
            var snapshot1Files = await _context.SnapshotFiles.Where(sf => sf.SnapshotId == snapshot1Id).ToListAsync();
            var snapshot2Files = await _context.SnapshotFiles.Where(sf => sf.SnapshotId == snapshot2Id).ToListAsync();

            var snapshot1SharedFile = snapshot1Files.FirstOrDefault(sf => sf.FileName == "shared.txt");
            var snapshot2SharedFile = snapshot2Files.FirstOrDefault(sf => sf.FileName == "shared.txt");

            Assert.IsNotNull(snapshot1SharedFile);
            Assert.IsNotNull(snapshot2SharedFile);
            Assert.AreEqual(snapshot1SharedFile.ContentHash, snapshot2SharedFile.ContentHash,
                "Shared files do not reference the same content hash");
        }

        #endregion

        #region Setup Verification Tests

        [TestMethod]
        public async Task Integration_WhenProjectSetupFollowsInstructions_AllComponentsWork()
        {
            // This test verifies that all components work together as described in README

            // Arrange - Simulate following README instructions
            var testProjectDirectory = Path.Combine(_testRootDirectory, "ProjectTest");
            Directory.CreateDirectory(testProjectDirectory);

            // Create sample files as described in README examples
            await File.WriteAllTextAsync(Path.Combine(testProjectDirectory, "document.txt"),
                "Sample document for testing backup functionality");
            await File.WriteAllTextAsync(Path.Combine(testProjectDirectory, "config.json"),
                @"{""database"": ""backup.db"", ""verbose"": true}");

            var subDir = Path.Combine(testProjectDirectory, "data");
            Directory.CreateDirectory(subDir);
            await File.WriteAllBytesAsync(Path.Combine(subDir, "binary.dat"), TestHelpers.GenerateRandomBytes(512));

            // Act - Test complete workflow as per README

            // 1. Create snapshot
            var snapshotId = await _backupService.CreateSnapshotAsync(testProjectDirectory);
            Assert.IsNotNull(snapshotId, "Failed to create snapshot - basic functionality broken");

            // 2. List snapshots (verify it exists)
            var snapshots = await _backupService.GetSnapshotsAsync();
            Assert.AreEqual(1, snapshots.Count, "Snapshot listing functionality broken");
            Assert.AreEqual(snapshotId, snapshots[0].Id, "Snapshot ID mismatch in listing");

            // 3. Restore snapshot
            var restoreDir = Path.Combine(_testRootDirectory, "RestoreTest");
            await _backupService.CreateOutputDirectoryAsync(restoreDir);
            await _backupService.RestoreSnapshotAsync(snapshotId.Value, restoreDir);

            // 4. Verify restore worked
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir, "document.txt")), "Text file not restored");
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir, "config.json")), "JSON file not restored");
            Assert.IsTrue(File.Exists(Path.Combine(restoreDir, "data", "binary.dat")), "Binary file not restored");

            // 5. Check for corrupted content (should find none)
            var corruptedFiles = await _backupService.CheckForCorruptedContentAsync();
            Assert.AreEqual(0, corruptedFiles.Count, "Corruption detected in fresh backup");

            // 6. Test pruning
            await _backupService.PruneSnapshotAsync(snapshotId.Value);
            var snapshotsAfterPrune = await _backupService.GetSnapshotsAsync();
            Assert.AreEqual(0, snapshotsAfterPrune.Count, "Pruning functionality broken");

            // Assert - All basic operations completed successfully
            Assert.IsTrue(true, "All README workflow steps completed successfully");
        }

        [TestMethod]
        public async Task Integration_WhenDatabaseOperations_WorkWithSQLite()
        {
            // Test that the actual SQLite database works correctly
            // (This simulates the real database usage vs in-memory testing)

            var tempDbPath = Path.Combine(_testRootDirectory, "test_backup.db");

            var options = new DbContextOptionsBuilder<BackupDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            await using var realContext = new BackupDbContext(options);
            await realContext.Database.EnsureCreatedAsync();

            var realUnitOfWork = new UnitOfWork(realContext);
            var realBackupService = new BackupService(realUnitOfWork, _hashService, _fileSystemService, _logger.Object);

            try
            {
                // Arrange - Create test data
                await File.WriteAllTextAsync(Path.Combine(_sourceDirectory, "sqlite_test.txt"),
                    "Testing with real SQLite database");

                // Act - Perform operations with real database
                var snapshotId = await realBackupService.CreateSnapshotAsync(_sourceDirectory);
                Assert.IsNotNull(snapshotId);

                var snapshots = await realBackupService.GetSnapshotsAsync();
                Assert.AreEqual(1, snapshots.Count);

                await realBackupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

                // Assert - Verify real database operations work
                Assert.IsTrue(File.Exists(Path.Combine(_restoreDirectory, "sqlite_test.txt")));
                Assert.IsTrue(File.Exists(tempDbPath), "SQLite database file was not created");

                // Verify database has expected tables and data
                var snapshotCount = await realContext.Snapshots.CountAsync();
                var fileContentCount = await realContext.FileContents.CountAsync();
                var snapshotFileCount = await realContext.SnapshotFiles.CountAsync();

                Assert.AreEqual(1, snapshotCount, "Snapshot not saved to real database");
                Assert.IsTrue(fileContentCount > 0, "File content not saved to real database");
                Assert.IsTrue(snapshotFileCount > 0, "Snapshot files not saved to real database");
            }
            finally
            {
                realUnitOfWork.Dispose();
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
        }

        #endregion

        #region Edge Case and Robustness Tests

        [TestMethod]
        public async Task Integration_WhenLargeFiles_HandlesCorrectly()
        {
            // Test handling of larger files (1MB+)
            var largeFileContent = TestHelpers.GenerateRandomBytes(2 * 1024 * 1024); // 2MB
            var largeFilePath = Path.Combine(_sourceDirectory, "large_file.dat");
            await File.WriteAllBytesAsync(largeFilePath, largeFileContent);

            // Act
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert
            var restoredLargeFile = Path.Combine(_restoreDirectory, "large_file.dat");
            Assert.IsTrue(File.Exists(restoredLargeFile));

            var restoredContent = await File.ReadAllBytesAsync(restoredLargeFile);
            Assert.AreEqual(largeFileContent.Length, restoredContent.Length);

            // Use hash comparison for large files (more efficient than byte-by-byte)
            var originalHash = _hashService.CalculateHash(largeFileContent);
            var restoredHash = _hashService.CalculateHash(restoredContent);
            Assert.AreEqual(originalHash, restoredHash);
        }

        [TestMethod]
        public async Task Integration_WhenEmptyFiles_HandlesCorrectly()
        {
            // Test handling of empty files
            var emptyFilePath = Path.Combine(_sourceDirectory, "empty.txt");
            await File.WriteAllBytesAsync(emptyFilePath, []);

            // Act
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert
            var restoredEmptyFile = Path.Combine(_restoreDirectory, "empty.txt");
            Assert.IsTrue(File.Exists(restoredEmptyFile));

            var restoredContent = await File.ReadAllBytesAsync(restoredEmptyFile);
            Assert.AreEqual(0, restoredContent.Length);
        }

        [TestMethod]
        public async Task Integration_WhenDeepDirectoryNesting_HandlesCorrectly()
        {
            // Test very deep directory nesting
            var deepPath = _sourceDirectory;
            for (int i = 0; i < 20; i++) // Create 20 levels deep
            {
                deepPath = Path.Combine(deepPath, $"level{i}");
                Directory.CreateDirectory(deepPath);
            }

            var deepFile = Path.Combine(deepPath, "deep_file.txt");
            await File.WriteAllTextAsync(deepFile, "File at maximum depth");

            // Act
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert
            var expectedRestorePath = deepFile.Replace(_sourceDirectory, _restoreDirectory);
            Assert.IsTrue(File.Exists(expectedRestorePath));

            var restoredContent = await File.ReadAllTextAsync(expectedRestorePath);
            Assert.AreEqual("File at maximum depth", restoredContent);
        }

        [TestMethod]
        public async Task Integration_WhenManySmallFiles_HandlesCorrectly()
        {
            // Test handling many small files (stress test)
            const int fileCount = 1000;
            var files = new List<string>();

            for (int i = 0; i < fileCount; i++)
            {
                var fileName = $"small_file_{i:D4}.txt";
                var filePath = Path.Combine(_sourceDirectory, fileName);
                await File.WriteAllTextAsync(filePath, $"Content of file {i}");
                files.Add(fileName);
            }

            // Act
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert
            foreach (var fileName in files)
            {
                var restoredPath = Path.Combine(_restoreDirectory, fileName);
                Assert.IsTrue(File.Exists(restoredPath), $"File {fileName} not restored");
            }

            // Verify total file count
            var restoredFiles = Directory.GetFiles(_restoreDirectory, "*", SearchOption.AllDirectories);
            Assert.AreEqual(fileCount, restoredFiles.Length);
        }

        #endregion

        #region Cross-Platform Path Tests

        [TestMethod]
        public async Task Integration_WhenCrossPlatformPaths_HandlesCorrectly()
        {
            // Test path handling that works across different operating systems
            var testFiles = new Dictionary<string, string>
            {
                // Use Path.Combine to ensure proper path separators
                [Path.Combine("folder1", "file1.txt")] = "File in folder1",
                [Path.Combine("folder2", "subfolder", "file2.txt")] = "File in nested folder",
                [Path.Combine("folder3", "file with spaces.txt")] = "File with spaces in name",
                [Path.Combine("unicode", "测试文件.txt")] = "Unicode filename test"
            };

            foreach (var file in testFiles)
            {
                var fullPath = Path.Combine(_sourceDirectory, file.Key);
                var directory = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(fullPath, file.Value);
            }

            // Act
            var snapshotId = await _backupService.CreateSnapshotAsync(_sourceDirectory);
            Assert.IsNotNull(snapshotId);

            await _backupService.RestoreSnapshotAsync(snapshotId.Value, _restoreDirectory);

            // Assert
            foreach (var file in testFiles)
            {
                var restoredPath = Path.Combine(_restoreDirectory, file.Key);
                Assert.IsTrue(File.Exists(restoredPath), $"Cross-platform file {file.Key} not restored");

                var restoredContent = await File.ReadAllTextAsync(restoredPath);
                Assert.AreEqual(file.Value, restoredContent, $"Content mismatch for {file.Key}");
            }
        }

        #endregion
    }
}