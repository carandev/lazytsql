using System.Text;
using System.Text.Json;
using LazyTsql.Cli;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Connections;
using LazyTsql.Core.Execution;
using LazyTsql.Infrastructure;
using LazyTsql.Output;

namespace LazyTsql.Tests;

public sealed class CliTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-tests-").FullName;

    private FileConnectionStore Store => new(Path.Combine(_directory, "connections.json"));

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Parses_command_options_flags_and_positionals()
    {
        var args = CliArguments.Parse(["schema", "-c", "dev", "dbo.users", "--json", "--max-rows", "5"]);

        Assert.Equal("schema", args.Command);
        Assert.Equal("dev", args.Get("-c", "--connection"));
        Assert.Equal(["dbo.users"], args.Positionals);
        Assert.True(args.Has("--json"));
        Assert.Equal(5, args.GetInt("-m", "--max-rows", 1000));
    }

    [Fact]
    public void Option_without_value_and_invalid_number_are_usage_errors()
    {
        Assert.Throws<CliException>(() => CliArguments.Parse(["query", "-q"]));
        Assert.Throws<CliException>(() => CliArguments.Parse(["query", "--max-rows", "0"]).GetInt("-m", "--max-rows", 1));
    }

    [Fact]
    public void Store_round_trips_profiles_with_owner_only_permissions()
    {
        Store.Upsert(new ConnectionProfile("dev", "Server=a;"));
        Store.Upsert(new ConnectionProfile("DEV", "Server=b;"));
        Store.Upsert(new ConnectionProfile("app", "Server=c;"));

        Assert.Equal([new ConnectionProfile("app", "Server=c;"), new ConnectionProfile("DEV", "Server=b;")], Store.Load());
        Assert.True(Store.Remove("dev"));
        Assert.False(Store.Remove("dev"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Store.Path));
        }
    }

    [Fact]
    public void Resolver_prefers_saved_profile_then_raw_string_then_environment()
    {
        Store.Upsert(new ConnectionProfile("dev", "Server=db;Password=${PASS};"));
        Func<string, string?> env = name => name switch
        {
            "PASS" => "x",
            ConnectionResolver.EnvironmentVariable => "Server=fromenv;",
            _ => null,
        };

        Assert.Equal("Server=db;Password=x;", ConnectionResolver.Resolve("dev", Store, env));
        Assert.Equal("Server=raw;", ConnectionResolver.Resolve("Server=raw;", Store, env));
        Assert.Equal("Server=fromenv;", ConnectionResolver.Resolve(null, Store, env));
        Assert.Throws<CliException>(() => ConnectionResolver.Resolve("unknown", Store, env));
        Assert.Throws<CliException>(() => ConnectionResolver.Resolve(null, Store, _ => null));
    }

    [Fact]
    public void Json_output_keeps_types_and_maps_message_lines_to_the_script()
    {
        var resultSet = new ResultSet(
            [new ColumnInfo("id", "int"), new ColumnInfo("price", "decimal"), new ColumnInfo("note", "nvarchar")],
            [[1, 12.50m, null]],
            IsTruncated: true);
        var batch = new BatchResult(new SqlBatch("SELECT ...", StartLine: 4), [resultSet], [new ServerMessage("boom", 208, 16, 2)], -1, TimeSpan.Zero);

        using var stream = new MemoryStream();
        JsonResultWriter.WriteBatches(stream, [batch]);
        using var document = JsonDocument.Parse(stream.ToArray());
        var root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        var output = root.GetProperty("batches")[0];
        Assert.Equal(5, output.GetProperty("messages")[0].GetProperty("line").GetInt32());
        Assert.Equal("error", output.GetProperty("messages")[0].GetProperty("level").GetString());
        var row = output.GetProperty("resultSets")[0].GetProperty("rows")[0];
        Assert.Equal(JsonValueKind.Number, row[0].ValueKind);
        Assert.Equal("12.50", row[1].GetRawText());
        Assert.Equal(JsonValueKind.Null, row[2].ValueKind);
        Assert.True(output.GetProperty("resultSets")[0].GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public void Text_output_aligns_columns_and_shows_null()
    {
        var resultSet = new ResultSet([new ColumnInfo("id", "int"), new ColumnInfo("name", "nvarchar")], [[1, "Ana"], [22, null]], false);
        var output = new StringWriter();

        TextResultWriter.WriteTable(output, resultSet);

        Assert.Equal(
            "id | name\n---+-----\n1  | Ana\n22 | NULL\n(2 rows)\n\n",
            output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Value_formatter_is_culture_invariant()
    {
        Assert.Equal("1.5", ValueFormatter.Format(1.5m));
        Assert.Equal("0xCAFE", ValueFormatter.Format(new byte[] { 0xCA, 0xFE }));
        Assert.Equal("2026-01-02", ValueFormatter.Format(new DateTime(2026, 1, 2)));
        Assert.Equal("a⏎b", ValueFormatter.FormatSingleLine("a\nb"));
    }
}
