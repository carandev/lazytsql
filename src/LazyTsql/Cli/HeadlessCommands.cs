using LazyTsql.Core;
using LazyTsql.Core.Connections;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Infrastructure;
using LazyTsql.Output;

namespace LazyTsql.Cli;

/// <summary>Non-interactive commands meant for scripts and agents. Exit codes: 0 success, 1 SQL errors, 2 usage or connection errors.</summary>
public sealed class HeadlessCommands(
    IDatabaseConnector connector,
    IBacpacTool bacpac,
    FileConnectionStore store,
    TextWriter stdout,
    Stream stdoutStream,
    TextWriter stderr,
    TextReader stdin,
    Func<string, string?> getEnvironmentVariable)
{
    public async Task<int> QueryAsync(CliArguments args, CancellationToken cancellationToken)
    {
        if (args.Has("--json") && args.Has("--csv")) throw new CliException("Use either --json or --csv, not both.");
        var script = ReadScript(args);
        var options = new QueryOptions(args.GetInt("-m", "--max-rows", 1000), args.GetInt("-t", "--timeout", 30));
        await using var session = await OpenAsync(args, cancellationToken);

        var results = new List<BatchResult>();
        await foreach (var result in ScriptRunner.RunAsync(session, script, options, cancellationToken))
        {
            results.Add(result);
        }

        if (args.Has("--json")) WriteJson(stream => JsonResultWriter.WriteBatches(stream, results));
        else if (args.Has("--csv")) WriteCsv(results);
        else TextResultWriter.WriteBatches(stdout, results);

        return results.Any(r => r.HasErrors) ? 1 : 0;
    }

    public async Task<int> SchemaAsync(CliArguments args, CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(args, cancellationToken);
        var objects = await session.GetObjectsAsync(session.CurrentDatabase, cancellationToken);
        var objectName = args.Positionals.FirstOrDefault();

        if (objectName is null)
        {
            if (args.Has("--json")) WriteJson(stream => JsonResultWriter.WriteObjects(stream, session.CurrentDatabase, objects));
            else foreach (var item in objects) stdout.WriteLine($"{item.Kind.ToString().ToLowerInvariant(),-9} {item.Schema}.{item.Name}");
            return 0;
        }

        var target = FindObject(objects, objectName);
        if (target.Kind is DbObjectKind.Table or DbObjectKind.View)
        {
            var columns = await session.GetColumnsAsync(session.CurrentDatabase, target, cancellationToken);
            if (args.Has("--json")) WriteJson(stream => JsonResultWriter.WriteColumns(stream, target, columns));
            else foreach (var c in columns) stdout.WriteLine($"{c.Name,-30} {c.DataType,-20} {(c.IsNullable ? "NULL" : "NOT NULL")}{(c.IsPrimaryKey ? " PK" : "")}{(c.IsIdentity ? " IDENTITY" : "")}");
        }
        else
        {
            stdout.WriteLine(await session.GetDefinitionAsync(target, cancellationToken));
        }

        return 0;
    }

    public async Task<int> DatabasesAsync(CliArguments args, CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(args, cancellationToken);
        foreach (var database in await session.GetDatabasesAsync(cancellationToken)) stdout.WriteLine(database);
        return 0;
    }

    public Task<int> ExportAsync(CliArguments args, CancellationToken cancellationToken) =>
        RunBacpacAsync(args, "export", (connection, database, file) =>
            bacpac.ExportAsync(connection, database, file, stderr.WriteLine, cancellationToken), "Exported {0} to {1}", cancellationToken);

    public Task<int> ImportAsync(CliArguments args, CancellationToken cancellationToken) =>
        RunBacpacAsync(args, "import", (connection, database, file) =>
            bacpac.ImportAsync(connection, database, file, stderr.WriteLine, cancellationToken), "Imported {1} as {0}", cancellationToken);

    /// <summary>
    /// export/import: sqlpackage's progress goes to stderr and one summary line to stdout. The database is -d or the
    /// connection's; a file name without extension gets .bacpac.
    /// </summary>
    private async Task<int> RunBacpacAsync(CliArguments args, string command, Func<string, string, string, Task> run, string done, CancellationToken cancellationToken)
    {
        if (args.Has("--json") || args.Has("--csv")) throw new CliException($"{command} does not support --json or --csv.");
        if (args.Positionals.Count != 1) throw new CliException($"Usage: lazytsql {command} [-c <conn>] [-d <db>] <file.bacpac>");
        var file = Path.GetFullPath(Path.HasExtension(args.Positionals[0]) ? args.Positionals[0] : $"{args.Positionals[0]}.bacpac");
        if (command == "import" && !File.Exists(file)) throw new CliException($"File not found: {args.Positionals[0]}");

        var connectionString = ConnectionResolver.Resolve(args.Get("-c", "--connection"), store, getEnvironmentVariable);
        var database = args.Get("-d", "--database") ?? ConnectionDatabase(connectionString)
            ?? throw new CliException("Name the database with -d <database>.");
        try
        {
            await run(connectionString, database, file);
        }
        catch (BacpacException ex)
        {
            stderr.WriteLine($"lazytsql: {ex.Message}");
            return 1;
        }

        stdout.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, done, database, file));
        return 0;
    }

    private static string? ConnectionDatabase(string connectionString)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
        return new[] { "Database", "Initial Catalog" }
            .Select(key => builder.TryGetValue(key, out var value) ? value?.ToString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public int Connections(CliArguments args)
    {
        switch (args.Positionals.FirstOrDefault())
        {
            case null or "list":
                foreach (var profile in store.Load()) stdout.WriteLine(profile.Name);
                return 0;
            case "add" when args.Positionals.Count == 3:
                store.Upsert(new ConnectionProfile(args.Positionals[1], args.Positionals[2]));
                stdout.WriteLine($"Saved '{args.Positionals[1]}' to {store.Path}");
                return 0;
            case "remove" when args.Positionals.Count == 2:
                if (!store.Remove(args.Positionals[1])) throw new CliException($"Unknown connection '{args.Positionals[1]}'.");
                stdout.WriteLine($"Removed '{args.Positionals[1]}'");
                return 0;
            default:
                throw new CliException("Usage: lazytsql connections [list | add <name> <connection string> | remove <name>]");
        }
    }

    public async Task<IDatabaseSession> OpenAsync(CliArguments args, CancellationToken cancellationToken)
    {
        var connectionString = ConnectionResolver.Resolve(args.Get("-c", "--connection"), store, getEnvironmentVariable);
        var session = await connector.OpenAsync(connectionString, cancellationToken);
        if (args.Get("-d", "--database") is { } database) await session.ChangeDatabaseAsync(database, cancellationToken);
        return session;
    }

    private string ReadScript(CliArguments args)
    {
        if (args.Get("-q", "--query") is { } query) return query;
        if (args.Get("-f", "--file") is { } file) return File.ReadAllText(file);
        if (Console.IsInputRedirected) return stdin.ReadToEnd();
        throw new CliException("No SQL given. Use -q \"<sql>\", -f <file> or pipe it through stdin.");
    }

    private static DbObject FindObject(IReadOnlyList<DbObject> objects, string name)
    {
        var parts = name.Replace("[", "").Replace("]", "").Split('.', 2);
        var (schema, objectName) = parts.Length == 2 ? (parts[0], parts[1]) : ("dbo", parts[0]);
        return objects.FirstOrDefault(o =>
                   o.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase) &&
                   o.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
               ?? throw new CliException($"Object '{name}' was not found in the current database.");
    }

    /// <summary>Only rows go to stdout so the output can be redirected to a file; messages go to stderr.</summary>
    private void WriteCsv(IReadOnlyList<BatchResult> results)
    {
        var first = true;
        foreach (var result in results)
        {
            foreach (var resultSet in result.ResultSets)
            {
                if (!first) stdout.Write('\n');
                CsvResultWriter.Write(stdout, resultSet);
                first = false;
            }

            TextResultWriter.WriteMessages(stderr, result);
        }
    }

    private void WriteJson(Action<Stream> write)
    {
        stdout.Flush();
        write(stdoutStream);
        stdoutStream.WriteByte((byte)'\n');
        stdoutStream.Flush();
    }
}
