using System.IO;
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

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection")
                 ?? "Server=SAMI-PC\\SQLEXPRESS;Database=BoardMgmtDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AppDbContext(options);
    }
}
