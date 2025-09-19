using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupTool.Entities
{
    public class Snapshot
    {
        [Key]
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string SourceDirectory { get; set; } = string.Empty;
        public virtual ICollection<SnapshotFile> Files { get; set; } = new List<SnapshotFile>();
    }
}
