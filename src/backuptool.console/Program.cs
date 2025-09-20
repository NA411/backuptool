using BackupTool.Contexts;
using BackupTool.Extensions;
using BackupTool.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;


namespace BackupTool
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            // Setup Command Line Functions
            RootCommand rootCommand = new("Console based file backup tool.");

            Option<bool> verboseOption = new("--verbose", "-v")
            {
                Description = "Enables logging output to console.",
                Required = false,
            };
            rootCommand.Add(verboseOption);

            rootCommand.SetupSnapshotCommand(null!);
            rootCommand.SetupRestoreCommand(null!);
            rootCommand.SetupListCommand(null!);
            rootCommand.SetupPruneCommand(null!);
            rootCommand.SetupCheckCommand(null!);

            var parseResult = rootCommand.Parse(args);
            bool isVerbose = parseResult.GetValue(verboseOption);

            // Setup DI Container
            var services = new ServiceCollection()
                .AddBackupServices("Data Source=backup.db", isVerbose)
                .BuildServiceProvider();

            // Ensure database is created
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BackupDbContext>();
            await context.Database.EnsureCreatedAsync();

            // Get backup service
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

            // Re-setup commands with actual service
            rootCommand.SetupSnapshotCommand(backupService);
            rootCommand.SetupRestoreCommand(backupService);
            rootCommand.SetupListCommand(backupService);
            rootCommand.SetupPruneCommand(backupService);
            rootCommand.SetupCheckCommand(backupService);

            parseResult.Invoke();

            return;
        }
    }
}