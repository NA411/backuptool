using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BackupTool.Repositories
{
    public class SnapshotFileRepository : ISnapshotFileRepository
    {
        private readonly BackupDbContext _context;

        public SnapshotFileRepository(BackupDbContext context)
        {
            _context = context;
        }

        public async Task<SnapshotFile> CreateAsync(SnapshotFile snapshotFile)
        {
            _context.SnapshotFiles.Add(snapshotFile);
            await _context.SaveChangesAsync();
            return snapshotFile;
        }

        public async Task<List<SnapshotFile>> GetBySnapshotIdAsync(int snapshotId)
        {
            return await _context.SnapshotFiles
                .Include(sf => sf.Content)
                .Where(sf => sf.SnapshotId == snapshotId)
                .ToListAsync();
        }

        public async Task DeleteBySnapshotIdAsync(int snapshotId)
        {
            var snapshotFiles = await _context.SnapshotFiles
                .Where(sf => sf.SnapshotId == snapshotId)
                .ToListAsync();

            _context.SnapshotFiles.RemoveRange(snapshotFiles);
            await _context.SaveChangesAsync();
        }
    }
}
