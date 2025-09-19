namespace BackupTool.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        ISnapshotRepository Snapshots { get; }
        IFileContentRepository FileContents { get; }
        ISnapshotFileRepository SnapshotFiles { get; }
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
