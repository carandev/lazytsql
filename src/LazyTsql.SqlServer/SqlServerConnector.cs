using LazyTsql.Core;
using Microsoft.Data.SqlClient;

namespace LazyTsql.SqlServer;

public sealed class SqlServerConnector : IDatabaseConnector
{
    public async Task<IDatabaseSession> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { ApplicationName = "lazytsql" };
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new SqlServerSession(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
