using BackupTool.Contexts;
using BackupTool.Extensions;
using BackupTool.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BackupToolTests")]
namespace BackupTool
{
    /// <summary>
    /// Main program entry point for the BackupTool console application.
    /// Configures the command-line interface, dependency injection container, database initialization,
    /// and orchestrates the overall application execution flow.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Application entry point that configures and executes the backup tool command-line interface.
        /// Handles the complete application life-cycle from argument parsing through command execution
        /// and error handling with appropriate exit codes.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application</param>
        /// <returns>Exit code: 0 for success, 1 for error conditions</returns>
        static async Task<int> Main(string[] args)
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

            // hacky way to get verbose option before DI setup
            if (args.Contains("--verbose") || args.Contains("-v"))
                isVerbose = true;

            // Setup DI Container
            var services = new ServiceCollection().AddBackupServices("Data Source=backup.db", isVerbose).BuildServiceProvider();

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
                return rootCommand.Parse(args).Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1; // Return error exit code
            }
        }
    }
}