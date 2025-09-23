using BackupTool.Interfaces;
using System.Security.Cryptography;

namespace BackupTool.Services
{
    public class Sha256HashService : IHashService
    {
        /// <summary>
        /// Computes the sha256 hash of the bytes passed in
        /// </summary>
        /// <param name="data">Byte Array</param>
        /// <returns>A lowercase string of the hash</returns>
        public string CalculateHash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }
}
