using BackupTool.Interfaces;
using System.Security.Cryptography;

namespace BackupTool.Services
{
    /// <summary>
    /// Implementation of <see cref="IHashService"/> that provides SHA-256 cryptographic hashing
    /// for content-based de-duplication in the backup system. Uses the .NET cryptographic
    /// libraries to generate consistent, collision-resistant hashes for file content identification.
    /// </summary>
    public class Sha256HashService : IHashService
    {
        /// <summary>
        /// Computes the SHA-256 hash of the provided byte array and returns it as a
        /// lowercase hexadecimal string for use in content de-duplication and integrity verification.
        /// </summary>
        /// <param name="data">The byte array containing the data to hash</param>
        /// <returns>A 64-character lowercase hexadecimal string representing the SHA-256 hash</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null</exception>
        public string CalculateHash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }
}
