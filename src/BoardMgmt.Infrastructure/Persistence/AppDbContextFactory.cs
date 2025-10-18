using System.IO;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BoardMgmt.Infrastructure.Persistence;

// Used ONLY by "dotnet ef" at design time
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Assumes running EF with --project BoardMgmt.Infrastructure --startup-project BoardMgmt.WebApi
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "BoardMgmt.WebApi"));

        LoadEnvIfPresent(basePath);

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection");
        var useSqlite = false;

        if (string.IsNullOrWhiteSpace(cs))
        {
            var dataDirectory = Path.Combine(basePath, "App_Data");
            Directory.CreateDirectory(dataDirectory);
            cs = $"Data Source={Path.Combine(dataDirectory, "boardmgmt.db")}";
            useSqlite = true;
        }

        var builder = new DbContextOptionsBuilder<AppDbContext>();

        if (useSqlite)
        {
            builder.UseSqlite(cs);
        }
        else
        {
            builder.UseSqlServer(cs);
        }

        var options = builder.Options;

        return new AppDbContext(options);
    }

    private static void LoadEnvIfPresent(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current is not null)
        {
            var envPath = Path.Combine(current.FullName, ".env");

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                break;
            }

            current = current.Parent;
        }
    }
}
