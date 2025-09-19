using BackupTool.Entities;

namespace BackupTool.Interfaces
{
    public interface ISnapshotRepository
    {
        Task<Snapshot> CreateAsync(Snapshot snapshot);
        Task<Snapshot?> GetByIdAsync(int id);
        Task<List<Snapshot>> GetAllAsync();
        Task DeleteAsync(int id);
        Task<bool> ExistsAsync(int id);
    }
}
