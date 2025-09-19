using BackupTool.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupTool.Contexts
{
    public class BackupDbContext(DbContextOptions<BackupDbContext> options) : DbContext(options)
    {
        public DbSet<Snapshot> Snapshots { get; set; }
        public DbSet<FileContent> FileContents { get; set; }
        public DbSet<SnapshotFile> SnapshotFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileContent>(entity =>
            {
                entity.HasKey(e => e.Hash);
                entity.Property(e => e.Hash).HasMaxLength(64);
                entity.HasIndex(e => e.Hash).IsUnique();
            });

            modelBuilder.Entity<Snapshot>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<SnapshotFile>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(sf => sf.Snapshot)
                      .WithMany(s => s.Files)
                      .HasForeignKey(sf => sf.SnapshotId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sf => sf.Content)
                      .WithMany(fc => fc.SnapshotFiles)
                      .HasForeignKey(sf => sf.ContentHash)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(sf => new { sf.SnapshotId, sf.RelativePath }).IsUnique();
            });
        }
    }
}
