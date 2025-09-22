using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BackupServiceTests;

[TestClass]
public class PruneSnapshotAsyncTests : BackupServiceTestsBase
{
    [TestMethod]
    public async Task PruneSnapshotAsync_WhenSnapshotExists_DeletesSnapshotSuccessfully()
    {
        // Arrange
        const int snapshotId = 1;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.Snapshots.DeleteAsync(snapshotId), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Never);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenOrphanedContentExists_RemovesOrphanedContent()
    {
        // Arrange
        const int snapshotId = 1;
        var orphanedContent = new List<FileContent>
            {
                new() { Hash = "hash1", Data = [1, 2, 3], Size = 3 },
                new() { Hash = "hash2", Data = [4, 5, 6], Size = 3 }
            };

        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync(orphanedContent);
        _fileContentRepository.Setup(x => x.DeleteRangeAsync(orphanedContent)).Returns(Task.CompletedTask);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _fileContentRepository.Verify(x => x.GetOrphanedContentAsync(), Times.Once);
        _fileContentRepository.Verify(x => x.DeleteRangeAsync(orphanedContent), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenNoOrphanedContent_DoesNotDeleteContent()
    {
        // Arrange
        const int snapshotId = 1;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _fileContentRepository.Verify(x => x.GetOrphanedContentAsync(), Times.Once);
        _fileContentRepository.Verify(x => x.DeleteRangeAsync(It.IsAny<IEnumerable<FileContent>>()), Times.Never);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenSnapshotDeleteThrows_RollsBackTransaction()
    {
        // Arrange
        const int snapshotId = 1;
        var exception = new InvalidOperationException("Delete failed");
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).ThrowsAsync(exception);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenGetOrphanedContentThrows_RollsBackTransaction()
    {
        // Arrange
        const int snapshotId = 1;
        var exception = new InvalidOperationException("Database error");
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ThrowsAsync(exception);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenDeleteOrphanedContentThrows_RollsBackTransaction()
    {
        // Arrange
        const int snapshotId = 1;
        var orphanedContent = new List<FileContent>
            {
                new() { Hash = "hash1", Data = [1, 2, 3], Size = 3 }
            };
        var exception = new InvalidOperationException("Delete content failed");

        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync(orphanedContent);
        _fileContentRepository.Setup(x => x.DeleteRangeAsync(orphanedContent)).ThrowsAsync(exception);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Never);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenCommitTransactionThrows_RollsBackTransaction()
    {
        // Arrange
        const int snapshotId = 1;
        var exception = new InvalidOperationException("Commit failed");
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);
        _unitOfWork.Setup(x => x.CommitTransactionAsync()).ThrowsAsync(exception);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.RollbackTransactionAsync(), Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenSuccessful_LogsInformation()
    {
        // Arrange
        const int snapshotId = 42;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Starting prune of snapshot {snapshotId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Prune completed for snapshot {snapshotId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenOrphanedContentFound_LogsOrphanedContentInfo()
    {
        // Arrange
        const int snapshotId = 1;
        var orphanedContent = new List<FileContent>
            {
                new() { Hash = "hash1", Data = [1, 2, 3], Size = 100 },
                new() { Hash = "hash2", Data = [4, 5, 6], Size = 200 }
            };

        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync(orphanedContent);
        _fileContentRepository.Setup(x => x.DeleteRangeAsync(orphanedContent)).Returns(Task.CompletedTask);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Removing 2 orphaned content entries") && v.ToString()!.Contains("300 bytes")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenNoOrphanedContentFound_LogsDebugMessage()
    {
        // Arrange
        const int snapshotId = 1;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"No orphaned content found after pruning snapshot {snapshotId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenExceptionOccurs_LogsError()
    {
        // Arrange
        const int snapshotId = 1;
        var exception = new InvalidOperationException("Test exception");
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).ThrowsAsync(exception);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains($"Failed to prune snapshot {snapshotId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenCalledWithZeroId_ProcessesNormally()
    {
        // Arrange
        const int snapshotId = 0;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.Snapshots.DeleteAsync(snapshotId), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenCalledWithNegativeId_ProcessesNormally()
    {
        // Arrange
        const int snapshotId = -1;
        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync([]);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _unitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Once);
        _unitOfWork.Verify(x => x.Snapshots.DeleteAsync(snapshotId), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenLargeNumberOfOrphanedContent_HandlesCorrectly()
    {
        // Arrange
        const int snapshotId = 1;
        var orphanedContent = new List<FileContent>();

        // Create 1000 orphaned content items
        for (int i = 1; i <= 1000; i++)
        {
            orphanedContent.Add(new FileContent
            {
                Hash = $"hash{i}",
                Data = [1, 2, 3, 4, 5],
                Size = 1024 // 1KB each
            });
        }

        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync(orphanedContent);
        _fileContentRepository.Setup(x => x.DeleteRangeAsync(orphanedContent)).Returns(Task.CompletedTask);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _fileContentRepository.Verify(x => x.DeleteRangeAsync(orphanedContent), Times.Once);
        _unitOfWork.Verify(x => x.CommitTransactionAsync(), Times.Once);

        // Verify logging of the large number
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Removing 1000 orphaned content entries") && v.ToString()!.Contains("1,024,000 bytes")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PruneSnapshotAsync_WhenOrphanedContentHasZeroSize_CalculatesCorrectTotalSize()
    {
        // Arrange
        const int snapshotId = 1;
        var orphanedContent = new List<FileContent>
            {
                new() { Hash = "hash1", Data = [], Size = 0 },
                new() { Hash = "hash2", Data = [1, 2], Size = 0 } // Size property is 0 regardless of Data
            };

        _unitOfWork.Setup(x => x.Snapshots.DeleteAsync(snapshotId)).Returns(Task.CompletedTask);
        _fileContentRepository.Setup(x => x.GetOrphanedContentAsync()).ReturnsAsync(orphanedContent);
        _fileContentRepository.Setup(x => x.DeleteRangeAsync(orphanedContent)).Returns(Task.CompletedTask);

        // Act
        await _service.PruneSnapshotAsync(snapshotId);

        // Assert
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Removing 2 orphaned content entries") && v.ToString()!.Contains("0 bytes")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
