using System.Data.Common;
using LazyTsql.Core.Connections;
using LazyTsql.Infrastructure;

namespace LazyTsql.Cli;

public static class ConnectionResolver
{
    public const string EnvironmentVariable = "LAZYTSQL_CONNECTION";

    /// <summary>
    /// Resolves <c>-c</c>: a saved profile name, otherwise a raw connection string,
    /// falling back to <c>LAZYTSQL_CONNECTION</c> when the option is absent.
    /// </summary>
    public static string Resolve(string? option, FileConnectionStore store, Func<string, string?> getEnvironmentVariable)
    {
        var value = option ?? getEnvironmentVariable(EnvironmentVariable)
            ?? throw new CliException($"No connection given. Use -c <name|connection string> or set {EnvironmentVariable}.");

        var profile = store.Load().FirstOrDefault(p => p.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
        var connectionString = profile?.ConnectionString
            ?? (value.Contains('=') ? value : throw new CliException($"Unknown connection '{value}'. Run 'lazytsql connections list'."));

        return ConnectionStringResolver.Resolve(connectionString, getEnvironmentVariable);
    }

    /// <summary>
    /// Name to show for <c>-c</c>: the saved profile name, or <c>server/database</c> for a raw connection string,
    /// which may carry a password and must never reach the screen.
    /// </summary>
    public static string DisplayName(string? option, FileConnectionStore store)
    {
        if (option is null) return EnvironmentVariable;
        if (store.Load().FirstOrDefault(p => p.Name.Equals(option, StringComparison.OrdinalIgnoreCase)) is { } profile) return profile.Name;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = option };
            var server = Value(builder, "Server", "Data Source", "Address", "Addr", "Network Address");
            var database = Value(builder, "Database", "Initial Catalog");
            return server is null ? "connection string" : database is null ? server : $"{server}/{database}";
        }
        catch (ArgumentException)
        {
            return "connection string";
        }
    }

    private static string? Value(DbConnectionStringBuilder builder, params string[] keys) =>
        keys.Select(key => builder.TryGetValue(key, out var value) ? value?.ToString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
