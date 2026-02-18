using Microsoft.Data.SqlClient;

namespace EFCore.NoLock.LinqToDb.Tests;

/// <summary>
/// Shared test fixture that seeds the Northwind database once for all LinqToDB integration tests.
/// </summary>
public class NorthwindFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=localhost,11433;Database=Northwind;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    private const string MasterConnectionString =
        "Server=localhost,11433;Database=master;User Id=sa;Password=NoLock_Test123!;TrustServerCertificate=True;";

    public async Task InitializeAsync()
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "seed", "northwind-seed.sql");
        var seedSql = await File.ReadAllTextAsync(seedPath);

        var batches = seedSql.Split(
            ["\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\r\nGO\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await using var masterConn = new SqlConnection(MasterConnectionString);
        await masterConn.OpenAsync();

        await using (var cmd = masterConn.CreateCommand())
        {
            cmd.CommandText = "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Northwind') CREATE DATABASE Northwind;";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.Contains("CREATE DATABASE Northwind", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                continue;

            if (trimmed.Equals("USE Northwind;", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("USE Northwind", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = trimmed;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (ex.Message.Contains("Cannot insert explicit value for identity column")
                                          || ex.Message.Contains("Violation of PRIMARY KEY"))
            {
                // Data already seeded
            }
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("Northwind")]
public class NorthwindCollection : ICollectionFixture<NorthwindFixture>;
