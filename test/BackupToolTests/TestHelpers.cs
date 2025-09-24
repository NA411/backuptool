using BackupTool.Entities;

namespace BackupToolTests
{
    internal static class TestHelpers
    {
        // Helper methods for creating test data
        internal static Snapshot CreateTestSnapshot(int id, DateTime createdAt, string sourceDirectory)
        {
            return new Snapshot
            {
                Id = id,
                CreatedAt = createdAt,
                SourceDirectory = sourceDirectory,
                Files = []
            };
        }

        internal static SnapshotFile CreateTestSnapshotFile(int id, int snapshotId, string hash, string fileName, string relativePath)
        {
            var fileContent = new FileContent
            {
                Hash = hash,
                Data = [1, 2, 3, 4, 5],
                Size = 5,
                CreatedAt = DateTime.UtcNow
            };

            return new SnapshotFile
            {
                Id = id,
                SnapshotId = snapshotId,
                ContentHash = hash,
                Content = fileContent,
                RelativePath = relativePath,
                FileName = fileName
            };
        }
        internal static byte[] GenerateRandomBytes(int size)
        {
            var random = new Random(42); // Fixed seed for reproducible tests
            var bytes = new byte[size];
            random.NextBytes(bytes);
            return bytes;
        }

        internal static byte[] GeneratePatternBytes(int size)
        {
            var pattern = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var result = new byte[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = pattern[i % pattern.Length];
            }
            return result;
        }

        internal static byte[] GenerateMixedContent(int size)
        {
            var result = new byte[size];
            for (int i = 0; i < size; i++)
            {
                if (i % 4 == 0) result[i] = (byte)(i % 256);
                else if (i % 4 == 1) result[i] = 0x00;
                else if (i % 4 == 2) result[i] = 0xFF;
                else result[i] = (byte)(255 - (i % 256));
            }
            return result;
        }

        internal static byte[] GenerateExecutableLikeContent(int size)
        {
            var result = new byte[size];
            // Simulate PE header
            result[0] = 0x4D; result[1] = 0x5A; // MZ header

            var random = new Random(123);
            for (int i = 2; i < size; i++)
            {
                result[i] = (byte)random.Next(256);
            }
            return result;
        }

        internal static byte[] GenerateImageLikeContent(int size)
        {
            var result = new byte[size];
            // Simulate PNG header
            result[0] = 0x89; result[1] = 0x50; result[2] = 0x4E; result[3] = 0x47;
            result[4] = 0x0D; result[5] = 0x0A; result[6] = 0x1A; result[7] = 0x0A;

            var random = new Random(456);
            for (int i = 8; i < size; i++)
            {
                result[i] = (byte)random.Next(256);
            }
            return result;
        }

        internal static byte[] GenerateCompressedLikeContent(int size)
        {
            var result = new byte[size];
            // Simulate ZIP header
            result[0] = 0x50; result[1] = 0x4B; result[2] = 0x03; result[3] = 0x04;

            var random = new Random(789);
            for (int i = 4; i < size; i++)
            {
                result[i] = (byte)random.Next(256);
            }
            return result;
        }

        internal static byte[] GenerateHighEntropyContent(int size)
        {
            var result = new byte[size];
            var random = new Random(999);

            // Generate high entropy content (close to random)
            random.NextBytes(result);
            return result;
        }

        internal static byte[] GenerateControlCharacterContent(int size)
        {
            var result = new byte[size];
            for (int i = 0; i < size; i++)
            {
                // Include control characters (0-31) and extended ASCII
                result[i] = (byte)(i % 256);
            }
            return result;
        }
    }
}
