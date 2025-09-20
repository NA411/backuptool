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
            checkCommand.SetAction(_ => CheckDatabaseForCorruption(backupService));
        }

        private static void CheckDatabaseForCorruption(IBackupService backupService) => Console.WriteLine($"Checking Database {backupService}");

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

            Console.WriteLine("SNAPSHOT  TIMESTAMP");
            Console.WriteLine("--------  ---------");

            foreach (var snapshot in snapshots)
                Console.WriteLine($"{snapshot.Id,-8}  {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}");
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
