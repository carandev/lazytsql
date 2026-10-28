using LazyTsql.Core.Connections;

namespace LazyTsql.Core.Tests;

public class ConnectionStringResolverTests
{
    [Fact]
    public void Expands_environment_references()
    {
        var resolved = ConnectionStringResolver.Resolve(
            "Server=db;User Id=sa;Password=${DB_PASS};",
            name => name == "DB_PASS" ? "s3cret" : null);

        Assert.Equal("Server=db;User Id=sa;Password=s3cret;", resolved);
    }

    [Fact]
    public void Missing_variable_fails_with_its_name()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => ConnectionStringResolver.Resolve("Password=${MISSING}", _ => null));

        Assert.Contains("MISSING", error.Message);
    }

    [Fact]
    public void Connection_string_without_references_is_unchanged()
    {
        Assert.Equal("Server=db;", ConnectionStringResolver.Resolve("Server=db;", _ => null));
    }
}
