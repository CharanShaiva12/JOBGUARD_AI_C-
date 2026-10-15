using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace JobGuard.Data;

public class DatabaseInitializer
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(SqlConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            var sqlPath = Path.Combine(AppContext.BaseDirectory, "Data", "Sql", "CreateTables.sql");
            if (!File.Exists(sqlPath))
            {
                // Fallback check in current directory
                sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Sql", "CreateTables.sql");
            }

            if (!File.Exists(sqlPath))
            {
                _logger.LogWarning("CreateTables.sql script not found at {Path}. Skipping automated table execution.", sqlPath);
                return false;
            }

            var script = await File.ReadAllTextAsync(sqlPath);
            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

            using var conn = _connectionFactory.CreateConnection();
            await conn.OpenAsync();

            foreach (var batch in batches)
            {
                var trimmed = batch.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                using var cmd = new SqlCommand(trimmed, conn);
                await cmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("JobGuard SQL Server database schema verified and initialized successfully.");
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to connect to or initialize SQL Server database. Check connection string.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during database initialization.");
            return false;
        }
    }
}
