using System.Text.RegularExpressions;

namespace LazyTsql.Core.Connections;

public sealed record ConnectionProfile(string Name, string ConnectionString);

public static partial class ConnectionStringResolver
{
    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex EnvironmentReference();

    /// <summary>Expands <c>${VAR}</c> references so secrets can stay out of the config file.</summary>
    public static string Resolve(string connectionString, Func<string, string?> getEnvironmentVariable) =>
        EnvironmentReference().Replace(connectionString, match =>
        {
            var name = match.Groups["name"].Value;
            return getEnvironmentVariable(name)
                ?? throw new InvalidOperationException($"Environment variable '{name}' referenced in the connection string is not set.");
        });
}
