using BackupTool.Interfaces;

namespace BackupTool.Services
{
    public class FileSystemService : IFileSystemService
    {
        public async Task<byte[]> ReadFileAsync(string filePath) => await File.ReadAllBytesAsync(filePath);
        public async Task WriteFileAsync(string filePath, byte[] data) => await File.WriteAllBytesAsync(filePath, data);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => Directory.GetFiles(path, searchPattern);
        public IEnumerable<string> GetDirectories(string path) => Directory.GetDirectories(path);
        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
    }
}