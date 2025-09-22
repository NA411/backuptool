namespace BackupTool.Interfaces
{
    public interface IFileSystemService
    {
        Task<byte[]> ReadFileAsync(string filePath);
        Task WriteFileAsync(string filePath, byte[] data);
        Task CreateDirectory(string path);
        IEnumerable<string> GetFiles(string path, string searchPattern = "*");
        IEnumerable<string> GetDirectories(string path);
        bool FileExists(string path);
        bool DirectoryExists(string path);
    }
}
