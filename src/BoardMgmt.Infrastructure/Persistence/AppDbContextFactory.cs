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
        if (string.IsNullOrWhiteSpace(cs))
        {
            cs = "Server=(localdb)\\MSSQLLocalDB;Database=BoardMgmtDb;Trusted_Connection=True;MultipleActiveResultSets=True";
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

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
