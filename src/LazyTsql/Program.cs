using LazyTsql.Cli;
using LazyTsql.Infrastructure;
using LazyTsql.Output;
using LazyTsql.SqlServer;
using LazyTsql.Tui;

const string Usage = """
    lazytsql - a lazygit-style SQL Server client

    Usage:
      lazytsql [-c <connection>] [<file.sql>]    Open the TUI, optionally editing a script
      lazytsql query  [-c <conn>] [-d <db>] (-q <sql> | -f <file> | stdin) [--json | --csv] [--max-rows N] [--timeout S]
      lazytsql schema [-c <conn>] [-d <db>] [<schema.object>] [--json]
      lazytsql databases [-c <conn>]
      lazytsql connections [list | add <name> <connection string> | remove <name>]

    <connection> is a saved name or a raw connection string; defaults to $LAZYTSQL_CONNECTION.
    Connection strings may reference environment variables as ${VAR}.
    --csv writes rows only to stdout (one block per result set) and messages to stderr.
    Exit codes: 0 success, 1 SQL errors, 2 usage or connection errors.
    """;

var json = args.Contains("--json");
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
    var commands = new HeadlessCommands(connector, store, Console.Out, Console.OpenStandardOutput(), Console.Error, Console.In, Environment.GetEnvironmentVariable);

    return arguments.Command switch
    {
        null => TuiApplication.Run(connector, store, arguments.Get("-c", "--connection"), arguments.ScriptPath),
        "query" => await commands.QueryAsync(arguments, cancellation.Token),
        "schema" => await commands.SchemaAsync(arguments, cancellation.Token),
        "databases" => await commands.DatabasesAsync(arguments, cancellation.Token),
        "connections" => commands.Connections(arguments),
        _ when arguments.ScriptPath is { } script => TuiApplication.Run(connector, store, arguments.Get("-c", "--connection"), script),
        _ => throw new CliException($"Unknown command '{arguments.Command}'. Run 'lazytsql --help'."),
    };
}
catch (Exception ex)
{
    if (json)
    {
        using var stdout = Console.OpenStandardOutput();
        JsonResultWriter.WriteError(stdout, ex.Message);
        stdout.WriteByte((byte)'\n');
    }
    else Console.Error.WriteLine($"lazytsql: {ex.Message}");
    return 2;
}
