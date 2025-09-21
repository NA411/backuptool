using BackupTool.Entities;
using BackupTool.Interfaces;
using System.CommandLine;

namespace BackupTool.Extensions
{
    internal static class RootCommandExtensions
    {
        internal static void SetupCheckCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command checkCommand = new("check", "Scans database for corrupted file content.");
            rootCommand.Subcommands.Add(checkCommand);
            checkCommand.SetAction(_ => HandleCheckCommand(backupService));
        }

        private static async Task HandleCheckCommand(IBackupService backupService)
        {
            Console.WriteLine("Scanning database for corrupted file content...");

            var corruptedFiles = await backupService.CheckForCorruptedContentAsync();

            if (corruptedFiles.Count == 0)
            {
                Console.WriteLine("No corrupted file content found.");
            }
            else
            {
                Console.WriteLine("Corrupted file content detected:");
                Console.WriteLine("SnapshotId  FileName                RelativePath");
                Console.WriteLine("----------  ----------------------  --------------------------");
                foreach (var file in corruptedFiles)
                {
                    Console.WriteLine($"{file.SnapshotId,-10}  {file.FileName,-22}  {file.RelativePath}");
                }
            }
        }

        internal static void SetupPruneCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Option<int> pruneSnapshotOption = new("--snapshot")
            {
                Description = "Snapshot number.",
                Required = true,
            };

            Command pruneCommand = new("prune", "Remove Snapshot from database.")
            {
                pruneSnapshotOption
            };
            rootCommand.Subcommands.Add(pruneCommand);
            pruneCommand.SetAction(parseResult => HandlePruneCommand(
                parseResult.GetValue(pruneSnapshotOption),
                backupService)
                );
        }

        private static async Task HandlePruneCommand(int snapshotId, IBackupService backupService)
        {
            if (backupService is null)
                return;
            Console.WriteLine($"Pruning snapshot {snapshotId}...");
            await backupService.PruneSnapshotAsync(snapshotId);
            Console.WriteLine("Prune completed successfully.");
        }

        internal static void SetupListCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command listCommand = new("list", "Lists all snapshots stored in the database.");
            rootCommand.Subcommands.Add(listCommand);
            listCommand.SetAction(_ => HandleListCommand(backupService));
        }

        private static async Task HandleListCommand(IBackupService backupService)
        {
            var snapshots = await backupService.GetSnapshotsAsync();

            // Header
            Console.WriteLine("SNAPSHOT  TIMESTAMP            SIZE  DISTINCT_SIZE");
            Console.WriteLine("--------  -------------------  ----- -------------");

            long totalSize = 0;

            // Build a map of ContentHash to all SnapshotFile instances
            var contentHashToFiles = new Dictionary<string, List<SnapshotFile>>();
            foreach (var snapshot in snapshots)
            {
                if (snapshot.Files == null) continue;
                foreach (var file in snapshot.Files)
                {
                    if (!contentHashToFiles.TryGetValue(file.ContentHash, out var list))
                    {
                        list = new List<SnapshotFile>();
                        contentHashToFiles[file.ContentHash] = list;
                    }
                    list.Add(file);
                }
            }

            // Print info for each snapshot
            foreach (var snapshot in snapshots)
            {
                if (snapshot.Files == null) continue;

                // SIZE: total size of files in this snapshot
                long snapshotSize = snapshot.Files
                    .Select(f => f.Content?.Size ?? 0)
                    .Sum();

                // DISTINCT_SIZE: sum of sizes of files whose ContentHash only appears in this snapshot
                long distinctSize = snapshot.Files
                    .Where(f => contentHashToFiles[f.ContentHash].Count == 1)
                    .Select(f => f.Content?.Size ?? 0)
                    .Sum();

                totalSize += snapshotSize;

                Console.WriteLine($"{snapshot.Id,-8}  {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}  {snapshotSize,5} {distinctSize,13}");
            }

            // Summary line
            Console.WriteLine($" total                          {totalSize,5}");
        }

        internal static void SetupRestoreCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Option<int> restoreSnapshotOption = new("--snapshot-number")
            {
                Description = "Snapshot number.",
                Required = true,
            };
            Option<DirectoryInfo> restoreDirectoryOption = new("--output-directory")
            {
                Description = "Path to the directory to restore.",
                Required = true,
            };
            restoreDirectoryOption.Validators.Add(ValidateDirectoryExists(restoreDirectoryOption));
            Command restoreCommand = new("restore", "Restores directory state from a previous snapshot to a new directory.")
            {
                restoreSnapshotOption,
                restoreDirectoryOption
            };
            rootCommand.Subcommands.Add(restoreCommand);
            restoreCommand.SetAction(parseResult => HandleRestoreCommand(
                parseResult.GetValue(restoreSnapshotOption),
                parseResult.GetValue(restoreDirectoryOption),
                backupService));
        }

        private static async Task HandleRestoreCommand(int snapshotId, DirectoryInfo? directoryInfo, IBackupService backupService)
        {
            var outputDirectory = directoryInfo?.FullName;
            if (outputDirectory is not null)
            {
                Console.WriteLine($"Restoring snapshot {snapshotId} to {outputDirectory}...");

                await backupService.RestoreSnapshotAsync(snapshotId, outputDirectory);
                Console.WriteLine("Restore completed successfully.");
            }
            else
            {
                Console.WriteLine("Directory is not valid.");
            }
        }

        internal static void SetupSnapshotCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Option<DirectoryInfo> targetDirectoryOption = new("--target-directory")
            {
                Description = "Path to the directory to backup.",
                Required = true
            };
            targetDirectoryOption.Validators.Add(ValidateDirectoryExists(targetDirectoryOption));
            Command snapshotCommand = new("snapshot", "Takes a snapshot of all files in a specified directory.")
            {
                targetDirectoryOption
            };
            rootCommand.Subcommands.Add(snapshotCommand);
            snapshotCommand.SetAction(parseResult => HandleSnapshotCommand(
                parseResult.GetValue(targetDirectoryOption), backupService)
            );
        }

        private static Action<System.CommandLine.Parsing.OptionResult> ValidateDirectoryExists(Option<DirectoryInfo> targetDirectoryOption)
        {
            return result =>
            {
                if (result?.GetValue(targetDirectoryOption)?.Exists == false)
                    result.AddError("Directory does not exist");
            };
        }

        private static async Task HandleSnapshotCommand(DirectoryInfo? directoryInfo, IBackupService backupService)
        {

            var targetDirectory = directoryInfo?.FullName;
            if (targetDirectory is not null)
            {
                Console.WriteLine($"Creating snapshot of {targetDirectory}...");

                var snapshotId = await backupService.CreateSnapshotAsync(targetDirectory);
                if (snapshotId is null)
                {
                    Console.WriteLine("No files to snapshot.");
                    return;
                }
                Console.WriteLine($"Snapshot {snapshotId} created successfully.");
            }
            else
            {
                Console.WriteLine("Directory is not valid.");
            }
        }
    }
}
