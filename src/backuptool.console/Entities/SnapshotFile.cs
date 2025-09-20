using System.ComponentModel.DataAnnotations;

namespace BackupTool.Entities
{
    public class SnapshotFile
    {
        [Key]
        public int Id { get; set; }
        public int SnapshotId { get; set; }
        public virtual Snapshot Snapshot { get; set; } = null!;
        public string ContentHash { get; set; } = string.Empty;
        public virtual FileContent Content { get; set; } = null!;
        public string RelativePath { get; set; } = string.Empty; // Path relative to snapshot root
        public string FileName { get; set; } = string.Empty;
    }
}
