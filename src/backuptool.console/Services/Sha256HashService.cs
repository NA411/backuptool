using BackupTool.Interfaces;

namespace BackupTool.Services
{
    public class Sha256HashService : IHashService
    {
        public string CalculateHash(byte[] data)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(data);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
