using BackupTool.Services;
using BackupTool.Contexts;
using BackupTool.Interfaces;
using BackupTool.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BackupTool.Extensions
{
    public static class ServiceExtensions
    {
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