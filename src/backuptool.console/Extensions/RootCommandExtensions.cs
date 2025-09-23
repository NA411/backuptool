using BackupTool.Entities;
using BackupTool.Interfaces;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace BackupTool.Extensions
{
    /// <summary>
    /// Extension methods for configuring the System.CommandLine RootCommand with all backup tool
    /// commands, options, and their associated handlers. Provides a fluent API for building
    /// the complete command-line interface with validation and error handling.
    /// </summary>
    internal static class RootCommandExtensions
    {
        /// <summary>
        /// Adds the global --verbose/-v option to the root command, making it available
        /// to all sub-commands for enabling detailed console logging output.
        /// </summary>
        /// <param name="rootCommand">The root command to configure</param>
        internal static void SetupVerboseOption(this RootCommand rootCommand)
        {
            Option<bool> verboseOption = new("--verbose", "-v")
            {
                Description = "Outputs logs to console",
                Required = false,
                Recursive = true
            };
            rootCommand.Options.Add(verboseOption);
        }

        /// <summary>
        /// Configures the 'check' command for scanning the database to identify corrupted
        /// or missing file content through hash verification.
        /// </summary>
        /// <param name="rootCommand">The root command to add the check sub-command to</param>
        /// <param name="backupService">The backup service instance for performing integrity checks</param>
        internal static void SetupCheckCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command checkCommand = new("check", "Scans database for corrupted file content.");
            rootCommand.Subcommands.Add(checkCommand);
            checkCommand.SetAction(_ => HandleCheckCommand(backupService));
        }

        /// <summary>
        /// Handles execution of the check command by performing integrity validation
        /// on all stored file content and reporting results to the console.
        /// </summary>
        /// <param name="backupService">The backup service to use for integrity checking</param>
        /// <returns>A task representing the asynchronous check operation</returns>
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

        /// <summary>
        /// Configures the 'prune' command for removing snapshots and automatically cleaning up
        /// orphaned content to reclaim storage space.
        /// </summary>
        /// <param name="rootCommand">The root command to add the prune sub-command to</param>
        /// <param name="backupService">The backup service instance for performing prune operations</param>
        /// <remarks>
        internal static void SetupPruneCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Option<int> pruneSnapshotOption = new("--snapshot")
            {
                Description = "Snapshot number.",
                Required = true,
            };

            pruneSnapshotOption.Validators.Add(ValidateSnapshotExists(backupService, pruneSnapshotOption));

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
        /// <summary>
        /// Creates a validator function that verifies a snapshot exists before allowing
        /// the command to proceed. Used by commands that reference specific snapshots.
        /// </summary>
        /// <param name="backupService">The backup service to use for snapshot validation</param>
        /// <param name="snapshotOption">The option containing the snapshot ID to validate</param>
        /// <returns>A validator function that can be added to command options</returns>
        private static Action<OptionResult> ValidateSnapshotExists(IBackupService backupService, Option<int> snapshotOption)
        {
            return result =>
            {
                int snapshot = result.GetValue(snapshotOption);
                var snapshots = backupService.GetSnapshotsAsync().Result;
                if (snapshots.Count(s => s.Id == snapshot) != 1)
                    result.AddError($"Snapshot: {snapshot} does not exist");
            };
        }

        /// <summary>
        /// Handles execution of the prune command by removing the specified snapshot
        /// and cleaning up any orphaned content.
        /// </summary>
        /// <param name="snapshotId">The ID of the snapshot to prune</param>
        /// <param name="backupService">The backup service to use for the prune operation</param>
        /// <returns>A task representing the asynchronous prune operation</returns>
        private static async Task HandlePruneCommand(int snapshotId, IBackupService backupService)
        {
            if (backupService is null)
                return;

            Console.WriteLine($"Pruning snapshot {snapshotId}...");
            await backupService.PruneSnapshotAsync(snapshotId);
            Console.WriteLine("Prune completed successfully.");
        }

        /// <summary>
        /// Configures the 'list' command for displaying all snapshots with their metadata
        /// including creation timestamps, file counts, and storage usage statistics.
        /// </summary>
        /// <param name="rootCommand">The root command to add the list subcommand to</param>
        /// <param name="backupService">The backup service instance for retrieving snapshot information</param>
        /// <remarks>
        internal static void SetupListCommand(this RootCommand rootCommand, IBackupService backupService)
        {
            Command listCommand = new("list", "Lists all snapshots stored in the database.");
            rootCommand.Subcommands.Add(listCommand);
            listCommand.SetAction(_ => HandleListCommand(backupService));
        }

        /// <summary>
        /// Handles execution of the list command by retrieving all snapshots and displaying
        /// them in a formatted table with storage statistics and deduplication information.
        /// </summary>
        /// <param name="backupService">The backup service to use for retrieving snapshots</param>
        /// <returns>A task representing the asynchronous list operation</returns>
        private static async Task HandleListCommand(IBackupService backupService)
        {
            var snapshots = await backupService.GetSnapshotsAsync();
            long totalSize = 0;

            // Get all existing snapshot IDs for pruning detection
            var existingSnapshotIds = snapshots.Select(s => s.Id).ToHashSet();

            var contentHashToFiles = new Dictionary<string, List<SnapshotFile>>(); // Build a map of ContentHash to all SnapshotFile instances
            foreach (var snapshot in snapshots)
            {
                if (snapshot.Files == null)
                    continue;

                foreach (var file in snapshot.Files)
                {
                    if (!contentHashToFiles.TryGetValue(file.ContentHash, out var list))
                    {
                        list = [];
                        contentHashToFiles[file.ContentHash] = list;
                    }
                    list.Add(file);
                }
            }

            // For each content hash, find the earliest existing snapshot that uses it and only consider existing snapshots
            var contentHashToSnapshot = contentHashToFiles.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Where(f => existingSnapshotIds.Contains(f.SnapshotId)).OrderBy(f => f.SnapshotId).Select(f => f.SnapshotId).FirstOrDefault()
            );

            Console.WriteLine("SNAPSHOT  TIMESTAMP            SIZE  DISTINCT_SIZE  DIRECTORY");
            Console.WriteLine("--------  -------------------  ----- -------------  ---------");

            foreach (var snapshot in snapshots)
            {
                if (snapshot.Files == null)
                    continue;

                long snapshotSize = snapshot.Files.Sum(f => f.Content?.Size ?? 0); // SIZE: total size of files in this snapshot

                // DISTINCT_SIZE: size of content that this snapshot "owns" (first non-pruned snapshot to use each piece of content)
                var distinctSize = snapshot.Files
                    .Where(f => contentHashToSnapshot.TryGetValue(f.ContentHash, out var ownerSnapshotId) && ownerSnapshotId == snapshot.Id)
                    .Sum(f => f.Content?.Size ?? 0);

                totalSize += snapshotSize;

                string displayDirectory = GetLastFolderName(snapshot.SourceDirectory); // Show just the last folder name for cleaner display

                Console.WriteLine($"{snapshot.Id,-8}  {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss}  {snapshotSize,5} {distinctSize,13}  {displayDirectory}");
            }

            Console.WriteLine($"total                          {totalSize,5}"); // Summary line
        }

        /// <summary>
        /// Extracts the last folder name from a full directory path for cleaner display
        /// in the list command output. Handles edge cases like root directories.
        /// </summary>
        /// <param name="path">The full directory path to process</param>
        /// <returns>The last folder name, or the original path if extraction fails</returns>
        private static string GetLastFolderName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); // Handle root paths and normalize separators

            if (string.IsNullOrEmpty(path))
                return "/"; // Root directory case

            var lastPart = Path.GetFileName(path); // Get the last part of the path

            return string.IsNullOrEmpty(lastPart) ? path : lastPart; // If GetFileName returns empty (e.g., for root drives like "C:\"), return the original path
        }

        /// <summary>
        /// Configures the 'restore' command for recreating files from a snapshot to a
        /// target directory with complete directory structure restoration.
        /// </summary>
        /// <param name="rootCommand">The root command to add the restore subcommand to</param>
        /// <param name="backupService">The backup service instance for performing restore operations</param>
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
            Option<bool> createDirectoryOption = new("--create-directory")
            {
                Description = "Create the output directory if it does not exist.",
                Required = false
            };

            Command restoreCommand = new("restore", "Restores directory state from a previous snapshot to a new directory.")
            {
                restoreSnapshotOption,
                restoreDirectoryOption,
                createDirectoryOption
            };

            restoreDirectoryOption.Validators.Add(ValidateDirectoryExists(restoreDirectoryOption, createDirectoryOption));
            restoreDirectoryOption.Validators.Add(ValidateSnapshotExists(backupService, restoreSnapshotOption));

            rootCommand.Subcommands.Add(restoreCommand);
            restoreCommand.SetAction(parseResult => HandleRestoreCommand(
                parseResult.GetValue(restoreSnapshotOption),
                parseResult.GetValue(restoreDirectoryOption),
                parseResult.GetValue(createDirectoryOption),
                backupService));
        }

        /// <summary>
        /// Handles execution of the restore command by recreating all files from the specified
        /// snapshot to the target directory with complete directory structure preservation.
        /// </summary>
        /// <param name="snapshotId">The ID of the snapshot to restore</param>
        /// <param name="directoryInfo">Information about the target directory for restoration</param>
        /// <param name="createDirectory">Whether to create the output directory if it doesn't exist</param>
        /// <param name="backupService">The backup service to use for the restore operation</param>
        /// <returns>A task representing the asynchronous restore operation</returns>
        private static async Task HandleRestoreCommand(int snapshotId, DirectoryInfo? directoryInfo, bool createDirectory, IBackupService backupService)
        {
            if (createDirectory)
                await backupService.CreateOutputDirectoryAsync(directoryInfo?.FullName);
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

        /// <summary>
        /// Configures the 'snapshot' command for creating new backups of directories
        /// with all subdirectories and files included.
        /// </summary>
        /// <param name="rootCommand">The root command to add the snapshot subcommand to</param>
        /// <param name="backupService">The backup service instance for performing snapshot operations</param>
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

        /// <summary>
        /// Creates a validator function that verifies a directory exists before allowing
        /// commands to proceed. Used by commands that operate on specific directories.
        /// </summary>
        /// <param name="targetDirectoryOption">The option containing the directory to validate</param>
        /// <param name="createDirectoryOption">Optional flag indicating if the directory should be created</param>
        /// <returns>A validator function that can be added to command options</returns>
        private static Action<OptionResult> ValidateDirectoryExists(Option<DirectoryInfo> targetDirectoryOption, Option<bool> createDirectoryOption = null!)
        {
            return result =>
            {
                var targetDir = result?.GetValue(targetDirectoryOption);
                var createDir = createDirectoryOption is not null && (result?.GetValue(createDirectoryOption) ?? false);
                if (targetDir?.Exists == false && !createDir)
                    result?.AddError($"Directory: {targetDir?.FullName} does not exist");
            };
        }

        /// <summary>
        /// Handles execution of the snapshot command by creating a complete backup of the
        /// specified directory and all its contents.
        /// </summary>
        /// <param name="directoryInfo">Information about the directory to backup</param>
        /// <param name="backupService">The backup service to use for the snapshot operation</param>
        /// <returns>A task representing the asynchronous snapshot operation</returns>
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
