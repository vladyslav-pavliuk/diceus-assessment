using Microsoft.Data.SqlClient;

namespace ClaimsModule.IntegrationTests.Fixtures;

internal static class SqlDiagnostics
{
    /// <summary>
    /// Waits until some request in the database is blocked by another session's lock: proof that two statements
    /// really overlapped, rather than a hope that a delay was long enough.
    /// </summary>
    public static async Task WaitForBlockedRequestAsync(string connectionString, TimeSpan timeout)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0 AND database_id = DB_ID(@database)";
            command.Parameters.AddWithValue("@database", database);
            if ((int)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"No blocked request appeared in {timeout}.");
    }
}
