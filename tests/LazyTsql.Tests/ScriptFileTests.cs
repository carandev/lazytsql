using System.Text;
using LazyTsql.Cli;
using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public sealed class ScriptFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-scripts-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_sql_file_instead_of_a_command_opens_in_the_tui()
    {
        Assert.Equal("q.sql", CliArguments.Parse(["q.sql", "-c", "dev"]).ScriptPath);
        Assert.Equal("dir/Q.SQL", CliArguments.Parse(["-c", "dev", "dir/Q.SQL"]).ScriptPath);
        Assert.Null(CliArguments.Parse(["schema", "dbo.users"]).ScriptPath);
        Assert.Null(CliArguments.Parse(["-c", "dev"]).ScriptPath);
    }

    [Fact]
    public void Saving_keeps_the_encoding_and_line_endings_the_file_was_read_with()
    {
        var crlfWithBom = Path.Combine(_directory, "ssms.sql");
        File.WriteAllText(crlfWithBom, "SELECT 'ñ'\r\nGO\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var utf16 = Path.Combine(_directory, "old.sql");
        File.WriteAllText(utf16, "SELECT 1\r\n", Encoding.Unicode);

        var (script, text) = ScriptFile.Open(crlfWithBom);
        Assert.Equal("SELECT 'ñ'\nGO\n", text);
        script.Save(text + "SELECT 2\n");
        Assert.Equal([0xEF, 0xBB, 0xBF], File.ReadAllBytes(crlfWithBom)[..3]);
        Assert.Equal("SELECT 'ñ'\r\nGO\r\nSELECT 2\r\n", File.ReadAllText(crlfWithBom));

        var (old, oldText) = ScriptFile.Open(utf16);
        old.Save(oldText);
        Assert.Equal(File.ReadAllBytes(utf16), Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("SELECT 1\r\n")).ToArray());
    }

    [Fact]
    public void A_missing_file_opens_empty_and_is_created_as_utf8_without_bom_on_save()
    {
        var path = Path.Combine(_directory, "new.sql");

        var (script, text) = ScriptFile.Open(path);
        Assert.Equal(string.Empty, text);
        Assert.False(File.Exists(path));

        script.Save("SELECT 'á'\n");
        Assert.Equal(Encoding.UTF8.GetBytes("SELECT 'á'\n"), File.ReadAllBytes(path));
    }
}
