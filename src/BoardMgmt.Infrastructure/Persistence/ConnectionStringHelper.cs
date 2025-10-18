using System;
using System.IO;

namespace BoardMgmt.Infrastructure.Persistence;

internal static class ConnectionStringHelper
{
    private const string SqliteFileName = "boardmgmt.db";

    public static (string ConnectionString, bool UseSqlite) Resolve(string? configuredConnectionString, string baseDirectory)
    {
        if (ShouldUseSqlite(configuredConnectionString))
        {
            var dataDirectory = Path.Combine(baseDirectory, "App_Data");
            Directory.CreateDirectory(dataDirectory);
            var sqlitePath = Path.Combine(dataDirectory, SqliteFileName);
            return ($"Data Source={sqlitePath}", true);
        }

        return (configuredConnectionString!, false);
    }

    private static bool ShouldUseSqlite(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return true;
        }

        return connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
    }
}
