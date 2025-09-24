using BackupTool.Entities;
using BackupToolTests;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public class CheckForCorruptedContentAsyncTests : BackupServiceTestsBase
    {
        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHasEmptyContentHash_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, string.Empty, "test.txt", "test.txt");
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshotFile, result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHasNullContentHash_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = snapshot.Id,
                ContentHash = null!, // Null hash
                Content = new FileContent { Hash = "validHash", Data = [1, 2, 3], Size = 3 },
                RelativePath = "test.txt",
                FileName = "test.txt"
            };
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshotFile, result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHashDoesNotMatch_ReturnsFileAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, "storedHash123", "test.txt", "test.txt");
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(snapshotFile.Content.Data)).Returns("differentHash456");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(snapshotFile, result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileHashMatchesButDifferentCase_ReturnsFileAsValid()
        {
            // Arrange
            const string validHash = "validHash123";
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, validHash.ToUpper(), "test.txt", "test.txt");
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(snapshotFile.Content.Data)).Returns(validHash.ToLower());

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileIsValid_ReturnsEmptyList()
        {
            // Arrange
            const string validHash = "validHash123";
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var snapshotFile = TestHelpers.CreateTestSnapshotFile(1, snapshot.Id, validHash, "test.txt", "test.txt");
            snapshot.Files.Add(snapshotFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(snapshotFile.Content.Data)).Returns(validHash);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenSnapshotHasNoFiles_ReturnsEmptyList()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            snapshot.Files = null!;

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenSnapshotHasEmptyFilesList_ReturnsEmptyList()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            snapshot.Files = []; // Empty but not null

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenNoSnapshots_ReturnsEmptyList()
        {
            // Arrange
            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenMultipleSnapshotsWithMixedContent_ReturnsOnlyCorruptedFiles()
        {
            // Arrange
            byte[] corruptedData = [9, 9, 9]; // Different content to ensure hash mismatch
            var snapshot1 = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "source1");
            var validFile1 = TestHelpers.CreateTestSnapshotFile(1, 1, "validHash1", "valid1.txt", "valid1.txt");
            var corruptedFile1 = TestHelpers.CreateTestSnapshotFile(2, 1, "storedHash1", "corrupt1.txt", "corrupt1.txt");
            corruptedFile1.Content.Data = corruptedData;
            snapshot1.Files.Add(validFile1);
            snapshot1.Files.Add(corruptedFile1);

            var snapshot2 = TestHelpers.CreateTestSnapshot(2, DateTime.UtcNow, "source2");
            var validFile2 = TestHelpers.CreateTestSnapshotFile(3, 2, "validHash2", "valid2.txt", "valid2.txt");
            var corruptedFile2 = TestHelpers.CreateTestSnapshotFile(4, 2, "storedHash2", "corrupt2.txt", "corrupt2.txt");
            corruptedFile2.Content.Data = corruptedData;
            validFile2.Content.Data = [1, 2, 3, 4]; // Needs valid but different content
            snapshot2.Files.Add(validFile2);
            snapshot2.Files.Add(corruptedFile2);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot1, snapshot2]);
            _hashService.Setup(h => h.CalculateHash(validFile1.Content.Data)).Returns("validHash1");
            _hashService.Setup(h => h.CalculateHash(corruptedFile1.Content.Data)).Returns("differentHash1");
            _hashService.Setup(h => h.CalculateHash(validFile2.Content.Data)).Returns("validHash2");
            _hashService.Setup(h => h.CalculateHash(corruptedFile2.Content.Data)).Returns("differentHash2");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(corruptedFile1));
            Assert.IsTrue(result.Contains(corruptedFile2));
            Assert.IsFalse(result.Contains(validFile1));
            Assert.IsFalse(result.Contains(validFile2));
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenLargeNumberOfFiles_ProcessesAllFiles()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "source");
            var corruptedFiles = new List<SnapshotFile>();

            // Create 1000 files, every 10th file is corrupted
            for (int i = 1; i <= 1000; i++)
            {
                var isCorrupted = i % 10 == 0;
                var hash = $"hash{i}";
                var file = TestHelpers.CreateTestSnapshotFile(i, 1, hash, $"file{i}.txt", $"file{i}.txt");
                file.Content.Data = [.. Enumerable.Range(i, i + 10).Select(i => (byte)i)]; // Unique content
                snapshot.Files.Add(file);

                if (isCorrupted)
                {
                    corruptedFiles.Add(file);
                    _hashService.Setup(h => h.CalculateHash(file.Content.Data)).Returns($"different{i}");
                }
                else
                {
                    _hashService.Setup(h => h.CalculateHash(file.Content.Data)).Returns(hash);
                }
            }

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
             Assert.AreEqual(100, result.Count); // Every 10th file = 100 corrupted files
            foreach (var corruptedFile in corruptedFiles)
                Assert.IsTrue(result.Contains(corruptedFile));
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenEmptyFileContent_HandlesCorrectly()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var emptyFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "emptyHash",
                Content = new FileContent { Hash = "emptyHash", Data = [], Size = 0 },
                RelativePath = "empty.txt",
                FileName = "empty.txt"
            };
            snapshot.Files.Add(emptyFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(emptyFile.Content.Data)).Returns("emptyHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenEmptyFileWithWrongHash_ReturnsAsCorrupted()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var emptyFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "wrongEmptyHash",
                Content = new FileContent { Hash = "wrongEmptyHash", Data = [], Size = 0 },
                RelativePath = "empty.txt",
                FileName = "empty.txt"
            };
            snapshot.Files.Add(emptyFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(emptyFile.Content.Data)).Returns("correctEmptyHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(emptyFile, result.FirstOrDefault());
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenHashServiceThrows_ContinuesWithOtherFiles()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var file1 = TestHelpers.CreateTestSnapshotFile(1, 1, "hash1", "file1.txt", "file1.txt");
            file1.Content.Data = [1];
            var file2 = TestHelpers.CreateTestSnapshotFile(2, 1, "hash2", "file2.txt", "file2.txt");
            file2.Content.Data = [1, 2];
            var file3 = TestHelpers.CreateTestSnapshotFile(3, 1, "hash3", "file3.txt", "file3.txt");
            file3.Content.Data = [1, 2, 3];
            snapshot.Files.Add(file1);
            snapshot.Files.Add(file2);
            snapshot.Files.Add(file3);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(file1.Content.Data)).Returns("hash1");
            _hashService.Setup(h => h.CalculateHash(file2.Content.Data)).Throws(new InvalidOperationException("Hash calculation failed"));
            _hashService.Setup(h => h.CalculateHash(file3.Content.Data)).Returns("differentHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.IsTrue(result.Contains(file3));
            Assert.IsFalse(result.Contains(file1));
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenRepositoryThrows_PropagatesException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Database connection failed");
            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ThrowsAsync(expectedException);

            // Act & Assert
            var actualException = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _service.CheckForCorruptedContentAsync());
            Assert.AreEqual(expectedException, actualException);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenSnapshotFilesCollectionIsNull_SkipsSnapshot()
        {
            // Arrange
            var snapshot1 = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "source1");
            snapshot1.Files = null!;

            var snapshot2 = TestHelpers.CreateTestSnapshot(2, DateTime.UtcNow, "source2");
            var validFile = TestHelpers.CreateTestSnapshotFile(1, 2, "validHash", "valid.txt", "valid.txt");
            snapshot2.Files.Add(validFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot1, snapshot2]);
            _hashService.Setup(h => h.CalculateHash(validFile.Content.Data)).Returns("validHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileContentDataIsNull_ReturnsAsCorrupted()
        {
            // Arrange
            const int expectedCorruptedCount = 1;
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var fileWithNullData = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "hash123",
                Content = new FileContent { Hash = "hash123", Data = null!, Size = 5 },
                RelativePath = "test.txt",
                FileName = "test.txt"
            };
            snapshot.Files.Add(fileWithNullData);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);

            // Act & Assert
            var result = await _service.CheckForCorruptedContentAsync();

            //Assert
            Assert.AreEqual(expectedCorruptedCount, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenMultipleCorruptionTypes_ReturnsAllCorruptedFiles()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);

            // File with null content
            var nullContentFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "hash1",
                Content = null!,
                RelativePath = "null.txt",
                FileName = "null.txt"
            };

            // File with empty hash
            var emptyHashFile = TestHelpers.CreateTestSnapshotFile(2, 1, string.Empty, "empty.txt", "empty.txt");

            // File with mismatched hash
            var mismatchedHashFile = TestHelpers.CreateTestSnapshotFile(3, 1, "storedHash", "mismatch.txt", "mismatch.txt");

            // Valid file
            var validFile = TestHelpers.CreateTestSnapshotFile(4, 1, "validHash", "valid.txt", "valid.txt");

            snapshot.Files.Add(nullContentFile);
            snapshot.Files.Add(emptyHashFile);
            snapshot.Files.Add(mismatchedHashFile);
            snapshot.Files.Add(validFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(mismatchedHashFile.Content.Data)).Returns("calculatedHash");
            _hashService.Setup(h => h.CalculateHash(validFile.Content.Data)).Returns("validHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.Contains(nullContentFile));
            Assert.IsTrue(result.Contains(emptyHashFile));
            Assert.IsTrue(result.Contains(mismatchedHashFile));
            Assert.IsFalse(result.Contains(validFile));
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileWithVeryLargeContent_ProcessesCorrectly()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var largeData = new byte[1024 * 1024]; // 1MB
            Array.Fill(largeData, (byte)0xAB);

            var largeFile = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "largeFileHash",
                Content = new FileContent { Hash = "largeFileHash", Data = largeData, Size = largeData.Length },
                RelativePath = "large.bin",
                FileName = "large.bin"
            };
            snapshot.Files.Add(largeFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(largeData)).Returns("largeFileHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
            _hashService.Verify(h => h.CalculateHash(largeData), Times.Once);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenFileWithSpecialCharacters_ProcessesCorrectly()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var specialFile = TestHelpers.CreateTestSnapshotFile(1, 1, "specialHash", "special & chars!.txt", @"path with spaces\special & chars!.txt");
            snapshot.Files.Add(specialFile);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(specialFile.Content.Data)).Returns("specialHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckForCorruptedContentAsync_WhenDuplicateContentWithDifferentHashes_ReturnsCorruptedOnes()
        {
            // Arrange
            var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, string.Empty);
            var identicalData = new byte[] { 1, 2, 3, 4, 5 };

            // Two files with identical content but different stored hashes
            var file1 = new SnapshotFile
            {
                Id = 1,
                SnapshotId = 1,
                ContentHash = "correctHash",
                Content = new FileContent { Hash = "correctHash", Data = identicalData, Size = 5 },
                RelativePath = "file1.txt",
                FileName = "file1.txt"
            };

            var file2 = new SnapshotFile
            {
                Id = 2,
                SnapshotId = 1,
                ContentHash = "wrongHash",
                Content = new FileContent { Hash = "wrongHash", Data = identicalData, Size = 5 },
                RelativePath = "file2.txt",
                FileName = "file2.txt"
            };

            snapshot.Files.Add(file1);
            snapshot.Files.Add(file2);

            _unitOfWork.Setup(u => u.Snapshots.GetAllAsync()).ReturnsAsync([snapshot]);
            _hashService.Setup(h => h.CalculateHash(identicalData)).Returns("actualHash");

            // Act
            var result = await _service.CheckForCorruptedContentAsync();

            // Assert
            Assert.AreEqual(2, result.Count); // Both files have wrong hashes
            Assert.IsTrue(result.Contains(file1));
            Assert.IsTrue(result.Contains(file2));
        }
    }
}