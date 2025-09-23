using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BackupTool.Repositories
{
    public class SnapshotRepository(BackupDbContext context) : ISnapshotRepository
    {
        private readonly BackupDbContext _context = context;

        public async Task<Snapshot> CreateAsync(Snapshot snapshot)
        {
            _context.Snapshots.Add(snapshot);
            await _context.SaveChangesAsync();
            return snapshot;
        }

        public async Task<Snapshot?> GetByIdAsync(int id) => await _context.Snapshots.Include(s => s.Files).ThenInclude(sf => sf.Content).FirstOrDefaultAsync(s => s.Id == id);

        public async Task<List<Snapshot>> GetAllAsync() => await _context.Snapshots.Include(s => s.Files).ThenInclude(sf => sf.Content).OrderBy(s => s.Id).ToListAsync();

        public async Task DeleteAsync(int id)
        {
            var snapshot = await _context.Snapshots.FindAsync(id);
            if (snapshot != null)
            {
                _context.Snapshots.Remove(snapshot);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id) => await _context.Snapshots.AnyAsync(s => s.Id == id);
    }
}
