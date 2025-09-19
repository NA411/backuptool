using BackupTool.Entities;
using BackupTool.Interfaces;

namespace BackupTool.Services
{
    public class BackupService(IUnitOfWork unitOfWork, IHashService hashService, IFileSystemService fileSystemService) : IBackupService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IHashService _hashService = hashService;
        private readonly IFileSystemService _fileSystemService = fileSystemService;

        public async Task<int> CreateSnapshotAsync(string sourceDirectory)
        {
            if (!_fileSystemService.DirectoryExists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var snapshot = new Snapshot
                {
                    SourceDirectory = sourceDirectory,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Snapshots.CreateAsync(snapshot);

                await ProcessDirectoryAsync(sourceDirectory, snapshot.Id, string.Empty);

                await _unitOfWork.CommitTransactionAsync();
                return snapshot.Id;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        private async Task ProcessDirectoryAsync(string directoryPath, int snapshotId, string relativePath)
        {
            foreach (var filePath in _fileSystemService.GetFiles(directoryPath))
            {
                await ProcessFileAsync(filePath, snapshotId, relativePath);
            }

            foreach (var subDir in _fileSystemService.GetDirectories(directoryPath))
            {
                var subDirName = Path.GetFileName(subDir);
                var newRelativePath = Path.Combine(relativePath, subDirName);
                await ProcessDirectoryAsync(subDir, snapshotId, newRelativePath);
            }
        }

        private async Task ProcessFileAsync(string filePath, int snapshotId, string relativePath)
        {
            var fileData = await _fileSystemService.ReadFileAsync(filePath);
            var hash = _hashService.CalculateHash(fileData);
            var fileName = Path.GetFileName(filePath);
            var fullRelativePath = Path.Combine(relativePath, fileName);

            // Check if content already exists
            if (!await _unitOfWork.FileContents.ExistsAsync(hash))
            {
                var newContent = new FileContent
                {
                    Hash = hash,
                    Data = fileData,
                    Size = fileData.Length
                };
                await _unitOfWork.FileContents.CreateAsync(newContent);
            }

            var snapshotFile = new SnapshotFile
            {
                SnapshotId = snapshotId,
                ContentHash = hash,
                RelativePath = fullRelativePath,
                FileName = fileName
            };

            await _unitOfWork.SnapshotFiles.CreateAsync(snapshotFile);
        }

        public async Task<List<Snapshot>> GetSnapshotsAsync() => await _unitOfWork.Snapshots.GetAllAsync();

        public async Task RestoreSnapshotAsync(int snapshotId, string outputDirectory)
        {
            if (!await _unitOfWork.Snapshots.ExistsAsync(snapshotId))
                throw new ArgumentException($"Snapshot {snapshotId} not found");

            var snapshotFiles = await _unitOfWork.SnapshotFiles.GetBySnapshotIdAsync(snapshotId);

            foreach (var snapshotFile in snapshotFiles)
            {
                var outputPath = Path.Combine(outputDirectory, snapshotFile.RelativePath);
                var outputDir = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrEmpty(outputDir))
                {
                    _fileSystemService.CreateDirectory(outputDir);
                }

                await _fileSystemService.WriteFileAsync(outputPath, snapshotFile.Content.Data);
            }
        }

        public async Task PruneSnapshotAsync(int snapshotId)
        {
            if (!await _unitOfWork.Snapshots.ExistsAsync(snapshotId))
                throw new ArgumentException($"Snapshot {snapshotId} not found");

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.Snapshots.DeleteAsync(snapshotId);

                // Clean up orphaned content
                var orphanedContent = await _unitOfWork.FileContents.GetOrphanedContentAsync();
                if (orphanedContent.Count > 0)
                    await _unitOfWork.FileContents.DeleteRangeAsync(orphanedContent);

                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
    }
}