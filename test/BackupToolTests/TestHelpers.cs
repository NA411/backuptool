using BackupTool.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
