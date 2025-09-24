
using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BackupTool.Services
{
    /// <summary>
    /// Core application service that orchestrates all backup operations including snapshot creation,
    /// restoration, pruning, and integrity checking. Implements content-based de-duplication and
    /// transactional safety for reliable backup operations.
    /// </summary>
    public class BackupService(IUnitOfWork unitOfWork, IHashService hashService, IFileSystemService fileSystemService, ILogger<BackupService> logger) : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IHashService _hashService = hashService;
        private readonly IFileSystemService _fileSystemService = fileSystemService;
        private readonly ILogger<BackupService> _logger = logger;

        /// <summary>
        /// Creates a complete snapshot of the specified directory and all its subdirectories.
        /// Uses content-based de-duplication to store only unique file content and maintains
        /// transactional integrity throughout the operation.
        /// </summary>
        /// <param name="sourceDirectory">The full path to the directory to backup</param>
        /// <returns>The ID of the created snapshot if successful, null if the operation failed</returns>
        /// <exception cref="ArgumentException">Thrown when sourceDirectory is null or empty</exception>
        public async Task<int?> CreateSnapshotAsync(string sourceDirectory)
        {
            _logger.LogInformation("Starting snapshot creation for directory: {SourceDirectory}", sourceDirectory);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!_fileSystemService.DirectoryExists(sourceDirectory))
                {
                    _logger.LogError("Source directory not found: {SourceDirectory}", sourceDirectory);
                    return null;
                }

                await _unitOfWork.BeginTransactionAsync();

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

        /// <summary>
        /// Recursively processes a directory and all its contents for snapshot creation.
        /// Handles both files and subdirectories while building proper relative paths
        /// for cross-platform restore compatibility.
        /// </summary>
        /// <param name="directoryPath">The full path to the directory to process</param>
        /// <param name="snapshotId">The ID of the snapshot being created</param>
        /// <param name="relativePath">The relative path from the snapshot root for this directory</param>
        /// <returns>Statistics containing the number of files processed and total bytes</returns>
        internal async Task<SnapshotStats> ProcessDirectoryAsync(string directoryPath, int snapshotId, string relativePath)
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

                    if (stats.FileCount % 100 == 0)
                        _logger.LogInformation("Processed {FileCount} files, {BytesProcessed:N0} bytes", stats.FileCount, stats.BytesProcessed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process file: {FilePath}", filePath);
                    throw;
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

        /// <summary>
        /// Processes a single file for inclusion in a snapshot, implementing content-based
        /// de-duplication by storing unique content only once based on SHA-256 hash.
        /// </summary>
        /// <param name="filePath">The full path to the file to process</param>
        /// <param name="snapshotId">The ID of the snapshot being created</param>
        /// <param name="relativePath">The relative path from the snapshot root to the file's directory</param>
        /// <returns>The size of the file in bytes</returns>
        /// <exception cref="IOException">Thrown when the file cannot be read</exception>
        internal async Task<long> ProcessFileAsync(string filePath, int snapshotId, string relativePath)
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

        /// <summary>
        /// Retrieves all snapshots from the database with their associated file metadata
        /// and content information loaded for immediate use.
        /// </summary>
        /// <returns>A list of all snapshots with complete file and content information</returns>
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

        /// <summary>
        /// Restores all files from a specified snapshot to the given output directory,
        /// recreating the complete directory structure and file contents exactly as
        /// they existed when the snapshot was created.
        /// </summary>
        /// <param name="snapshotId">The ID of the snapshot to restore</param>
        /// <param name="outputDirectory">The directory where files should be restored</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when the output directory cannot be created</exception>
        public async Task RestoreSnapshotAsync(int snapshotId, string outputDirectory)
        {
            _logger.LogInformation("Starting restore of snapshot {SnapshotId} to {OutputDirectory}", snapshotId, outputDirectory);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var snapshotFiles = await _unitOfWork.SnapshotFiles.GetBySnapshotIdAsync(snapshotId);

                if (snapshotFiles.Count == 0)
                {
                    _logger.LogError("No files found for Snapshot: {SnapshotId}, stopping", snapshotId);
                    return;
                }

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
                            await _fileSystemService.CreateDirectory(outputDir);

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

        /// <summary>
        /// Removes a snapshot from the database and automatically cleans up any file content
        /// that becomes orphaned (no longer referenced by any remaining snapshots).
        /// This operation is transactional and will rollback completely on any failure.
        /// </summary>
        /// <param name="snapshotId">The ID of the snapshot to remove</param>
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

        /// <summary>
        /// Validates the integrity of all stored file content by recalculating SHA-256 hashes
        /// and comparing them with the stored hash values. Identifies files with corrupted
        /// or missing content that may indicate data integrity issues.
        /// </summary>
        /// <returns>A list of SnapshotFile records that have corrupted or missing content</returns>
        public async Task<List<SnapshotFile>> CheckForCorruptedContentAsync()
        {
            var corruptedFiles = new List<SnapshotFile>();

            var snapshots = await _unitOfWork.Snapshots.GetAllAsync();

            foreach (var snapshot in snapshots)
            {
                if (snapshot.Files == null)
                    continue;
                foreach (var file in snapshot.Files)
                {
                    // Check for missing content or mismatched hash
                    if (file.Content == null || string.IsNullOrEmpty(file.ContentHash))
                    {
                        corruptedFiles.Add(file);
                        continue;
                    }

                    try
                    {
                        // Recalculate hash and compare
                        var data = file.Content.Data;
                        var actualHash = _hashService.CalculateHash(data);
                        if (!string.Equals(actualHash, file.ContentHash, StringComparison.OrdinalIgnoreCase))
                            corruptedFiles.Add(file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Calculate Hash failed on {data}", file.Content.Data);
                    }
                }
            }
            return corruptedFiles;
        }

        /// <summary>
        /// Creates the specified output directory if it doesn't exist. This is a helper method
        /// used by restore operations to ensure the target directory structure is available.
        /// </summary>
        /// <param name="fullName">The full path of the directory to create</param>
        public Task CreateOutputDirectoryAsync(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                _logger.LogError("Output directory name is null or empty");
                return Task.CompletedTask;
            }
            _logger.LogInformation("Creating output directory: {FullName}", fullName);
            return _fileSystemService.CreateDirectory(fullName);
        }
    }
}