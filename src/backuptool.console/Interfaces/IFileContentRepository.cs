using BackupTool.Entities;

namespace BackupTool.Interfaces
{
    public interface IFileContentRepository
    {
        Task<FileContent> CreateAsync(FileContent content);
        Task<FileContent?> GetByHashAsync(string hash);
        Task<bool> ExistsAsync(string hash);
        Task<List<FileContent>> GetOrphanedContentAsync();
        Task DeleteRangeAsync(IEnumerable<FileContent> contents);
    }
}
