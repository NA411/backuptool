using BackupTool.Contexts;
using BackupTool.Extensions;
using BackupTool.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;


namespace BackupTool
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            // Setup Command Line Functions
            const string asciiArt = @"
                ██████╗  █████╗  ██████╗██╗  ██╗██╗   ██╗██████╗     ████████╗ ██████╗  ██████╗ ██╗     
                ██╔══██╗██╔══██╗██╔════╝██║ ██╔╝██║   ██║██╔══██╗    ╚══██╔══╝██╔═══██╗██╔═══██╗██║     
                ██████╔╝███████║██║     █████╔╝ ██║   ██║██████╔╝       ██║   ██║   ██║██║   ██║██║     
                ██╔══██╗██╔══██║██║     ██╔═██╗ ██║   ██║██╔═══╝        ██║   ██║   ██║██║   ██║██║     
                ██████╔╝██║  ██║╚██████╗██║  ██╗╚██████╔╝██║            ██║   ╚██████╔╝╚██████╔╝███████╗
                ╚═════╝ ╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝ ╚═════╝ ╚═╝            ╚═╝    ╚═════╝  ╚═════╝ ╚══════╝
                                                                                         
                Console based file backup tool with content de-duplication and snapshot management.
                ";

            RootCommand rootCommand = new(asciiArt);
            bool isVerbose = false;

            if (args.Contains("--verbose") || args.Contains("-v")) // hacky way to get verbose option before DI setup
                isVerbose = true;

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

            // Setup commands with service
            rootCommand.SetupVerboseOption();
            rootCommand.SetupSnapshotCommand(backupService);
            rootCommand.SetupRestoreCommand(backupService);
            rootCommand.SetupListCommand(backupService);
            rootCommand.SetupPruneCommand(backupService);
            rootCommand.SetupCheckCommand(backupService);

            try
            {
                rootCommand.Parse(args).Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }

            return;
        }
    }
}