using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace BackupTool.Services
{
    public class BackupService(IUnitOfWork unitOfWork, IHashService hashService, IFileSystemService fileSystemService, ILogger<BackupService> logger) : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IHashService _hashService = hashService;
        private readonly IFileSystemService _fileSystemService = fileSystemService;
        private readonly ILogger<BackupService> _logger = logger;

        public async Task<int?> CreateSnapshotAsync(string sourceDirectory)
        {
            _logger.LogInformation("Starting snapshot creation for directory: {SourceDirectory}", sourceDirectory);
            var stopwatch = Stopwatch.StartNew();

            if (!_fileSystemService.DirectoryExists(sourceDirectory))
            {
                _logger.LogError("Source directory not found: {SourceDirectory}", sourceDirectory);
                return null;
            }

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var snapshot = new Snapshot
                {
                    SourceDirectory = sourceDirectory,
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogDebug("Creating snapshot record for directory: {SourceDirectory}", sourceDirectory);
                await _unitOfWork.Snapshots.CreateAsync(snapshot);

                var stats = await ProcessDirectoryAsync(sourceDirectory, snapshot.Id, string.Empty);

                await _unitOfWork.CommitTransactionAsync();

                stopwatch.Stop();
                _logger.LogInformation(
                    "Snapshot {SnapshotId} created successfully. Files: {FileCount}, Bytes: {BytesProcessed:N0}, Duration: {Duration:c}",
                    snapshot.Id, stats.FileCount, stats.BytesProcessed, stopwatch.Elapsed);

                return snapshot.Id;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Failed to create snapshot for directory: {SourceDirectory}", sourceDirectory);
                return null;
            }
        }

        private async Task<SnapshotStats> ProcessDirectoryAsync(string directoryPath, int snapshotId, string relativePath)
        {
            _logger.LogDebug("Processing directory: {DirectoryPath} (relative: {RelativePath})", directoryPath, relativePath);

            var stats = new SnapshotStats();

            foreach (var filePath in _fileSystemService.GetFiles(directoryPath))
            {
                try
                {
                    var fileSize = await ProcessFileAsync(filePath, snapshotId, relativePath);
                    stats.FileCount++;
                    stats.BytesProcessed += fileSize;

                    if (stats.FileCount++ % 100 == 0)
                    {
                        _logger.LogInformation("Processed {FileCount} files, {BytesProcessed:N0} bytes", stats.FileCount, stats.BytesProcessed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process file: {FilePath}", filePath);
                }
            }

            foreach (var subDir in _fileSystemService.GetDirectories(directoryPath))
            {
                try
                {
                    var subDirName = Path.GetFileName(subDir);
                    var newRelativePath = Path.Combine(relativePath, subDirName);
                    var sub_stats = await ProcessDirectoryAsync(subDir, snapshotId, newRelativePath);
                    stats.FileCount += sub_stats.FileCount;
                    stats.BytesProcessed += sub_stats.BytesProcessed;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process subdirectory: {SubDirectory}", subDir);
                }
            }
            return stats;
        }

        private async Task<long> ProcessFileAsync(string filePath, int snapshotId, string relativePath)
        {
            _logger.LogTrace("Processing file: {FilePath}", filePath);

            var fileData = await _fileSystemService.ReadFileAsync(filePath);
            var hash = _hashService.CalculateHash(fileData);
            var fileName = Path.GetFileName(filePath);
            var fullRelativePath = Path.Combine(relativePath, fileName);

            var contentExists = await _unitOfWork.FileContents.ExistsAsync(hash);
            if (!contentExists)
            {
                _logger.LogDebug("Storing new file content: {FilePath} (hash: {Hash})", filePath, hash);
                var newContent = new FileContent
                {
                    Hash = hash,
                    Data = fileData,
                    Size = fileData.Length
                };
                await _unitOfWork.FileContents.CreateAsync(newContent);
            }
            else
            {
                _logger.LogTrace("File content already exists: {FilePath} (hash: {Hash})", filePath, hash);
            }

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = snapshotId,
                ContentHash = hash,
                RelativePath = fullRelativePath,
                FileName = fileName
            };

            await _unitOfWork.SnapshotFiles.CreateAsync(snapshotFile);
            return fileData.Length;
        }

        public async Task<List<Snapshot>> GetSnapshotsAsync()
        {
            _logger.LogDebug("Retrieving all snapshots");
            try
            {
                var snapshots = await _unitOfWork.Snapshots.GetAllAsync();
                _logger.LogInformation("Retrieved {SnapshotCount} snapshots", snapshots.Count);
                return snapshots;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve snapshots");
                return Enumerable.Empty<Snapshot>().ToList();
            }
        }

        public async Task RestoreSnapshotAsync(int snapshotId, string outputDirectory)
        {
            _logger.LogInformation("Starting restore of snapshot {SnapshotId} to {OutputDirectory}", snapshotId, outputDirectory);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var snapshotFiles = await _unitOfWork.SnapshotFiles.GetBySnapshotIdAsync(snapshotId);

                if (snapshotFiles.Count > 0)
                    _logger.LogError("Snapshot {SnapshotId} not found for restore", snapshotId);

                _logger.LogInformation("Restoring {FileCount} files from snapshot {SnapshotId}", snapshotFiles.Count, snapshotId);

                var restoredFiles = 0;
                var bytesRestored = 0L;

                foreach (var snapshotFile in snapshotFiles)
                {
                    try
                    {
                        var outputPath = Path.Combine(outputDirectory, snapshotFile.RelativePath);
                        var outputDir = Path.GetDirectoryName(outputPath);

                        if (!string.IsNullOrEmpty(outputDir))
                            _fileSystemService.CreateDirectory(outputDir);

                        await _fileSystemService.WriteFileAsync(outputPath, snapshotFile.Content.Data);
                        restoredFiles++;
                        bytesRestored += snapshotFile.Content.Size;

                        _logger.LogTrace("Restored file: {FilePath}", outputPath);

                        if (restoredFiles % 50 == 0)
                            _logger.LogInformation("Restored {RestoredFiles}/{TotalFiles} files, {BytesRestored:N0} bytes", restoredFiles, snapshotFiles.Count, bytesRestored);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to restore file: {RelativePath}", snapshotFile.RelativePath);
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation(
                    "Restore completed. Snapshot: {SnapshotId}, Files: {RestoredFiles}, Bytes: {BytesRestored:N0}, Duration: {Duration:c}", snapshotId, restoredFiles, bytesRestored, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore snapshot {SnapshotId} to {OutputDirectory}", snapshotId, outputDirectory);
            }
        }

        public async Task PruneSnapshotAsync(int snapshotId)
        {
            _logger.LogInformation("Starting prune of snapshot {SnapshotId}", snapshotId);
            var stopwatch = Stopwatch.StartNew();

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Check if snapshot exists by trying to delete it
                _logger.LogDebug("Deleting snapshot {SnapshotId} record", snapshotId);
                await _unitOfWork.Snapshots.DeleteAsync(snapshotId);

                _logger.LogDebug("Cleaning up orphaned content after pruning snapshot {SnapshotId}", snapshotId);
                var orphanedContent = await _unitOfWork.FileContents.GetOrphanedContentAsync();

                if (orphanedContent.Count > 0)
                {
                    var orphanedSize = orphanedContent.Sum(c => c.Size);
                    _logger.LogInformation("Removing {OrphanedCount} orphaned content entries, {OrphanedSize:N0} bytes",
                        orphanedContent.Count, orphanedSize);
                    await _unitOfWork.FileContents.DeleteRangeAsync(orphanedContent);
                }
                else
                {
                    _logger.LogDebug("No orphaned content found after pruning snapshot {SnapshotId}", snapshotId);
                }

                await _unitOfWork.CommitTransactionAsync();

                stopwatch.Stop();
                _logger.LogInformation("Prune completed for snapshot {SnapshotId}, Duration: {Duration:c}",
                    snapshotId, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Failed to prune snapshot {SnapshotId}", snapshotId);
            }
        }
    }
}