using LazyTsql.Cli;
using LazyTsql.Infrastructure;
using LazyTsql.Output;
using LazyTsql.SqlServer;
using LazyTsql.Tui;

const string Usage = """
    lazytsql - a lazygit-style SQL Server client

    Usage:
      lazytsql [-c <connection>] [<file.sql> | <dir>]  Open the TUI, optionally editing a script or browsing a folder of scripts
      lazytsql query  [-c <conn>] [-d <db>] (-q <sql> | -f <file> | stdin) [--json | --csv] [--max-rows N] [--timeout S]
      lazytsql schema [-c <conn>] [-d <db>] [<schema.object>] [--json]
      lazytsql databases [-c <conn>]
      lazytsql export [-c <conn>] [-d <db>] <file.bacpac>     Export a database with sqlpackage
      lazytsql import [-c <conn>] [-d <new db>] <file.bacpac> Import a .bacpac as a new database
      lazytsql connections [list | add <name> <connection string> | remove <name>]

    <connection> is a saved name or a raw connection string; defaults to $LAZYTSQL_CONNECTION.
    Connection strings may reference environment variables as ${VAR}.
    --csv writes rows only to stdout (one block per result set) and messages to stderr.
    export/import need sqlpackage (dotnet tool install -g microsoft.sqlpackage, or set $LAZYTSQL_SQLPACKAGE);
    the database defaults to the connection's, and sqlpackage's progress goes to stderr.
    Exit codes: 0 success, 1 SQL errors, 2 usage or connection errors.
    Activity (SQL run, messages, exports, imports) is logged to ~/.local/state/lazytsql/logs/<date>.log for 30 days;
    set $LAZYTSQL_LOG_DIR to log elsewhere. Connection strings are never logged.
    """;

var json = args.Contains("--json");
var activity = ActivityLog.Default(Environment.GetEnvironmentVariable);
try
{
    var arguments = CliArguments.Parse(args);
    if (arguments.Has("-h") || arguments.Has("--help") || arguments.Command == "help")
    {
        Console.WriteLine(Usage);
        return 0;
    }

    if (arguments.Has("--version"))
    {
        Console.WriteLine(typeof(Program).Assembly.GetName().Version?.ToString(3));
        return 0;
    }

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancellation.Cancel();
    };

    var store = FileConnectionStore.Default();
    var connector = new SqlServerConnector();
    var bacpac = new SqlPackageTool(Environment.GetEnvironmentVariable);
    var commands = new HeadlessCommands(connector, bacpac, store, Console.Out, Console.OpenStandardOutput(), Console.Error, Console.In, Environment.GetEnvironmentVariable, activity);
    activity.Prune();

    return arguments.Command switch
    {
        null => TuiApplication.Run(connector, bacpac, store, activity, arguments.Get("-c", "--connection"), arguments.ScriptPath, arguments.DirectoryPath),
        "query" => await commands.QueryAsync(arguments, cancellation.Token),
        "schema" => await commands.SchemaAsync(arguments, cancellation.Token),
        "databases" => await commands.DatabasesAsync(arguments, cancellation.Token),
        "export" => await commands.ExportAsync(arguments, cancellation.Token),
        "import" => await commands.ImportAsync(arguments, cancellation.Token),
        "connections" => commands.Connections(arguments),
        _ when arguments.ScriptPath is not null || arguments.DirectoryPath is not null =>
            TuiApplication.Run(connector, bacpac, store, activity, arguments.Get("-c", "--connection"), arguments.ScriptPath, arguments.DirectoryPath),
        _ => throw new CliException($"Unknown command '{arguments.Command}'. Run 'lazytsql --help'."),
    };
}
catch (Exception ex)
{
    activity.Write("cli", $"✗ {ex.Message}");
    if (json)
    {
        using var stdout = Console.OpenStandardOutput();
        JsonResultWriter.WriteError(stdout, ex.Message);
        stdout.WriteByte((byte)'\n');
    }
    else Console.Error.WriteLine($"lazytsql: {ex.Message}");
    return 2;
}
