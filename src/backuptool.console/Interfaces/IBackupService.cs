using BackupTool.Entities;

namespace BackupTool.Interfaces
{
    public interface IBackupService
    {
        Task<int?> CreateSnapshotAsync(string sourceDirectory);
        Task<List<Snapshot>> GetSnapshotsAsync();
        Task RestoreSnapshotAsync(int snapshotId, string outputDirectory);
        Task PruneSnapshotAsync(int snapshotId);
        Task<List<SnapshotFile>> CheckForCorruptedContentAsync();
    }
}
