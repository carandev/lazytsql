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
}
