using BackupTool.Contexts;
using BackupTool.Entities;
using BackupTool.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BackupTool.Repositories
{
    public class FileContentRepository(BackupDbContext context) : IFileContentRepository
    {
        private readonly BackupDbContext _context = context;

        public async Task<FileContent> CreateAsync(FileContent content)
        {
            _context.FileContents.Add(content);
            await _context.SaveChangesAsync();
            return content;
        }

        public async Task<FileContent?> GetByHashAsync(string hash) => await _context.FileContents.FindAsync(hash);

        public async Task<bool> ExistsAsync(string hash) => await _context.FileContents.AnyAsync(fc => fc.Hash == hash);

        public async Task<List<FileContent>> GetOrphanedContentAsync() => await _context.FileContents.Where(fc => !fc.SnapshotFiles.Any()).ToListAsync();

        public async Task DeleteRangeAsync(IEnumerable<FileContent> contents)
        {
            _context.FileContents.RemoveRange(contents);
            await _context.SaveChangesAsync();
        }
    }
}
