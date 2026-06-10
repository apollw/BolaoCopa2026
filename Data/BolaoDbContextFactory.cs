using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace BolaoCopa2026.Data;

public sealed class BolaoDbContextFactory : IDesignTimeDbContextFactory<BolaoDbContext>
{
    public BolaoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BolaoDbContext>();
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Postgres";

        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(GetPostgresConnectionString(), postgres =>
            {
                postgres.CommandTimeout(120);
                postgres.EnableRetryOnFailure();
            });
            return new BolaoDbContext(options.Options);
        }

        options.UseSqlite("Data Source=Data/bolao.db");
        return new BolaoDbContext(options.Options);
    }

    private static string GetPostgresConnectionString()
    {
        var databaseUrl = Environment.GetEnvironmentVariable("SUPABASE_DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return ConvertDatabaseUrl(databaseUrl);
        }

        return "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres;Ssl Mode=Disable";
    }

    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
