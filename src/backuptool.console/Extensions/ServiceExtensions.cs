using BackupTool.Services;
using BackupTool.Contexts;
using BackupTool.Interfaces;
using BackupTool.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackupTool.Extensions
{
    /// <summary>
    /// Extension methods for configuring dependency injection services for the backup tool application.
    /// Provides a centralized configuration point for all application services, repositories, and
    /// infrastructure components with support for different logging configurations.
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Configures and registers all backup tool services with the dependency injection container.
        /// Sets up the complete service dependency graph including database context, repositories,
        /// domain services, and logging infrastructure.
        /// </summary>
        public static IServiceCollection AddBackupServices(this IServiceCollection services, string connectionString, bool isVerbose)
        {
            // Database
            services.AddDbContext<BackupDbContext>(options => options.UseSqlite(connectionString));

            // Repositories and Unit of Work
            services.AddScoped<ISnapshotRepository, SnapshotRepository>();
            services.AddScoped<IFileContentRepository, FileContentRepository>();
            services.AddScoped<ISnapshotFileRepository, SnapshotFileRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Domain Services
            services.AddScoped<IHashService, Sha256HashService>();
            services.AddScoped<IFileSystemService, FileSystemService>();

            // Application Services
            services.AddScoped<IBackupService, BackupService>();

            // Logging
            services.AddLogging(configure =>
            {
                if (isVerbose)
                    configure.AddConsole();
                configure.AddDebug();
            });

            return services;
        }
    }
}