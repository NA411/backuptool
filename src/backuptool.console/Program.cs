using BackupTool.Contexts;
using BackupTool.Extensions;
using BackupTool.Interfaces;
using BackupTool.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace BackupTool
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            //args = ["restore", "--snapshot-number", "1", "--output-directory", "D:\\Dev\\GridUnity"];
            // Setup DI Container
            var services = new ServiceCollection()
                .AddBackupServices("Data Source=backup.db")
                .BuildServiceProvider();

            // Ensure database is created
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
            await context.Database.EnsureCreatedAsync();

            // Get backup service
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

            // Setup Command Line Functions
            RootCommand rootCommand = new("Console based file backup tool.");
            rootCommand.SetupSnapshotCommand(backupService);
            rootCommand.SetupRestoreCommand(backupService);
            rootCommand.SetupListCommand(backupService);
            rootCommand.SetupPruneCommand(backupService);
            rootCommand.SetupCheckCommand(backupService);

            rootCommand.Parse(args).Invoke();

            return;
        }

        private static void SetupCheckCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command checkCommand = new("check", "Scans database for corrupted file content.");
            rootCommand.Subcommands.Add(checkCommand);
            checkCommand.SetAction(_ => CheckDatabaseForCorruption(backupService));
        }

        private static void CheckDatabaseForCorruption(IBackupService backupService) => Console.WriteLine("Checking Database");

        private static void SetupPruneCommand(this RootCommand rootCommand, IBackupService backupService)
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
            pruneCommand.SetAction(parseResult => PruneSnapshotFromDatabase(
                parseResult.GetValue(pruneSnapshotOption),
                backupService)
                );
        }

        private static async Task PruneSnapshotFromDatabase(int snapshotId, IBackupService backupService)
        {
            Console.WriteLine($"Pruning snapshot {snapshotId}...");
            await backupService.PruneSnapshotAsync(snapshotId);
            Console.WriteLine("Prune completed successfully.");
        }

        private static void SetupListCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command listCommand = new("list", "Lists all snapshots stored in the database.");
            rootCommand.Subcommands.Add(listCommand);
            listCommand.SetAction(_ => ListSnapshotsFromDatabase(backupService));
        }

        private static async Task ListSnapshotsFromDatabase(IBackupService backupService)
        {
            var snapshots = await backupService.GetSnapshotsAsync();

            Console.WriteLine("SNAPSHOT  TIMESTAMP");
            Console.WriteLine("--------  ---------");

            foreach (var snapshot in snapshots)
                Console.WriteLine($"{snapshot.Id,-8}  {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        private static void SetupRestoreCommand(this RootCommand rootCommand, IBackupService backupService)
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
            restoreCommand.SetAction(parseResult => RestoreSnapShotFromDatabase(
                parseResult.GetValue(restoreSnapshotOption),
                parseResult.GetValue(restoreDirectoryOption),
                backupService));
        }

        private static async Task RestoreSnapShotFromDatabase(int snapshotId, DirectoryInfo? directoryInfo, IBackupService backupService)
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

        private static void SetupSnapshotCommand(this RootCommand rootCommand, IBackupService backupService)
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
            snapshotCommand.SetAction(parseResult => AddDirectorySnapshotToDatabase(
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

        private static async Task AddDirectorySnapshotToDatabase(DirectoryInfo? directoryInfo, IBackupService backupService)
        {

            var targetDirectory = directoryInfo?.FullName;
            if (targetDirectory is not null)
            {
                Console.WriteLine($"Creating snapshot of {targetDirectory}...");

                var snapshotId = await backupService.CreateSnapshotAsync(targetDirectory);
                Console.WriteLine($"Snapshot {snapshotId} created successfully.");
            }
            else
            {
                Console.WriteLine("Directory is not valid.");
            }
        }
    }
}