using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;

namespace LazyTsql.SqlServer.Tests;

/// <summary>Runs a fake sqlpackage (a shell script) to see exactly what the adapter hands it.</summary>
public sealed class SqlPackageToolTests : IDisposable
{
    private const string ConnectionString = "Server=db.example,1433;User Id=sa;Password=S3cret;TrustServerCertificate=true";
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-sqlpackage-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string FakeSqlPackage(string body)
    {
        var path = Path.Combine(_directory, "sqlpackage");
        // $target is the file sqlpackage is told to write.
        File.WriteAllText(path, $"#!/bin/sh\nrsp=\"${{1#@}}\"\ntarget=$(sed -n 's|^/TargetFile:\"\\(.*\\)\"$|\\1|p' \"$rsp\")\n{body}\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static SqlPackageTool Tool(string? executable, string? path = null) =>
        new(name => name switch
        {
            SqlPackageTool.EnvironmentVariable => executable,
            "PATH" => path ?? string.Empty,
            "HOME" => "/nonexistent",
            _ => null,
        });

    [Fact]
    public async Task Export_passes_the_connection_in_a_private_response_file_and_streams_the_output()
    {
        if (OperatingSystem.IsWindows()) return;
        var record = Path.Combine(_directory, "record");
        var executable = FakeSqlPackage($"""
            echo "argc=$# arg=$1" > {record}
            stat -c %a "$rsp" >> {record}
            cat "$rsp" >> {record}
            echo "Connecting to database 'shop'"
            echo exported > "$target"
            echo "Successfully exported database"
            """);
        var output = new List<string>();

        await Tool(executable).ExportAsync(ConnectionString, "shop", Path.Combine(_directory, "shop.bacpac"), output.Add, CancellationToken.None);

        var lines = File.ReadAllLines(record);
        Assert.Matches(@"^argc=1 arg=@.*\.rsp$", lines[0]);
        Assert.DoesNotContain("S3cret", lines[0]);
        Assert.Equal("600", lines[1]);
        Assert.Equal("/Action:Export", lines[2]);
        Assert.Equal("/SourceConnectionString:\"data source=db.example,1433;initial catalog=shop;user id=sa;password=S3cret;trust server certificate=True\"", lines[3]);
        Assert.Matches($"^/TargetFile:\"{_directory}/shop\\.partial-[0-9a-f]{{32}}\\.bacpac\"$", lines[4]);
        Assert.Equal("exported\n", File.ReadAllText(Path.Combine(_directory, "shop.bacpac")));
        Assert.Equal(["record", "shop.bacpac", "sqlpackage"], Directory.GetFiles(_directory).Select(Path.GetFileName).Order());
        Assert.Equal(["Connecting to database 'shop'", "Successfully exported database"], output);
        Assert.False(File.Exists(lines[0][(lines[0].IndexOf("arg=@", StringComparison.Ordinal) + 5)..]));
    }

    [Fact]
    public async Task Import_targets_the_new_database_from_the_source_file()
    {
        if (OperatingSystem.IsWindows()) return;
        var record = Path.Combine(_directory, "record");
        var executable = FakeSqlPackage($"cat \"$rsp\" > {record}");

        await Tool(executable).ImportAsync(ConnectionString, "shop_copy", "shop.bacpac", _ => { }, CancellationToken.None);

        var lines = File.ReadAllLines(record);
        Assert.Equal("/Action:Import", lines[0]);
        Assert.StartsWith("/TargetConnectionString:\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("initial catalog=shop_copy", lines[1], StringComparison.Ordinal);
        Assert.Equal($"/SourceFile:\"{Path.GetFullPath("shop.bacpac")}\"", lines[2]);
    }

    [Fact]
    public async Task Values_are_single_quoted_because_a_response_file_cannot_hold_a_double_quote_inside_a_value()
    {
        if (OperatingSystem.IsWindows()) return;
        var record = Path.Combine(_directory, "record");
        var executable = FakeSqlPackage($"sed -n 2p \"$rsp\" > {record}\necho x > \"$target\"");

        await Tool(executable).ExportAsync("Server=db;User Id=odd;Password=\"Ab1'x;y z!\"", "my db", Path.Combine(_directory, "x.bacpac"), _ => { }, CancellationToken.None);

        var line = File.ReadAllText(record).Trim();
        var connectionString = line["/SourceConnectionString:\"".Length..^1];
        Assert.DoesNotContain('"', connectionString);
        var parsed = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        Assert.Equal(("Ab1'x;y z!", "my db", "odd"), (parsed.Password, parsed.InitialCatalog, parsed.UserID));
    }

    [Fact]
    public async Task A_double_quote_in_the_connection_is_refused_before_starting_sqlpackage()
    {
        if (OperatingSystem.IsWindows()) return;
        var record = Path.Combine(_directory, "record");
        var executable = FakeSqlPackage($"touch {record}");

        var error = await Assert.ThrowsAsync<BacpacException>(() =>
            Tool(executable).ExportAsync("Server=db;User Id=odd;Password='Ab1\"x'", "shop", "x.bacpac", _ => { }, CancellationToken.None));

        Assert.Equal("sqlpackage cannot read a connection string or file path containing a double quote (\"). Change that password or path.", error.Message);
        Assert.False(File.Exists(record));
    }

    [Fact]
    public async Task A_failed_or_cancelled_export_leaves_an_existing_file_untouched()
    {
        if (OperatingSystem.IsWindows()) return;
        var file = Path.Combine(_directory, "shop.bacpac");
        File.WriteAllText(file, "previous");
        var executable = FakeSqlPackage("echo partial > \"$target\"\necho '*** Error exporting database' >&2\nexit 1");

        await Assert.ThrowsAsync<BacpacException>(() => Tool(executable).ExportAsync(ConnectionString, "shop", file, _ => { }, CancellationToken.None));

        Assert.Equal("previous", File.ReadAllText(file));
        Assert.Equal(["shop.bacpac", "sqlpackage"], Directory.GetFiles(_directory).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task A_failure_reports_the_exit_code_and_the_last_lines()
    {
        if (OperatingSystem.IsWindows()) return;
        var executable = FakeSqlPackage("echo 'Importing...'\necho '*** Error importing database: The database already exists.' >&2\nexit 1");

        var error = await Assert.ThrowsAsync<BacpacException>(() =>
            Tool(executable).ImportAsync(ConnectionString, "shop", "shop.bacpac", _ => { }, CancellationToken.None));

        Assert.Equal("sqlpackage import failed (exit code 1): *** Error importing database: The database already exists.", error.Message);
    }

    [Fact]
    public async Task Cancelling_stops_sqlpackage_and_removes_the_response_file()
    {
        if (OperatingSystem.IsWindows()) return;
        var record = Path.Combine(_directory, "record");
        var executable = FakeSqlPackage($"echo \"$rsp\" > {record}\necho started\nsleep 30");
        using var cancellation = new CancellationTokenSource();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Tool(executable).ExportAsync(ConnectionString, "shop", Path.Combine(_directory, "shop.bacpac"), _ => cancellation.Cancel(), cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        Assert.False(File.Exists(File.ReadAllText(record).Trim()));
    }

    [Fact]
    public async Task Without_sqlpackage_the_error_says_how_to_install_it()
    {
        var error = await Assert.ThrowsAsync<BacpacException>(() =>
            Tool(executable: null, path: _directory).ExportAsync(ConnectionString, "shop", "shop.bacpac", _ => { }, CancellationToken.None));

        Assert.Equal("sqlpackage not found. Install it with 'dotnet tool install -g microsoft.sqlpackage' or set LAZYTSQL_SQLPACKAGE to its path.", error.Message);
    }

    [Fact]
    public async Task It_is_found_on_the_PATH()
    {
        if (OperatingSystem.IsWindows()) return;
        FakeSqlPackage("echo found\necho x > \"$target\"");
        var output = new List<string>();

        await Tool(executable: null, path: $"/nonexistent:{_directory}").ExportAsync(ConnectionString, "shop", Path.Combine(_directory, "shop.bacpac"), output.Add, CancellationToken.None);

        Assert.Equal(["found"], output);
    }

    [SqlServerFact]
    public async Task A_database_exported_to_a_bacpac_imports_as_a_new_database()
    {
        if (Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Any(dir => File.Exists(Path.Combine(dir, "sqlpackage"))) != true
            && !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "sqlpackage")))
        {
            return;
        }

        var source = $"lazytsql_{Guid.NewGuid():N}";
        var copy = $"{source}_copy";
        var file = Path.Combine(_directory, "source.bacpac");
        await using var session = await new SqlServerConnector().OpenAsync(SqlServerFactAttribute.ConnectionString!, CancellationToken.None);
        await RunAsync(session, $"CREATE DATABASE [{source}]");
        try
        {
            await RunAsync(session, $"EXEC [{source}].sys.sp_executesql N'CREATE TABLE dbo.items (id int PRIMARY KEY, name nvarchar(20)); INSERT dbo.items VALUES (1, N''uno''), (2, N''dos'');'");
            var tool = new SqlPackageTool(Environment.GetEnvironmentVariable);

            await tool.ExportAsync(SqlServerFactAttribute.ConnectionString!, source, file, _ => { }, CancellationToken.None);
            await tool.ImportAsync(SqlServerFactAttribute.ConnectionString!, copy, file, _ => { }, CancellationToken.None);

            var result = await RunAsync(session, $"SELECT name FROM [{copy}].dbo.items ORDER BY id");
            Assert.Equal(["uno", "dos"], result.ResultSets[0].Rows.Select(row => (string)row[0]!));
        }
        finally
        {
            await RunAsync(session, $"""
                IF DB_ID('{copy}') IS NOT NULL BEGIN ALTER DATABASE [{copy}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{copy}] END;
                ALTER DATABASE [{source}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{source}]
                """);
        }
    }

    private static Task<BatchResult> RunAsync(IDatabaseSession session, string sql) =>
        session.ExecuteAsync(new SqlBatch(sql, 1), new QueryOptions(), CancellationToken.None);
}
