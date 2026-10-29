namespace LazyTsql.SqlServer.Tests;

public class SqlServerConnectionFieldsTests
{
    [Fact]
    public void Builds_a_sql_login_connection_string_with_port_and_database()
    {
        var fields = new SqlServerConnectionFields(
            "db.local", "14339", "shop", SqlServerAuthentication.SqlLogin, "sa", "${DB_PASS}", TrustServerCertificate: true, Encrypt: true);

        var connectionString = fields.ToConnectionString();

        Assert.Equal(
            "Data Source=db.local,14339;Initial Catalog=shop;User ID=sa;Password=${DB_PASS};Encrypt=True;Trust Server Certificate=True",
            connectionString);
    }

    [Fact]
    public void Integrated_authentication_omits_credentials_and_empty_values()
    {
        var fields = new SqlServerConnectionFields(
            "db.local", "", "", SqlServerAuthentication.Integrated, "ignored", "ignored", TrustServerCertificate: false, Encrypt: true);

        Assert.Equal("Data Source=db.local;Integrated Security=True;Encrypt=True", fields.ToConnectionString());
    }

    [Fact]
    public void Parses_back_what_it_builds()
    {
        var original = new SqlServerConnectionFields(
            "db.local", "14339", "shop", SqlServerAuthentication.SqlLogin, "sa", "p;w=d", TrustServerCertificate: true, Encrypt: false);

        Assert.Equal(original, SqlServerConnectionFields.TryParse(original.ToConnectionString()));
    }

    [Fact]
    public void Parses_common_aliases()
    {
        var fields = SqlServerConnectionFields.TryParse("Server=tcp:db.local,1433;Database=app;User Id=sa;Password=x;TrustServerCertificate=true");

        Assert.Equal(new SqlServerConnectionFields("tcp:db.local", "1433", "app", SqlServerAuthentication.SqlLogin, "sa", "x", true, true), fields);
    }

    [Fact]
    public void Connection_strings_with_other_settings_cannot_be_edited_as_fields()
    {
        Assert.Null(SqlServerConnectionFields.TryParse("Server=db;User Id=sa;Password=x;MultipleActiveResultSets=true"));
        Assert.Null(SqlServerConnectionFields.TryParse("not a connection string"));
    }
}
