using BackupTool.Contexts;
using BackupTool.Interfaces;

namespace BackupTool.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BackupDbContext _context;
        private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

        public UnitOfWork(BackupDbContext context)
        {
            _context = context;
            Snapshots = new SnapshotRepository(_context);
            FileContents = new FileContentRepository(_context);
            SnapshotFiles = new SnapshotFileRepository(_context);
        }

        public ISnapshotRepository Snapshots { get; }
        public IFileContentRepository FileContents { get; }
        public ISnapshotFileRepository SnapshotFiles { get; }

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public async Task BeginTransactionAsync() => _transaction = await _context.Database.BeginTransactionAsync();

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
