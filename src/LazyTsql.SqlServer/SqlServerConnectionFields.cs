using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace LazyTsql.SqlServer;

public enum SqlServerAuthentication
{
    SqlLogin,
    Integrated,
}

/// <summary>The connection settings a login form edits, convertible to and from a connection string.</summary>
public sealed record SqlServerConnectionFields(
    string Server,
    string Port,
    string Database,
    SqlServerAuthentication Authentication,
    string User,
    string Password,
    bool TrustServerCertificate,
    bool Encrypt)
{
    private static readonly HashSet<string> EditableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "data source", "server", "address", "addr", "network address",
        "initial catalog", "database",
        "user id", "uid", "user",
        "password", "pwd",
        "integrated security", "trusted_connection",
        "encrypt",
        "trust server certificate", "trustservercertificate",
    };

    public string ToConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(Port) ? Server.Trim() : $"{Server.Trim()},{Port.Trim()}",
        };
        if (!string.IsNullOrWhiteSpace(Database)) builder.InitialCatalog = Database.Trim();
        if (Authentication == SqlServerAuthentication.Integrated)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = User.Trim();
            builder.Password = Password;
        }

        builder.Encrypt = Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional;
        if (TrustServerCertificate) builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    /// <summary>Returns null when the string has settings the form cannot represent, so editing it as fields would lose them.</summary>
    public static SqlServerConnectionFields? TryParse(string connectionString)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (raw.Keys.Cast<string>().Any(key => !EditableKeys.Contains(key))) return null;
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var separator = builder.DataSource.LastIndexOf(',');
        var (server, port) = separator < 0
            ? (builder.DataSource, string.Empty)
            : (builder.DataSource[..separator], builder.DataSource[(separator + 1)..]);

        return new SqlServerConnectionFields(
            server,
            port,
            builder.InitialCatalog,
            builder.IntegratedSecurity ? SqlServerAuthentication.Integrated : SqlServerAuthentication.SqlLogin,
            builder.UserID,
            builder.Password,
            builder.TrustServerCertificate,
            builder.Encrypt != SqlConnectionEncryptOption.Optional);
    }
}
