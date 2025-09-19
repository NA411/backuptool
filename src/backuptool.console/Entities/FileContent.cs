using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupTool.Entities
{
    public class FileContent
    {
        [Key]
        public string Hash { get; set; } = string.Empty;

        public byte[] Data { get; set; } = [];

        public long Size { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property - files that reference this content
        public virtual ICollection<SnapshotFile> SnapshotFiles { get; set; } = new List<SnapshotFile>();
    }
}
