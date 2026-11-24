using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public class ConnectionConsoleTests
{
    [Fact]
    public void Each_profile_gets_its_own_console_file_next_to_the_connections_directory()
    {
        Assert.Equal(
            Path.Combine("config", "consoles", "prod.sql"),
            ConnectionConsole.PathFor("config", "prod"));
    }

    [Fact]
    public void Two_different_profile_names_never_share_a_console_file()
    {
        var a = ConnectionConsole.PathFor("config", "prod");
        var b = ConnectionConsole.PathFor("config", "staging");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Characters_unsafe_in_a_file_name_are_replaced()
    {
        Assert.Equal(
            Path.Combine("config", "consoles", "prod-db-shop.sql"),
            ConnectionConsole.PathFor("config", "prod/db:shop"));
    }

    [Fact]
    public void A_name_with_no_safe_characters_still_gets_a_console_file()
    {
        Assert.Equal(
            Path.Combine("config", "consoles", "connection.sql"),
            ConnectionConsole.PathFor("config", "/:/"));
    }
}
