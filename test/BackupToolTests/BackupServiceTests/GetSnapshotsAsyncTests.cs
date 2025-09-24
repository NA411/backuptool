using BackupTool.Entities;
using BackupToolTests;
using Moq;

namespace BackupServiceTests;

[TestClass]
public class GetSnapshotsAsyncTests : BackupServiceTestsBase
{
    [TestMethod]
    public async Task GetSnapshotsAsync_WhenNoSnapshotsExist_ShouldReturnEmptyList()
    {
        // Arrange
        var emptySnapshotList = new List<Snapshot>();
        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(emptySnapshotList);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenSingleSnapshotExists_ShouldReturnListWithOneSnapshot()
    {
        // Arrange
        var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "/test/path");
        var snapshotList = new List<Snapshot> { snapshot };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshotList);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(snapshot.Id, result[0].Id);
        Assert.AreEqual(snapshot.SourceDirectory, result[0].SourceDirectory);
        Assert.AreEqual(snapshot.CreatedAt, result[0].CreatedAt);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenMultipleSnapshotsExist_ShouldReturnAllSnapshots()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var snapshots = new List<Snapshot>
            {
                TestHelpers.CreateTestSnapshot(1, baseTime.AddDays(-2), "/test/path1"),
                TestHelpers.CreateTestSnapshot(2, baseTime.AddDays(-1), "/test/path2"),
                TestHelpers.CreateTestSnapshot(3, baseTime, "/test/path3")
            };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshots);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.Any(s => s.Id == 1));
        Assert.IsTrue(result.Any(s => s.Id == 2));
        Assert.IsTrue(result.Any(s => s.Id == 3));
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenSnapshotsHaveFiles_ShouldReturnSnapshotsWithFiles()
    {
        // Arrange
        var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "/test/path");
        var fileContent = new FileContent
        {
            Hash = "test-hash-123",
            Data = [1, 2, 3, 4, 5],
            Size = 5,
            CreatedAt = DateTime.UtcNow
        };

        var snapshotFile = new SnapshotFile
        {
            Id = 1,
            SnapshotId = 1,
            ContentHash = "test-hash-123",
            Content = fileContent,
            RelativePath = "test/file.txt",
            FileName = "file.txt"
        };

        snapshot.Files = [snapshotFile];
        var snapshotList = new List<Snapshot> { snapshot };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshotList);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        var returnedSnapshot = result[0];
        Assert.AreEqual(1, returnedSnapshot.Files.Count);
        Assert.AreEqual("file.txt", returnedSnapshot.Files.First().FileName);
        Assert.AreEqual("test/file.txt", returnedSnapshot.Files.First().RelativePath);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenSnapshotsHaveMultipleFiles_ShouldReturnAllFiles()
    {
        // Arrange
        var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "/test/path");
        var files = new List<SnapshotFile>
            {
                TestHelpers.CreateTestSnapshotFile(1, 1, "hash1", "file1.txt", "dir1/file1.txt"),
                TestHelpers.CreateTestSnapshotFile(2, 1, "hash2", "file2.txt", "dir2/file2.txt"),
                TestHelpers.CreateTestSnapshotFile(3, 1, "hash3", "file3.txt", "dir3/file3.txt")
            };

        snapshot.Files = files;
        var snapshotList = new List<Snapshot> { snapshot };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshotList);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        var returnedSnapshot = result[0];
        Assert.AreEqual(3, returnedSnapshot.Files.Count);
        Assert.IsTrue(returnedSnapshot.Files.Any(f => f.FileName == "file1.txt"));
        Assert.IsTrue(returnedSnapshot.Files.Any(f => f.FileName == "file2.txt"));
        Assert.IsTrue(returnedSnapshot.Files.Any(f => f.FileName == "file3.txt"));
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenRepositoryThrowsException_ShouldReturnEmpty()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Database connection failed");
        _snapshotRepository.Setup(x => x.GetAllAsync()).ThrowsAsync(expectedException);

        // Act & Assert
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.AreEqual(true, result.Count == 0);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenRepositoryReturnsNull_ShouldHandleGracefully()
    {
        // Arrange
        _snapshotRepository.Setup(x => x.GetAllAsync()).ReturnsAsync((List<Snapshot>)null!);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsTrue(result == null || result.Count == 0);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenCalledMultipleTimes_ShouldCallRepositoryEachTime()
    {
        // Arrange
        var snapshots = new List<Snapshot>
        {
            TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "/test/path")
        };

        _snapshotRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(snapshots);

        // Act
        await _service.GetSnapshotsAsync();
        await _service.GetSnapshotsAsync();
        await _service.GetSnapshotsAsync();

        // Assert
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Exactly(3));
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenSnapshotsOrderedById_ShouldMaintainOrder()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var snapshots = new List<Snapshot>
            {
                TestHelpers.CreateTestSnapshot(3, baseTime.AddDays(-1), "/test/path3"),
                TestHelpers.CreateTestSnapshot(1, baseTime.AddDays(-3), "/test/path1"),
                TestHelpers.CreateTestSnapshot(2, baseTime.AddDays(-2), "/test/path2")
            };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshots);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        // Verify the order is maintained as returned by repository
        Assert.AreEqual(3, result[0].Id);
        Assert.AreEqual(1, result[1].Id);
        Assert.AreEqual(2, result[2].Id);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenSnapshotsHaveEmptyFilesCollection_ShouldReturnSnapshotsWithEmptyFiles()
    {
        // Arrange
        var snapshot = TestHelpers.CreateTestSnapshot(1, DateTime.UtcNow, "/test/path");
        snapshot.Files = []; // Empty but not null
        var snapshotList = new List<Snapshot> { snapshot };

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshotList);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        var returnedSnapshot = result[0];
        Assert.AreEqual(0, returnedSnapshot.Files.Count);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetSnapshotsAsync_WhenLargeNumberOfSnapshots_ShouldReturnAllSnapshots()
    {
        // Arrange
        var snapshots = new List<Snapshot>();
        var baseTime = DateTime.UtcNow;

        for (int i = 1; i <= 1000; i++)
        {
            snapshots.Add(TestHelpers.CreateTestSnapshot(i, baseTime.AddDays(-i), $"/test/path{i}"));
        }

        _snapshotRepository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(snapshots);

        // Act
        var result = await _service.GetSnapshotsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1000, result.Count);
        Assert.AreEqual(1, result[0].Id);
        Assert.AreEqual(1000, result[^1].Id);
        _snapshotRepository.Verify(x => x.GetAllAsync(), Times.Once);
    }
}