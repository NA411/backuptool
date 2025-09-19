using BackupTool.Entities;

namespace BackupTool.Interfaces
{
    public interface ISnapshotFileRepository
    {
        Task<SnapshotFile> CreateAsync(SnapshotFile snapshotFile);
        Task<List<SnapshotFile>> GetBySnapshotIdAsync(int snapshotId);
        Task DeleteBySnapshotIdAsync(int snapshotId);
    }
}
